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
///
/// Only the hash of the token is stored, like invitation codes and MFA
/// challenges: a copy of the table holds no session anyone can use.
/// </summary>
/// <param name="TokenHash">SHA-256 of the token (<see cref="HashOf"/>). The token itself is only ever held by the client.</param>
public sealed record AuthSession(
    string TokenHash,
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

    /// <summary>What the store keys a session by. A plain hash suffices: the token has 256 bits of entropy.</summary>
    public static string HashOf(string token) => Invitation.HashOf(token);
}

/// <summary>
/// Failed attempts counted under one key: a client address, an account, or
/// the recipient of sign-up mails.
///
/// Bookkeeping locks an IP out for 15 minutes after 5 failures
/// (fin_login_attempts). Same policy here: without it, an unauthenticated
/// endpoint on the public internet is an open invitation to guess passwords.
///
/// Per address alone is not enough: guesses spread over many addresses (a
/// botnet) would never trip it. So failures are also counted per account,
/// with a higher threshold - an account lock also locks out its owner, so it
/// should take more than a handful of requests to cause one.
/// </summary>
/// <param name="ClientKey">The address, or <see cref="AccountKey"/> / <see cref="SignupMailKey"/>.</param>
public sealed record LoginAttempts(
    string ClientKey,
    int FailedCount,
    DateTimeOffset? LockedUntil)
{
    /// <summary>Failures from one address before it is locked out.</summary>
    public const int MaxAttempts = 5;

    /// <summary>Failures for one account, from any number of addresses, before it is locked out.</summary>
    public const int MaxAccountAttempts = 10;

    /// <summary>Sign-up mails to one address before further sign-ups for it are refused for <see cref="SignupMailWindow"/>.</summary>
    public const int MaxSignupMails = 3;

    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public static readonly TimeSpan SignupMailWindow = TimeSpan.FromHours(1);

    public bool IsLocked(DateTimeOffset now) => LockedUntil is { } until && now < until;

    /// <summary>
    /// The key for failures against one account (password or second-step
    /// code). Built from the normalised email whether or not an account
    /// exists, so a lock never tells the two apart.
    /// </summary>
    public static string AccountKey(string normalizedEmail) => $"account:{normalizedEmail}";

    /// <summary>The key for sign-up mails sent to one address.</summary>
    public static string SignupMailKey(string normalizedEmail) => $"signup-mail:{normalizedEmail}";

    /// <summary>The record after one more failure: locked for <paramref name="lockFor"/> once it reaches <paramref name="max"/>.</summary>
    public static LoginAttempts AfterFailure(string key, LoginAttempts? existing, DateTimeOffset now, int max, TimeSpan lockFor)
    {
        var failed = (existing?.FailedCount ?? 0) + 1;
        return new LoginAttempts(key, failed, failed >= max ? now.Add(lockFor) : null);
    }
}
