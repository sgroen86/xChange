using System.Security.Cryptography;

namespace InvoicePlatform.Domain.Identity;

/// <summary>
/// A logged-in session.
///
/// Deliberately a server-side session rather than a self-contained token,
/// because that is what Bookkeeping does: PHP holds the session and the cookie
/// carries only an opaque id. The same property matters here - logging out, or
/// deactivating an account, takes effect immediately, where a signed token
/// stays valid until it expires no matter what the server decides.
///
/// The frontend is a static site on one origin calling an API on another, so
/// the id travels as a bearer token rather than a cookie: a SameSite=Strict
/// cookie is never sent cross-site, and relaxing that to None invites the
/// browser's third-party cookie rules into the design.
/// </summary>
/// <param name="Token">Opaque, high-entropy. The only thing the client holds.</param>
public sealed record AuthSession(
    string Token,
    string UserId,
    string OrganizationId,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt)
{
    /// <summary>Matches Bookkeeping's SESSION_LIFETIME intent: a working day.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(8);

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    /// <summary>
    /// 32 bytes from the CSPRNG, hex encoded. Guessing one is not a realistic
    /// attack, which is what lets the token stand in for a password.
    /// </summary>
    public static string NewToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

/// <summary>
/// Failed login tracking for one client address.
///
/// Bookkeeping locks an IP out for 15 minutes after 5 failures
/// (fin_login_attempts). Same policy here: without it, an unauthenticated
/// endpoint on the public internet is an open invitation to guess passwords.
/// </summary>
public sealed record LoginAttempts(
    string ClientKey,
    int FailedCount,
    DateTimeOffset? LockedUntil)
{
    public const int MaxAttempts = 5;

    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public bool IsLocked(DateTimeOffset now) => LockedUntil is { } until && now < until;
}
