using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using InvoicePlatform.Application.Identity;
using InvoicePlatform.Domain.Identity;
using Microsoft.Extensions.Options;

namespace InvoicePlatform.Infrastructure.Identity;

/// <summary>
/// Two-step verification in the identity table:
///
///   ORG#&lt;org&gt;             / MFA#&lt;userId&gt;   the user's state, in the organisation's partition
///   MFACHALLENGE#&lt;hash&gt;   / CHALLENGE        a half-finished login, expires by TTL
///
/// The secrets arrive here already encrypted (<see cref="ISecretProtector"/>);
/// recovery codes and challenges only as hashes. Using a step, using a recovery
/// code and using a challenge are each one conditional write, so two
/// simultaneous requests cannot both succeed.
/// </summary>
internal sealed class DynamoDbMfaStore(
    IAmazonDynamoDB dynamo,
    IOptions<IdentityOptions> options) : IMfaStore
{
    private const string Pk = "pk";
    private const string Sk = "sk";
    private const string Ttl = "expiresAt";

    private readonly string _table = options.Value.TableName;

    public async Task<UserMfa?> GetAsync(string organizationId, string userId, CancellationToken cancellationToken = default)
    {
        var response = await dynamo.GetItemAsync(
            new GetItemRequest { TableName = _table, Key = UserKey(organizationId, userId), ConsistentRead = true },
            cancellationToken).ConfigureAwait(false);

        if (!response.IsItemSet)
        {
            return null;
        }

        var item = response.Item;
        return new UserMfa
        {
            OrganizationId = organizationId,
            UserId = userId,
            ProtectedSecret = Optional(item, "secret"),
            PendingProtectedSecret = Optional(item, "pendingSecret"),
            LastUsedStep = item.TryGetValue("lastUsedStep", out var step) && step.N is { } n ? long.Parse(n, CultureInfo.InvariantCulture) : null,
            RecoveryCodeHashes = item.TryGetValue("recoveryCodes", out var codes) && codes.SS is { } set ? set.ToList() : [],
            EnabledAt = Optional(item, "enabledAt") is { } at ? DateTimeOffset.Parse(at, CultureInfo.InvariantCulture) : null,
        };
    }

    public Task SaveAsync(UserMfa mfa, CancellationToken cancellationToken = default)
    {
        var item = UserKey(mfa.OrganizationId, mfa.UserId);
        item["organizationId"] = new(mfa.OrganizationId);
        item["userId"] = new(mfa.UserId);
        Put(item, "secret", mfa.ProtectedSecret);
        Put(item, "pendingSecret", mfa.PendingProtectedSecret);
        Put(item, "enabledAt", mfa.EnabledAt?.ToString("O", CultureInfo.InvariantCulture));
        if (mfa.LastUsedStep is { } step)
        {
            item["lastUsedStep"] = new AttributeValue { N = step.ToString(CultureInfo.InvariantCulture) };
        }

        // A string set cannot be empty: no codes left is no attribute.
        if (mfa.RecoveryCodeHashes.Count > 0)
        {
            item["recoveryCodes"] = new AttributeValue { SS = mfa.RecoveryCodeHashes.ToList() };
        }

        return dynamo.PutItemAsync(new PutItemRequest { TableName = _table, Item = item }, cancellationToken);
    }

    public Task DeleteAsync(string organizationId, string userId, CancellationToken cancellationToken = default)
        => dynamo.DeleteItemAsync(
            new DeleteItemRequest { TableName = _table, Key = UserKey(organizationId, userId) },
            cancellationToken);

    public async Task<bool> TryUseStepAsync(string organizationId, string userId, long step, CancellationToken cancellationToken = default)
    {
        try
        {
            await dynamo.UpdateItemAsync(
                new UpdateItemRequest
                {
                    TableName = _table,
                    Key = UserKey(organizationId, userId),
                    UpdateExpression = "SET lastUsedStep = :step",
                    ConditionExpression = "attribute_exists(#pk) AND (attribute_not_exists(lastUsedStep) OR lastUsedStep < :step)",
                    ExpressionAttributeNames = new Dictionary<string, string> { ["#pk"] = Pk },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        [":step"] = new() { N = step.ToString(CultureInfo.InvariantCulture) },
                    },
                },
                cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }

    public async Task<int?> TryConsumeRecoveryCodeAsync(
        string organizationId, string userId, string codeHash, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await dynamo.UpdateItemAsync(
                new UpdateItemRequest
                {
                    TableName = _table,
                    Key = UserKey(organizationId, userId),
                    UpdateExpression = "DELETE recoveryCodes :set",
                    ConditionExpression = "contains(recoveryCodes, :code)",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        [":set"] = new() { SS = [codeHash] },
                        [":code"] = new(codeHash),
                    },
                    ReturnValues = ReturnValue.ALL_NEW,
                },
                cancellationToken).ConfigureAwait(false);

            return response.Attributes is { } attributes && attributes.TryGetValue("recoveryCodes", out var left) && left.SS is { } set
                ? set.Count
                : 0;
        }
        catch (ConditionalCheckFailedException)
        {
            return null;
        }
    }

    public async Task SetRecoveryCodesAsync(
        string organizationId, string userId, IReadOnlyList<string> codeHashes, CancellationToken cancellationToken = default)
    {
        var request = new UpdateItemRequest
        {
            TableName = _table,
            Key = UserKey(organizationId, userId),
            ConditionExpression = "attribute_exists(#pk)",
            ExpressionAttributeNames = new Dictionary<string, string> { ["#pk"] = Pk },
        };

        if (codeHashes.Count > 0)
        {
            request.UpdateExpression = "SET recoveryCodes = :codes";
            request.ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":codes"] = new() { SS = codeHashes.ToList() },
            };
        }
        else
        {
            request.UpdateExpression = "REMOVE recoveryCodes";
        }

        await dynamo.UpdateItemAsync(request, cancellationToken).ConfigureAwait(false);
    }

    // ── Challenges ───────────────────────────────────────────────────────────

    public Task SaveChallengeAsync(MfaChallenge challenge, CancellationToken cancellationToken = default)
        => dynamo.PutItemAsync(
            new PutItemRequest
            {
                TableName = _table,
                Item = new Dictionary<string, AttributeValue>
                {
                    [Pk] = new($"MFACHALLENGE#{challenge.TokenHash}"),
                    [Sk] = new("CHALLENGE"),
                    ["organizationId"] = new(challenge.OrganizationId),
                    ["userId"] = new(challenge.UserId),
                    ["purpose"] = new(challenge.Purpose.ToString()),
                    ["createdAt"] = new(challenge.CreatedAt.ToString("O", CultureInfo.InvariantCulture)),
                    ["expiresAtIso"] = new(challenge.ExpiresAt.ToString("O", CultureInfo.InvariantCulture)),
                    ["failedAttempts"] = new() { N = challenge.FailedAttempts.ToString(CultureInfo.InvariantCulture) },
                    // TTL is not punctual, which is why IsExpired is still checked on read.
                    [Ttl] = new() { N = challenge.ExpiresAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture) },
                },
            },
            cancellationToken);

    public async Task<MfaChallenge?> FindChallengeAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        var response = await dynamo.GetItemAsync(
            new GetItemRequest { TableName = _table, Key = ChallengeKey(tokenHash), ConsistentRead = true },
            cancellationToken).ConfigureAwait(false);

        if (!response.IsItemSet)
        {
            return null;
        }

        var item = response.Item;
        return new MfaChallenge(
            tokenHash,
            item["organizationId"].S,
            item["userId"].S,
            Enum.Parse<MfaChallengePurpose>(item["purpose"].S),
            DateTimeOffset.Parse(item["createdAt"].S, CultureInfo.InvariantCulture),
            DateTimeOffset.Parse(item["expiresAtIso"].S, CultureInfo.InvariantCulture),
            int.Parse(item["failedAttempts"].N, CultureInfo.InvariantCulture));
    }

    public async Task<int> RecordChallengeFailureAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await dynamo.UpdateItemAsync(
                new UpdateItemRequest
                {
                    TableName = _table,
                    Key = ChallengeKey(tokenHash),
                    UpdateExpression = "ADD failedAttempts :one",
                    ConditionExpression = "attribute_exists(#pk)",
                    ExpressionAttributeNames = new Dictionary<string, string> { ["#pk"] = Pk },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        [":one"] = new() { N = "1" },
                    },
                    ReturnValues = ReturnValue.UPDATED_NEW,
                },
                cancellationToken).ConfigureAwait(false);

            return int.Parse(response.Attributes["failedAttempts"].N, CultureInfo.InvariantCulture);
        }
        catch (ConditionalCheckFailedException)
        {
            // Already gone: as good as used up.
            return MfaChallenge.MaxFailedAttempts;
        }
    }

    public async Task<bool> TryDeleteChallengeAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        try
        {
            await dynamo.DeleteItemAsync(
                new DeleteItemRequest
                {
                    TableName = _table,
                    Key = ChallengeKey(tokenHash),
                    ConditionExpression = "attribute_exists(#pk)",
                    ExpressionAttributeNames = new Dictionary<string, string> { ["#pk"] = Pk },
                },
                cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }

    // ── Plumbing ─────────────────────────────────────────────────────────────

    private static Dictionary<string, AttributeValue> UserKey(string organizationId, string userId)
        => new() { [Pk] = new($"ORG#{organizationId}"), [Sk] = new($"MFA#{userId}") };

    private static Dictionary<string, AttributeValue> ChallengeKey(string tokenHash)
        => new() { [Pk] = new($"MFACHALLENGE#{tokenHash}"), [Sk] = new("CHALLENGE") };

    private static void Put(Dictionary<string, AttributeValue> item, string name, string? value)
    {
        if (value is not null)
        {
            item[name] = new AttributeValue(value);
        }
    }

    private static string? Optional(Dictionary<string, AttributeValue> item, string name)
        => item.TryGetValue(name, out var value) ? value.S : null;
}
