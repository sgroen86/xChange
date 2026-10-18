namespace InvoicePlatform.Domain.Identity;

/// <summary>
/// Someone who signed up and has not yet confirmed their email address.
/// Nothing exists for them until they do: no organisation, no account. Only
/// the SHA-256 of the confirmation code is stored, as for invitations, and the
/// password is already hashed.
/// </summary>
public sealed record PendingSignup(
    string CodeHash,
    string Email,
    string Name,
    string PasswordHash,
    string OrganizationName,
    string CountryCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt)
{
    /// <summary>A day: long enough to find the mail, short enough that a stray link dies.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;
}
