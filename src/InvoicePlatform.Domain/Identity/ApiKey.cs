using System.Security.Cryptography;
using System.Text;

namespace InvoicePlatform.Domain.Identity;

/// <summary>
/// A key another system (an ERP, a script) uses to send PDFs to the conversion
/// API on behalf of one organisation. Not a user: it opens the conversion
/// endpoints and nothing else.
///
/// The key itself is shown once, when it is made; only its SHA-256 is stored,
/// so a copy of the database holds no usable key. Tenant-owned:
/// <see cref="OrganizationId"/> comes with the key and is never taken from a
/// request.
/// </summary>
public sealed record ApiKey
{
    /// <summary>What every key starts with, so the API can tell a key from a session token.</summary>
    public const string Prefix = "xck_";

    /// <summary>How many characters of the key stay visible in the list ("xck_AbCdEfGh…").</summary>
    private const int VisibleLength = 12;

    public required string KeyId { get; init; }

    public required string OrganizationId { get; init; }

    /// <summary>What the admin called it, e.g. "Exact koppeling".</summary>
    public required string Name { get; init; }

    /// <summary>SHA-256 of the key, lower-case hex.</summary>
    public required string Hash { get; init; }

    /// <summary>The start of the key, to recognise it by; never enough to use it.</summary>
    public required string VisiblePrefix { get; init; }

    public required string CreatedByUserId { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? LastUsedAt { get; init; }

    /// <summary>"apikey:&lt;keyId&gt;": who booked an invoice this key's conversion produced.</summary>
    public string Actor => ActorFor(KeyId);

    public static string ActorFor(string keyId) => $"apikey:{keyId}";

    /// <summary>"xck_" + 32 bytes from the CSPRNG, base64url without padding.</summary>
    public static string NewSecret()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Prefix + Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static bool LooksLikeKey(string? token)
        => token is not null && token.StartsWith(Prefix, StringComparison.Ordinal);

    public static string HashOf(string secret)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret.Trim()))).ToLowerInvariant();

    public static string VisiblePrefixOf(string secret)
        => secret[..Math.Min(secret.Length, VisibleLength)];
}
