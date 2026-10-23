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
///   pk = ORG#&lt;organizationId&gt;   sk = ALLOWANCE         see <see cref="AllowanceItem"/>
///   pk = ORG#&lt;organizationId&gt;   sk = USAGE#&lt;yyyy-MM&gt;   conversions that month
///
/// A conversion is taken from the first bucket that has one, each with one
/// conditional update, so two uploads at once cannot both take the last one:
/// the subscription's period, then purchased credits, then the trial or limit
/// as before billing existed. No read precedes them; the conditions decide.
/// </summary>
internal sealed class DynamoDbUsageMeter(IAmazonDynamoDB dynamo, IOptions<InvoiceStorageOptions> options, IClock clock) : IUsageMeter
{
    private readonly string _table = options.Value.TableName;

    public async Task<ConversionBucket?> TryConsumeAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        ConversionBucket? bucket = null;
        // ConversionRules, as DynamoDB conditions: each bucket checked and counted in one step.
        foreach (var candidate in ConversionRules.Order)
        {
            if (await TryTakeAsync(organizationId, candidate, cancellationToken).ConfigureAwait(false))
            {
                bucket = candidate;
                break;
            }
        }

        if (bucket is null)
        {
            return null;
        }

        await dynamo.UpdateItemAsync(
            new UpdateItemRequest
            {
                TableName = _table,
                Key = AllowanceItem.Key(organizationId, $"USAGE#{clock.UtcNow:yyyy-MM}"),
                UpdateExpression = "ADD conversions :one",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":one"] = new AttributeValue { N = "1" } },
            },
            cancellationToken).ConfigureAwait(false);

        return bucket;
    }

    private async Task<bool> TryTakeAsync(string organizationId, ConversionBucket bucket, CancellationToken cancellationToken)
    {
        var (update, condition) = bucket switch
        {
            ConversionBucket.Subscription => (
                "SET periodUsed = periodUsed + :one",
                "attribute_exists(periodAllowance) AND periodUsed < periodAllowance"),
            ConversionBucket.Credits => (
                "SET credits = credits - :one",
                "credits > :zero"),

            // As before billing: under the limit, or no limit at all - unless the
            // organisation ever subscribed, after which its plan is what it has.
            _ => (
                "SET used = if_not_exists(used, :zero) + :one",
                "(attribute_not_exists(#limit) AND attribute_not_exists(subscribedOnce)) OR used < #limit"),
        };

        try
        {
            await dynamo.UpdateItemAsync(
                new UpdateItemRequest
                {
                    TableName = _table,
                    Key = AllowanceItem.Key(organizationId),
                    UpdateExpression = update,
                    ConditionExpression = condition,
                    ExpressionAttributeNames = bucket == ConversionBucket.Base
                        ? new Dictionary<string, string> { ["#limit"] = "limit" }
                        : null,
                    ExpressionAttributeValues = Values(update + condition),
                },
                cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }

    public async Task RefundAsync(string organizationId, ConversionBucket bucket, CancellationToken cancellationToken = default)
    {
        var (update, condition) = bucket switch
        {
            ConversionBucket.Subscription => ("SET periodUsed = periodUsed - :one", "periodUsed > :zero"),
            ConversionBucket.Credits => ("ADD credits :one", (string?)null),
            _ => ("SET used = used - :one", "used > :zero"),
        };

        try
        {
            await dynamo.UpdateItemAsync(
                new UpdateItemRequest
                {
                    TableName = _table,
                    Key = AllowanceItem.Key(organizationId),
                    UpdateExpression = update,
                    ConditionExpression = condition,
                    ExpressionAttributeValues = Values(update + condition),
                },
                cancellationToken).ConfigureAwait(false);

            await dynamo.UpdateItemAsync(
                new UpdateItemRequest
                {
                    TableName = _table,
                    Key = AllowanceItem.Key(organizationId, $"USAGE#{clock.UtcNow:yyyy-MM}"),
                    UpdateExpression = "ADD conversions :minus",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":minus"] = new AttributeValue { N = "-1" } },
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (ConditionalCheckFailedException)
        {
            // Nothing was counted (or the period was reset since); nothing to give back.
        }
    }

    public async Task<UsageSummary> GetAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        var response = await dynamo.GetItemAsync(
            new GetItemRequest { TableName = _table, Key = AllowanceItem.Key(organizationId), ConsistentRead = true },
            cancellationToken).ConfigureAwait(false);

        if (!response.IsItemSet)
        {
            return new UsageSummary(0, null);
        }

        var item = response.Item;
        var used = AllowanceItem.Int(item, "used") ?? 0;
        var limit = AllowanceItem.Int(item, "limit");

        // Paying once - a subscription or a credit pack - ends showing the trial.
        // It grants nothing by itself: downloading is ExportEntitlement's call.
        var trial = item.TryGetValue("plan", out var p) && p.S == "trial" && !AllowanceItem.Bool(item, "paidOnce");
        return new UsageSummary(used, limit, trial, AllowanceItem.Bool(item, "subscribedOnce"));
    }

    public Task SetLimitAsync(string organizationId, int? limit, bool trial = false, CancellationToken cancellationToken = default)
        => dynamo.UpdateItemAsync(
            limit is { } value
                ? new UpdateItemRequest
                {
                    TableName = _table,
                    Key = AllowanceItem.Key(organizationId),
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
                    Key = AllowanceItem.Key(organizationId),
                    UpdateExpression = "REMOVE #limit, #plan",
                    ExpressionAttributeNames = new Dictionary<string, string> { ["#limit"] = "limit", ["#plan"] = "plan" },
                },
            cancellationToken);

    /// <summary>Only the placeholders an expression uses: DynamoDB rejects unused ones.</summary>
    private static Dictionary<string, AttributeValue> Values(string expressions)
    {
        var values = new Dictionary<string, AttributeValue>();
        if (expressions.Contains(":one", StringComparison.Ordinal))
        {
            values[":one"] = new AttributeValue { N = "1" };
        }

        if (expressions.Contains(":zero", StringComparison.Ordinal))
        {
            values[":zero"] = new AttributeValue { N = "0" };
        }

        return values;
    }
}

/// <summary>
/// The organisation's allowance item, shared by the usage meter and billing:
///
///   pk = ORG#&lt;organizationId&gt;   sk = ALLOWANCE
///     used, limit, plan="trial"           the trial or limit; no limit: unlimited
///     periodAllowance, periodUsed          the subscription's conversions this period
///     credits                              purchased credits left
///     subscribedOnce, paidOnce             ever subscribed / ever paid (ends unlimited / showing the trial)
///     stripeCustomerId, stripeSubscriptionId, subscriptionPlan, subscriptionStatus,
///     periodStart, periodEnd, periodResetFrom, cancelAtPeriodEnd, subscriptionEventAt
///
/// One item, so one read shows everything and each step of counting is one
/// conditional update. Times are Unix seconds.
/// </summary>
internal static class AllowanceItem
{
    public static Dictionary<string, AttributeValue> Key(string organizationId, string sk = "ALLOWANCE") => new()
    {
        ["pk"] = new($"ORG#{organizationId}"),
        ["sk"] = new(sk),
    };

    public static int? Int(Dictionary<string, AttributeValue> item, string name)
        => item.TryGetValue(name, out var value) && value.N is not null ? int.Parse(value.N, CultureInfo.InvariantCulture) : null;

    public static long? Long(Dictionary<string, AttributeValue> item, string name)
        => item.TryGetValue(name, out var value) && value.N is not null ? long.Parse(value.N, CultureInfo.InvariantCulture) : null;

    public static string? String(Dictionary<string, AttributeValue> item, string name)
        => item.TryGetValue(name, out var value) ? value.S : null;

    public static bool Bool(Dictionary<string, AttributeValue> item, string name)
        => item.TryGetValue(name, out var value) && value.BOOL == true;
}
