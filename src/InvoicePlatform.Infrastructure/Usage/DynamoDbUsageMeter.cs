using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using InvoicePlatform.Application.Identity;
using InvoicePlatform.Application.Usage;
using InvoicePlatform.Infrastructure.Invoices;
using Microsoft.Extensions.Options;

namespace InvoicePlatform.Infrastructure.Usage;

/// <summary>
/// Conversions per organisation, in the invoice table:
///
///   pk = ORG#&lt;organizationId&gt;   sk = ALLOWANCE         used, limit (none: unlimited)
///   pk = ORG#&lt;organizationId&gt;   sk = USAGE#&lt;yyyy-MM&gt;   conversions that month (for billing later)
///
/// Counting is one conditional update, so two uploads at once cannot both take
/// the last conversion.
/// </summary>
internal sealed class DynamoDbUsageMeter(IAmazonDynamoDB dynamo, IOptions<InvoiceStorageOptions> options, IClock clock) : IUsageMeter
{
    private readonly string _table = options.Value.TableName;

    public async Task<bool> TryConsumeAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        try
        {
            await dynamo.UpdateItemAsync(
                new UpdateItemRequest
                {
                    TableName = _table,
                    Key = Key(organizationId, "ALLOWANCE"),
                    UpdateExpression = "SET used = if_not_exists(used, :zero) + :one",
                    ConditionExpression = "attribute_not_exists(#limit) OR used < #limit",
                    ExpressionAttributeNames = new Dictionary<string, string> { ["#limit"] = "limit" },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        [":zero"] = new AttributeValue { N = "0" },
                        [":one"] = new AttributeValue { N = "1" },
                    },
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }

        await dynamo.UpdateItemAsync(
            new UpdateItemRequest
            {
                TableName = _table,
                Key = Key(organizationId, $"USAGE#{clock.UtcNow:yyyy-MM}"),
                UpdateExpression = "ADD conversions :one",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":one"] = new AttributeValue { N = "1" } },
            },
            cancellationToken).ConfigureAwait(false);

        return true;
    }

    public async Task RefundAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        try
        {
            await dynamo.UpdateItemAsync(
                new UpdateItemRequest
                {
                    TableName = _table,
                    Key = Key(organizationId, "ALLOWANCE"),
                    UpdateExpression = "SET used = used - :one",
                    ConditionExpression = "used > :zero",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        [":zero"] = new AttributeValue { N = "0" },
                        [":one"] = new AttributeValue { N = "1" },
                    },
                },
                cancellationToken).ConfigureAwait(false);

            await dynamo.UpdateItemAsync(
                new UpdateItemRequest
                {
                    TableName = _table,
                    Key = Key(organizationId, $"USAGE#{clock.UtcNow:yyyy-MM}"),
                    UpdateExpression = "ADD conversions :minus",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":minus"] = new AttributeValue { N = "-1" } },
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (ConditionalCheckFailedException)
        {
            // Nothing was counted; nothing to give back.
        }
    }

    public async Task<UsageSummary> GetAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        var response = await dynamo.GetItemAsync(
            new GetItemRequest { TableName = _table, Key = Key(organizationId, "ALLOWANCE"), ConsistentRead = true },
            cancellationToken).ConfigureAwait(false);

        if (!response.IsItemSet)
        {
            return new UsageSummary(0, null);
        }

        var used = response.Item.TryGetValue("used", out var u) ? int.Parse(u.N, CultureInfo.InvariantCulture) : 0;
        int? limit = response.Item.TryGetValue("limit", out var l) && l.N is not null ? int.Parse(l.N, CultureInfo.InvariantCulture) : null;
        var trial = response.Item.TryGetValue("plan", out var p) && p.S == "trial";
        return new UsageSummary(used, limit, trial);
    }

    public Task SetLimitAsync(string organizationId, int? limit, bool trial = false, CancellationToken cancellationToken = default)
        => dynamo.UpdateItemAsync(
            limit is { } value
                ? new UpdateItemRequest
                {
                    TableName = _table,
                    Key = Key(organizationId, "ALLOWANCE"),
                    UpdateExpression = "SET #limit = :limit, used = if_not_exists(used, :zero), #plan = :plan",
                    ExpressionAttributeNames = new Dictionary<string, string> { ["#limit"] = "limit", ["#plan"] = "plan" },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        [":limit"] = new AttributeValue { N = value.ToString(CultureInfo.InvariantCulture) },
                        [":zero"] = new AttributeValue { N = "0" },
                        [":plan"] = trial ? new AttributeValue("trial") : new AttributeValue { NULL = true },
                    },
                }
                : new UpdateItemRequest
                {
                    TableName = _table,
                    Key = Key(organizationId, "ALLOWANCE"),
                    UpdateExpression = "REMOVE #limit, #plan",
                    ExpressionAttributeNames = new Dictionary<string, string> { ["#limit"] = "limit", ["#plan"] = "plan" },
                },
            cancellationToken);

    private static Dictionary<string, AttributeValue> Key(string organizationId, string sk) => new()
    {
        ["pk"] = new($"ORG#{organizationId}"),
        ["sk"] = new(sk),
    };
}
