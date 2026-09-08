using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using InvoicePlatform.Application.Identity;
using InvoicePlatform.Domain.Identity;
using Microsoft.Extensions.Options;

namespace InvoicePlatform.Infrastructure.Identity;

/// <summary>
/// Users, sessions and login attempts in one DynamoDB table.
///
/// DynamoDB rather than the PostgreSQL that CLAUDE.md specifies, and that is a
/// deliberate, temporary deviation: its 25 GB and 25 read/write units per month
/// are a perpetual free tier, where the smallest RDS instance is not free at
/// all. It also suits a Lambda, which has no connection pool to keep warm.
/// Move to PostgreSQL when the database lands; the store interfaces exist so
/// that swap touches this file only.
///
/// One table with a composite key, because three tables would be three things
/// to provision for no benefit at this size:
///
///   USER#&lt;email&gt;   / PROFILE          the account, found by login
///   ORG#&lt;org&gt;      / USER#&lt;id&gt;        the same account, found by id
///   SESSION#&lt;tok&gt;  / SESSION          expires by TTL
///   ATTEMPT#&lt;ip&gt;   / ATTEMPT          expires by TTL
///
/// The account is written twice on purpose: login looks up by email, and every
/// authenticated request looks up by organization and id. Storing both avoids a
/// secondary index, and the pair is only written when an account is created.
/// </summary>
internal sealed class DynamoDbIdentityStore(
    IAmazonDynamoDB dynamo,
    IOptions<IdentityOptions> options)
    : IUserStore, ISessionStore, ILoginAttemptStore
{
    private const string Pk = "pk";
    private const string Sk = "sk";
    private const string Ttl = "expiresAt";

    private readonly string _table = options.Value.TableName;

    // ── Users ────────────────────────────────────────────────────────────────

    public async Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var item = await GetItemAsync($"USER#{User.NormalizeEmail(email)}", "PROFILE", cancellationToken)
            .ConfigureAwait(false);
        return item is null ? null : ToUser(item);
    }

    public async Task<User?> FindByIdAsync(
        string organizationId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        // Scoped by organization, per CLAUDE.md: a lookup by id alone would let
        // an id from one tenant resolve against another.
        var item = await GetItemAsync($"ORG#{organizationId}", $"USER#{userId}", cancellationToken)
            .ConfigureAwait(false);
        return item is null ? null : ToUser(item);
    }

    public async Task<bool> AnyUsersExistAsync(CancellationToken cancellationToken = default)
    {
        // Only ever asked during first-run setup, and it stops at the first hit.
        var response = await dynamo.ScanAsync(
            new ScanRequest
            {
                TableName = _table,
                FilterExpression = "begins_with(#pk, :prefix)",
                ExpressionAttributeNames = new Dictionary<string, string> { ["#pk"] = Pk },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":prefix"] = new("USER#"),
                },
                Limit = 1,
            },
            cancellationToken).ConfigureAwait(false);

        return response.Items.Count > 0;
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        var byEmail = ToItem(user, $"USER#{user.Email}", "PROFILE");
        var byId = ToItem(user, $"ORG#{user.OrganizationId}", $"USER#{user.Id}");

        try
        {
            // Conditional so a second signup with the same address fails rather
            // than silently replacing the first.
            await dynamo.PutItemAsync(
                new PutItemRequest
                {
                    TableName = _table,
                    Item = byEmail,
                    ConditionExpression = "attribute_not_exists(#pk)",
                    ExpressionAttributeNames = new Dictionary<string, string> { ["#pk"] = Pk },
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (ConditionalCheckFailedException)
        {
            throw new InvalidOperationException($"An account already exists for {user.Email}.");
        }

        await dynamo.PutItemAsync(
            new PutItemRequest { TableName = _table, Item = byId },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateLastLoginAsync(
        string organizationId,
        string userId,
        DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        var user = await FindByIdAsync(organizationId, userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return;
        }

        var updated = user with { LastLoginAt = at };

        await dynamo.PutItemAsync(
            new PutItemRequest
            {
                TableName = _table,
                Item = ToItem(updated, $"ORG#{organizationId}", $"USER#{userId}"),
            },
            cancellationToken).ConfigureAwait(false);

        await dynamo.PutItemAsync(
            new PutItemRequest
            {
                TableName = _table,
                Item = ToItem(updated, $"USER#{updated.Email}", "PROFILE"),
            },
            cancellationToken).ConfigureAwait(false);
    }

    // ── Sessions ─────────────────────────────────────────────────────────────

    public Task SaveAsync(AuthSession session, CancellationToken cancellationToken = default)
        => dynamo.PutItemAsync(
            new PutItemRequest
            {
                TableName = _table,
                Item = new Dictionary<string, AttributeValue>
                {
                    [Pk] = new($"SESSION#{session.Token}"),
                    [Sk] = new("SESSION"),
                    ["userId"] = new(session.UserId),
                    ["organizationId"] = new(session.OrganizationId),
                    ["createdAt"] = new(session.CreatedAt.ToString("O")),
                    ["expiresAtIso"] = new(session.ExpiresAt.ToString("O")),
                    // DynamoDB TTL reaps expired sessions for us. It is not
                    // punctual, which is why IsExpired is still checked on read.
                    [Ttl] = new AttributeValue { N = session.ExpiresAt.ToUnixTimeSeconds().ToString() },
                },
            },
            cancellationToken);

    public async Task<AuthSession?> FindAsync(string token, CancellationToken cancellationToken = default)
    {
        var item = await GetItemAsync($"SESSION#{token}", "SESSION", cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return null;
        }

        return new AuthSession(
            token,
            item["userId"].S,
            item["organizationId"].S,
            DateTimeOffset.Parse(item["createdAt"].S),
            DateTimeOffset.Parse(item["expiresAtIso"].S));
    }

    public Task DeleteAsync(string token, CancellationToken cancellationToken = default)
        => dynamo.DeleteItemAsync(
            new DeleteItemRequest
            {
                TableName = _table,
                Key = Key($"SESSION#{token}", "SESSION"),
            },
            cancellationToken);

    // ── Login attempts ───────────────────────────────────────────────────────

    public async Task<LoginAttempts?> GetAsync(string clientKey, CancellationToken cancellationToken = default)
    {
        var item = await GetItemAsync($"ATTEMPT#{clientKey}", "ATTEMPT", cancellationToken)
            .ConfigureAwait(false);

        if (item is null)
        {
            return null;
        }

        DateTimeOffset? lockedUntil = item.TryGetValue("lockedUntil", out var locked) && locked.S is { } s
            ? DateTimeOffset.Parse(s)
            : null;

        return new LoginAttempts(clientKey, int.Parse(item["failedCount"].N), lockedUntil);
    }

    public Task SaveAsync(LoginAttempts attempts, CancellationToken cancellationToken = default)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            [Pk] = new($"ATTEMPT#{attempts.ClientKey}"),
            [Sk] = new("ATTEMPT"),
            ["failedCount"] = new AttributeValue { N = attempts.FailedCount.ToString() },
            // Counters expire on their own, so a single mistyped password does
            // not count against someone an hour later.
            [Ttl] = new AttributeValue
            {
                N = DateTimeOffset.UtcNow.Add(LoginAttempts.LockoutDuration * 2).ToUnixTimeSeconds().ToString(),
            },
        };

        if (attempts.LockedUntil is { } until)
        {
            item["lockedUntil"] = new AttributeValue(until.ToString("O"));
        }

        return dynamo.PutItemAsync(new PutItemRequest { TableName = _table, Item = item }, cancellationToken);
    }

    public Task ClearAsync(string clientKey, CancellationToken cancellationToken = default)
        => dynamo.DeleteItemAsync(
            new DeleteItemRequest
            {
                TableName = _table,
                Key = Key($"ATTEMPT#{clientKey}", "ATTEMPT"),
            },
            cancellationToken);

    // ── Plumbing ─────────────────────────────────────────────────────────────

    private static Dictionary<string, AttributeValue> Key(string pk, string sk)
        => new() { [Pk] = new(pk), [Sk] = new(sk) };

    private async Task<Dictionary<string, AttributeValue>?> GetItemAsync(
        string pk,
        string sk,
        CancellationToken cancellationToken)
    {
        var response = await dynamo.GetItemAsync(
            new GetItemRequest { TableName = _table, Key = Key(pk, sk) },
            cancellationToken).ConfigureAwait(false);

        return response.IsItemSet ? response.Item : null;
    }

    private static Dictionary<string, AttributeValue> ToItem(User user, string pk, string sk) => new()
    {
        [Pk] = new(pk),
        [Sk] = new(sk),
        ["id"] = new(user.Id),
        ["organizationId"] = new(user.OrganizationId),
        ["email"] = new(user.Email),
        ["passwordHash"] = new(user.PasswordHash),
        ["name"] = new(user.Name),
        ["role"] = new(user.Role.ToString()),
        ["isActive"] = new AttributeValue { BOOL = user.IsActive },
        ["createdAt"] = new(user.CreatedAt.ToString("O")),
        ["lastLoginAt"] = user.LastLoginAt is { } last
            ? new AttributeValue(last.ToString("O"))
            : new AttributeValue { NULL = true },
    };

    private static User ToUser(Dictionary<string, AttributeValue> item) => new()
    {
        Id = item["id"].S,
        OrganizationId = item["organizationId"].S,
        Email = item["email"].S,
        PasswordHash = item["passwordHash"].S,
        Name = item["name"].S,
        Role = Enum.TryParse<UserRole>(item["role"].S, out var role) ? role : UserRole.User,
        IsActive = item["isActive"].BOOL ?? false,
        CreatedAt = DateTimeOffset.Parse(item["createdAt"].S),
        LastLoginAt = item.TryGetValue("lastLoginAt", out var last) && last.S is { } value
            ? DateTimeOffset.Parse(value)
            : null,
    };
}

public sealed class IdentityOptions
{
    public const string SectionName = "Identity";

    public string TableName { get; set; } = "xchange-identity";
}
