using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using InvoicePlatform.Application.Identity;
using InvoicePlatform.Domain.Identity;
using Microsoft.Extensions.Options;

namespace InvoicePlatform.Infrastructure.Identity;

/// <summary>
/// API keys in the identity table, next to sessions, written twice:
///
///   pk = APIKEY#&lt;sha256&gt;   sk = APIKEY              found by the key, on every call
///   pk = ORG#&lt;org&gt;         sk = APIKEY#&lt;keyId&gt;     listed per organisation
///
/// The key itself is never stored. First-run setup looks only at USER#
/// partitions and user counts at USER# sort keys, so neither item is mistaken
/// for an account.
/// </summary>
internal sealed class DynamoDbApiKeyStore(IAmazonDynamoDB dynamo, IOptions<IdentityOptions> options) : IApiKeyStore
{
    private const string ListPrefix = "APIKEY#";

    private readonly string _table = options.Value.TableName;

    public async Task SaveAsync(ApiKey key, CancellationToken cancellationToken = default)
    {
        await dynamo.PutItemAsync(new PutItemRequest { TableName = _table, Item = ToItem(key, ByHash(key)) }, cancellationToken)
            .ConfigureAwait(false);
        await dynamo.PutItemAsync(new PutItemRequest { TableName = _table, Item = ToItem(key, ByOrganization(key)) }, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ApiKey?> FindByHashAsync(string hash, CancellationToken cancellationToken = default)
    {
        var response = await dynamo.GetItemAsync(
            new GetItemRequest { TableName = _table, Key = Key($"APIKEY#{hash}", "APIKEY"), ConsistentRead = true },
            cancellationToken).ConfigureAwait(false);

        return response.IsItemSet ? ToKey(response.Item) : null;
    }

    public async Task<IReadOnlyList<ApiKey>> ListAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        var keys = new List<ApiKey>();
        Dictionary<string, AttributeValue>? start = null;
        do
        {
            var response = await dynamo.QueryAsync(
                new QueryRequest
                {
                    TableName = _table,
                    KeyConditionExpression = "pk = :pk AND begins_with(sk, :sk)",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        [":pk"] = new($"ORG#{organizationId}"),
                        [":sk"] = new(ListPrefix),
                    },
                    ExclusiveStartKey = start,
                },
                cancellationToken).ConfigureAwait(false);

            keys.AddRange(response.Items.Select(ToKey));
            start = response.LastEvaluatedKey is { Count: > 0 } next ? next : null;
        }
        while (start is not null);

        return keys;
    }

    public async Task TouchAsync(ApiKey key, DateTimeOffset at, CancellationToken cancellationToken = default)
    {
        var used = key with { LastUsedAt = at };
        foreach (var item in new[] { ToItem(used, ByHash(used)), ToItem(used, ByOrganization(used)) })
        {
            try
            {
                // Only over an item that is still there: a use racing a revoke
                // must not bring the key back.
                await dynamo.PutItemAsync(
                    new PutItemRequest { TableName = _table, Item = item, ConditionExpression = "attribute_exists(pk)" },
                    cancellationToken).ConfigureAwait(false);
            }
            catch (ConditionalCheckFailedException)
            {
                return;
            }
        }
    }

    public async Task DeleteAsync(ApiKey key, CancellationToken cancellationToken = default)
    {
        // The lookup item first: from then on the key no longer works.
        await dynamo.DeleteItemAsync(new DeleteItemRequest { TableName = _table, Key = ByHash(key) }, cancellationToken)
            .ConfigureAwait(false);
        await dynamo.DeleteItemAsync(new DeleteItemRequest { TableName = _table, Key = ByOrganization(key) }, cancellationToken)
            .ConfigureAwait(false);
    }

    private static Dictionary<string, AttributeValue> ByHash(ApiKey key) => Key($"APIKEY#{key.Hash}", "APIKEY");

    private static Dictionary<string, AttributeValue> ByOrganization(ApiKey key) => Key($"ORG#{key.OrganizationId}", $"{ListPrefix}{key.KeyId}");

    private static Dictionary<string, AttributeValue> Key(string pk, string sk) => new() { ["pk"] = new(pk), ["sk"] = new(sk) };

    private static Dictionary<string, AttributeValue> ToItem(ApiKey key, Dictionary<string, AttributeValue> itemKey)
        => new(itemKey)
        {
            ["keyId"] = new(key.KeyId),
            ["organizationId"] = new(key.OrganizationId),
            ["name"] = new(key.Name),
            ["hash"] = new(key.Hash),
            ["visiblePrefix"] = new(key.VisiblePrefix),
            ["createdByUserId"] = new(key.CreatedByUserId),
            ["createdAt"] = new(key.CreatedAt.ToString("O", CultureInfo.InvariantCulture)),
            ["lastUsedAt"] = key.LastUsedAt is { } last
                ? new AttributeValue(last.ToString("O", CultureInfo.InvariantCulture))
                : new AttributeValue { NULL = true },
        };

    private static ApiKey ToKey(Dictionary<string, AttributeValue> item) => new()
    {
        KeyId = item["keyId"].S,
        OrganizationId = item["organizationId"].S,
        Name = item["name"].S,
        Hash = item["hash"].S,
        VisiblePrefix = item["visiblePrefix"].S,
        CreatedByUserId = item["createdByUserId"].S,
        CreatedAt = DateTimeOffset.Parse(item["createdAt"].S, CultureInfo.InvariantCulture),
        LastUsedAt = item.TryGetValue("lastUsedAt", out var last) && last.S is { } value
            ? DateTimeOffset.Parse(value, CultureInfo.InvariantCulture)
            : null,
    };
}
