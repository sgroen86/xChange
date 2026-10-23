using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Domain.Identity;
using Microsoft.Extensions.Options;

namespace InvoicePlatform.Infrastructure.Identity;

/// <summary>
/// An organisation's items in the identity table, deleted when the
/// organisation is (see the stores that write them for the layout):
///
///   ORG#&lt;org&gt;          / USER#&lt;id&gt;            the account by id     (<see cref="DynamoDbIdentityStore"/>)
///   USER#&lt;email&gt;       / PROFILE              the account by email  (<see cref="DynamoDbIdentityStore"/>)
///   ORG#&lt;org&gt;          / MFA#&lt;userId&gt;         two-step verification (<see cref="DynamoDbMfaStore"/>)
///   ORG#&lt;org&gt;          / INBOX                the inbox address     (DynamoDbInboxDirectory)
///   INBOX#&lt;localPart&gt;   / INBOX                and back
///   ORG#&lt;org&gt;          / SETTINGS             the organisation's settings
///   PLATFORM            / ORGANIZATION#&lt;org&gt;   the organisation      (<see cref="DynamoDbPlatformStore"/>)
///
/// Items outside the organisation's partition are deleted only when they
/// still name this organisation, so nothing of another one can go. Sessions
/// (SESSION#&lt;hash&gt;) and MFA challenges are keyed by a token hash and cannot
/// be found per organisation: they stop working with the account and expire
/// by TTL. The open invitation goes through <see cref="IPlatformStore"/>.
/// </summary>
internal sealed class DynamoDbOrganizationIdentityDeletion(
    IAmazonDynamoDB dynamo,
    IOptions<IdentityOptions> options)
    : IOrganizationIdentityDeletion
{
    private readonly string _table = options.Value.TableName;

    public async Task<IReadOnlyList<User>> ListUsersAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        var users = new List<User>();
        Dictionary<string, AttributeValue>? start = null;
        do
        {
            var query = Query(organizationId, "USER#");
            query.ExclusiveStartKey = start;
            var response = await dynamo.QueryAsync(query, cancellationToken).ConfigureAwait(false);
            users.AddRange((response.Items ?? []).Select(DynamoDbIdentityStore.ToUser));
            start = response.LastEvaluatedKey is { Count: > 0 } last ? last : null;
        }
        while (start is not null);

        return users;
    }

    public async Task<int> DeleteUsersAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        var users = await ListUsersAsync(organizationId, cancellationToken).ConfigureAwait(false);
        foreach (var user in users)
        {
            // The login item first, so nobody can sign in; then the item every
            // request is checked against, which ends the user's sessions.
            await DeleteIfOwnedAsync(Key($"USER#{user.Email}", "PROFILE"), organizationId, cancellationToken).ConfigureAwait(false);
            await DeleteAsync(Key($"ORG#{organizationId}", $"USER#{user.Id}"), cancellationToken).ConfigureAwait(false);
        }

        return users.Count;
    }

    public Task<int> DeleteMfaAsync(string organizationId, CancellationToken cancellationToken = default)
        => DynamoDbBatchDelete.DeleteQueriedAsync(dynamo, Query(organizationId, "MFA#"), cancellationToken);

    public async Task<int> DeleteInboxAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        var response = await dynamo.GetItemAsync(
            new GetItemRequest { TableName = _table, Key = Key($"ORG#{organizationId}", "INBOX"), ConsistentRead = true },
            cancellationToken).ConfigureAwait(false);

        var deleted = 0;
        if (response.IsItemSet && response.Item.TryGetValue("localPart", out var localPart) && localPart.S is { } local)
        {
            // The address first: from then on mail to it is not accepted.
            deleted += await DeleteIfOwnedAsync(Key($"INBOX#{local}", "INBOX"), organizationId, cancellationToken).ConfigureAwait(false);
        }

        return deleted + await DeleteAsync(Key($"ORG#{organizationId}", "INBOX"), cancellationToken).ConfigureAwait(false);
    }

    public Task<int> DeleteSettingsAsync(string organizationId, CancellationToken cancellationToken = default)
        => DeleteAsync(Key($"ORG#{organizationId}", "SETTINGS"), cancellationToken);

    public Task<int> DeleteOrganizationRecordAsync(string organizationId, CancellationToken cancellationToken = default)
        => DeleteAsync(Key("PLATFORM", $"ORGANIZATION#{organizationId}"), cancellationToken);

    private QueryRequest Query(string organizationId, string sortPrefix) => new()
    {
        TableName = _table,
        KeyConditionExpression = "pk = :pk AND begins_with(sk, :sk)",
        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
        {
            [":pk"] = new($"ORG#{organizationId}"),
            [":sk"] = new(sortPrefix),
        },
        ConsistentRead = true,
    };

    /// <returns>1 when the item was there, 0 when it was already gone.</returns>
    private async Task<int> DeleteAsync(Dictionary<string, AttributeValue> key, CancellationToken cancellationToken)
    {
        var response = await dynamo.DeleteItemAsync(
            new DeleteItemRequest { TableName = _table, Key = key, ReturnValues = ReturnValue.ALL_OLD },
            cancellationToken).ConfigureAwait(false);
        return response.Attributes is { Count: > 0 } ? 1 : 0;
    }

    /// <summary>Deletes an item outside the organisation's partition only while it still names the organisation.</summary>
    private async Task<int> DeleteIfOwnedAsync(Dictionary<string, AttributeValue> key, string organizationId, CancellationToken cancellationToken)
    {
        try
        {
            await dynamo.DeleteItemAsync(
                new DeleteItemRequest
                {
                    TableName = _table,
                    Key = key,
                    ConditionExpression = "organizationId = :org",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":org"] = new(organizationId) },
                },
                cancellationToken).ConfigureAwait(false);
            return 1;
        }
        catch (ConditionalCheckFailedException)
        {
            // Gone already, or it belongs to someone else now: either way not ours to delete.
            return 0;
        }
    }

    private static Dictionary<string, AttributeValue> Key(string pk, string sk)
        => new() { ["pk"] = new(pk), ["sk"] = new(sk) };
}
