using InvoicePlatform.Domain.Identity;

namespace InvoicePlatform.Application.Identity;

/// <summary>API keys, found by hash (to authenticate) and listed per organisation (to manage).</summary>
public interface IApiKeyStore
{
    Task SaveAsync(ApiKey key, CancellationToken cancellationToken = default);

    Task<ApiKey?> FindByHashAsync(string hash, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApiKey>> ListAsync(string organizationId, CancellationToken cancellationToken = default);

    /// <summary>Records a use. Does nothing for a key that was revoked in the meantime.</summary>
    Task TouchAsync(ApiKey key, DateTimeOffset at, CancellationToken cancellationToken = default);

    Task DeleteAsync(ApiKey key, CancellationToken cancellationToken = default);
}

/// <summary>Who is calling with an API key: the organisation and the key, never a user.</summary>
public sealed record ApiKeyCaller(string OrganizationId, string KeyId)
{
    /// <summary>"apikey:&lt;keyId&gt;", recorded where a user id would be.</summary>
    public string Actor => ApiKey.ActorFor(KeyId);
}

/// <summary>A key just made: the secret is in here once and nowhere else, ever.</summary>
public sealed record CreatedApiKey(ApiKey Key, string Secret);

/// <summary>
/// Making, listing, revoking and recognising API keys. An admin manages them in
/// the app; the conversion API accepts them.
/// </summary>
public sealed class ApiKeyService(IApiKeyStore keys, IClock clock)
{
    public const int MaxNameLength = 80;

    /// <summary>A key's last use is written at most this often, not on every poll.</summary>
    private static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(5);

    /// <returns>Null when the name is empty or too long.</returns>
    public async Task<CreatedApiKey?> CreateAsync(
        string organizationId,
        string userId,
        string name,
        CancellationToken cancellationToken = default)
    {
        name = name?.Trim() ?? "";
        if (name.Length == 0 || name.Length > MaxNameLength)
        {
            return null;
        }

        var secret = ApiKey.NewSecret();
        var key = new ApiKey
        {
            KeyId = Guid.NewGuid().ToString("n"),
            OrganizationId = organizationId,
            Name = name,
            Hash = ApiKey.HashOf(secret),
            VisiblePrefix = ApiKey.VisiblePrefixOf(secret),
            CreatedByUserId = userId,
            CreatedAt = clock.UtcNow,
        };

        await keys.SaveAsync(key, cancellationToken).ConfigureAwait(false);
        return new CreatedApiKey(key, secret);
    }

    public async Task<IReadOnlyList<ApiKey>> ListAsync(string organizationId, CancellationToken cancellationToken = default)
        => (await keys.ListAsync(organizationId, cancellationToken).ConfigureAwait(false))
            .OrderBy(k => k.CreatedAt)
            .ToList();

    /// <summary>Deletes the key: it stops working at once.</summary>
    /// <returns>False when the organisation has no such key.</returns>
    public async Task<bool> RevokeAsync(string organizationId, string keyId, CancellationToken cancellationToken = default)
    {
        var key = (await keys.ListAsync(organizationId, cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(k => k.KeyId == keyId);
        if (key is null)
        {
            return false;
        }

        await keys.DeleteAsync(key, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>Resolves a bearer token to the key's organisation, or null if it is not a usable key.</summary>
    public async Task<ApiKeyCaller?> AuthenticateAsync(string token, CancellationToken cancellationToken = default)
    {
        if (!ApiKey.LooksLikeKey(token))
        {
            return null;
        }

        var key = await keys.FindByHashAsync(ApiKey.HashOf(token), cancellationToken).ConfigureAwait(false);
        if (key is null)
        {
            return null;
        }

        var now = clock.UtcNow;
        if (key.LastUsedAt is not { } last || now - last >= TouchInterval)
        {
            await keys.TouchAsync(key, now, cancellationToken).ConfigureAwait(false);
        }

        return new ApiKeyCaller(key.OrganizationId, key.KeyId);
    }
}
