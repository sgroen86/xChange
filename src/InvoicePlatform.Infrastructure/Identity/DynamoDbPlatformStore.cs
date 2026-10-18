using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Domain.Identity;
using InvoicePlatform.Domain.Organizations;
using Microsoft.Extensions.Options;

namespace InvoicePlatform.Infrastructure.Identity;

/// <summary>
/// Organisations and invitations, in the identity table next to the accounts:
///
///   PLATFORM          / ORGANIZATION#&lt;id&gt;   one item per organisation
///   INVITATION#&lt;hash&gt; / INVITATION         expires by TTL
///
/// All organisations share one partition key so the platform list is a Query,
/// not a Scan. Users are counted with a Query on the organisation's own
/// partition (ORG#&lt;id&gt; / USER#...). None of these keys start with USER#, so
/// the first-run setup check cannot mistake them for accounts.
/// </summary>
internal sealed class DynamoDbPlatformStore(
    IAmazonDynamoDB dynamo,
    IOptions<IdentityOptions> options)
    : IPlatformStore
{
    private const string PlatformKey = "PLATFORM";
    private const string OrganizationPrefix = "ORGANIZATION#";

    private readonly string _table = options.Value.TableName;

    public async Task<Organization?> FindOrganizationAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        var item = await GetItemAsync(PlatformKey, OrganizationPrefix + organizationId, cancellationToken).ConfigureAwait(false);
        return item is null ? null : ToOrganization(item);
    }

    public async Task<IReadOnlyList<Organization>> ListOrganizationsAsync(CancellationToken cancellationToken = default)
    {
        var items = await QueryAsync(PlatformKey, OrganizationPrefix, countOnly: false, cancellationToken).ConfigureAwait(false);
        return items.Items.Select(ToOrganization).ToList();
    }

    public Task SaveOrganizationAsync(Organization organization, CancellationToken cancellationToken = default)
        => dynamo.PutItemAsync(
            new PutItemRequest
            {
                TableName = _table,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new(PlatformKey),
                    ["sk"] = new(OrganizationPrefix + organization.OrganizationId),
                    ["organizationId"] = new(organization.OrganizationId),
                    ["name"] = new(organization.Name),
                    ["countryCode"] = new(organization.CountryCode),
                    ["createdAt"] = new(organization.CreatedAt.ToString("O")),
                    ["createdByUserId"] = Nullable(organization.CreatedByUserId),
                    ["pendingInvitationHash"] = Nullable(organization.PendingInvitationHash),
                    ["pendingInvitationEmail"] = Nullable(organization.PendingInvitationEmail),
                    ["pendingInvitationExpiresAt"] = Nullable(organization.PendingInvitationExpiresAt?.ToString("O")),
                },
            },
            cancellationToken);

    public async Task<int> CountUsersAsync(string organizationId, CancellationToken cancellationToken = default)
        => (await QueryAsync($"ORG#{organizationId}", "USER#", countOnly: true, cancellationToken).ConfigureAwait(false)).Count;

    public Task SaveInvitationAsync(Invitation invitation, CancellationToken cancellationToken = default)
        => dynamo.PutItemAsync(
            new PutItemRequest
            {
                TableName = _table,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new($"INVITATION#{invitation.CodeHash}"),
                    ["sk"] = new("INVITATION"),
                    ["organizationId"] = new(invitation.OrganizationId),
                    ["email"] = new(invitation.Email),
                    ["name"] = new(invitation.Name),
                    ["createdAt"] = new(invitation.CreatedAt.ToString("O")),
                    ["expiresAtIso"] = new(invitation.ExpiresAt.ToString("O")),
                    // DynamoDB TTL removes it eventually; IsExpired is still
                    // checked on read, because TTL is not punctual.
                    ["expiresAt"] = new AttributeValue
                    {
                        N = invitation.ExpiresAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                    },
                },
            },
            cancellationToken);

    public async Task<Invitation?> FindInvitationAsync(string codeHash, CancellationToken cancellationToken = default)
    {
        var item = await GetItemAsync($"INVITATION#{codeHash}", "INVITATION", cancellationToken).ConfigureAwait(false);
        return item is null
            ? null
            : new Invitation(
                codeHash,
                item["organizationId"].S,
                item["email"].S,
                item["name"].S,
                DateTimeOffset.Parse(item["createdAt"].S, CultureInfo.InvariantCulture),
                DateTimeOffset.Parse(item["expiresAtIso"].S, CultureInfo.InvariantCulture));
    }

    public async Task<bool> TryDeleteInvitationAsync(string codeHash, CancellationToken cancellationToken = default)
    {
        try
        {
            await dynamo.DeleteItemAsync(
                new DeleteItemRequest
                {
                    TableName = _table,
                    Key = Key($"INVITATION#{codeHash}", "INVITATION"),
                    // Of two simultaneous deletes only one meets this condition.
                    ConditionExpression = "attribute_exists(pk)",
                },
                cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }

    private async Task<(List<Dictionary<string, AttributeValue>> Items, int Count)> QueryAsync(
        string partition, string sortPrefix, bool countOnly, CancellationToken cancellationToken)
    {
        var items = new List<Dictionary<string, AttributeValue>>();
        var count = 0;
        Dictionary<string, AttributeValue>? startKey = null;

        do
        {
            var response = await dynamo.QueryAsync(
                new QueryRequest
                {
                    TableName = _table,
                    KeyConditionExpression = "pk = :pk AND begins_with(sk, :sk)",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        [":pk"] = new(partition),
                        [":sk"] = new(sortPrefix),
                    },
                    Select = countOnly ? Select.COUNT : Select.ALL_ATTRIBUTES,
                    ExclusiveStartKey = startKey,
                },
                cancellationToken).ConfigureAwait(false);

            count += response.Count ?? 0;
            if (!countOnly)
            {
                items.AddRange(response.Items);
            }

            startKey = response.LastEvaluatedKey is { Count: > 0 } ? response.LastEvaluatedKey : null;
        }
        while (startKey is not null);

        return (items, count);
    }

    private async Task<Dictionary<string, AttributeValue>?> GetItemAsync(string pk, string sk, CancellationToken cancellationToken)
    {
        var response = await dynamo.GetItemAsync(
            new GetItemRequest { TableName = _table, Key = Key(pk, sk) },
            cancellationToken).ConfigureAwait(false);
        return response.IsItemSet ? response.Item : null;
    }

    private static Dictionary<string, AttributeValue> Key(string pk, string sk)
        => new() { ["pk"] = new(pk), ["sk"] = new(sk) };

    private static AttributeValue Nullable(string? value)
        => value is null ? new AttributeValue { NULL = true } : new AttributeValue(value);

    private static string? Optional(Dictionary<string, AttributeValue> item, string name)
        => item.TryGetValue(name, out var value) ? value.S : null;

    private static Organization ToOrganization(Dictionary<string, AttributeValue> item) => new()
    {
        OrganizationId = item["organizationId"].S,
        Name = item["name"].S,
        CountryCode = item["countryCode"].S,
        CreatedAt = DateTimeOffset.Parse(item["createdAt"].S, CultureInfo.InvariantCulture),
        CreatedByUserId = Optional(item, "createdByUserId"),
        PendingInvitationHash = Optional(item, "pendingInvitationHash"),
        PendingInvitationEmail = Optional(item, "pendingInvitationEmail"),
        PendingInvitationExpiresAt = Optional(item, "pendingInvitationExpiresAt") is { } expires
            ? DateTimeOffset.Parse(expires, CultureInfo.InvariantCulture)
            : null,
    };
}
