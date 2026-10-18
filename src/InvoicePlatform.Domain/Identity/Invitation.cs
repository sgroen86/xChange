using System.Security.Cryptography;
using System.Text;

namespace InvoicePlatform.Domain.Identity;

/// <summary>
/// A one-off invitation for a user to set up their own account in an
/// organisation. The code travels in a link; only its hash is stored, so a copy
/// of the database contains no usable invitation.
/// </summary>
/// <param name="CodeHash">SHA-256 of the code, lower-case hex.</param>
public sealed record Invitation(
    string CodeHash,
    string OrganizationId,
    string Email,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    /// <summary>32 bytes from the CSPRNG, hex: unguessable, like a session token.</summary>
    public static string NewCode()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string HashOf(string code)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim()))).ToLowerInvariant();
}
