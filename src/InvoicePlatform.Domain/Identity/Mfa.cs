using System.Security.Cryptography;
using System.Text;

namespace InvoicePlatform.Domain.Identity;

/// <summary>
/// One user's two-step verification. Tenant-owned: it lives under the user's
/// organisation, like the account itself.
///
/// The secrets are held only in protected form (encrypted with a key the
/// database does not hold); this type never sees them in plain text.
/// </summary>
public sealed record UserMfa
{
    public required string OrganizationId { get; init; }

    public required string UserId { get; init; }

    /// <summary>The confirmed secret, protected. Null: MFA is off.</summary>
    public string? ProtectedSecret { get; init; }

    /// <summary>A secret shown during setup and not yet confirmed with a code, protected.</summary>
    public string? PendingProtectedSecret { get; init; }

    /// <summary>The last time step a code was accepted for; never accepted again (replay).</summary>
    public long? LastUsedStep { get; init; }

    /// <summary>SHA-256 of each unused recovery code (<see cref="RecoveryCodes.HashOf"/>).</summary>
    public IReadOnlyList<string> RecoveryCodeHashes { get; init; } = [];

    public DateTimeOffset? EnabledAt { get; init; }

    public bool IsEnabled => ProtectedSecret is not null;

    /// <summary>
    /// The owner's rule (2026-10-23): optional for customers, company admins
    /// included, so self sign-up runs to payment without an authenticator app;
    /// mandatory only for the platform administrator, who can reach every
    /// organisation.
    /// </summary>
    public static bool IsMandatoryFor(User user) => user.IsPlatformAdmin;

    /// <summary>What a secret is bound to when protected, so it cannot be moved to another account.</summary>
    public static string ProtectionContext(string organizationId, string userId) => $"{organizationId}/{userId}";
}

public enum MfaChallengePurpose
{
    /// <summary>The password was right and MFA is on: a code completes the login.</summary>
    SignIn,

    /// <summary>The password was right and MFA is mandatory (platform administrator) but not set up: setting it up completes the login.</summary>
    Setup,
}

/// <summary>
/// The half-way point of a login: the password was right, the second step is
/// still to come. It is not a session and opens nothing else. Only the hash of
/// its token is stored, like invitations.
/// </summary>
public sealed record MfaChallenge(
    string TokenHash,
    string OrganizationId,
    string UserId,
    MfaChallengePurpose Purpose,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    int FailedAttempts = 0)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    /// <summary>Setting up an authenticator for the first time takes longer than typing a code.</summary>
    public static readonly TimeSpan SetupLifetime = TimeSpan.FromMinutes(15);

    public static TimeSpan LifetimeFor(MfaChallengePurpose purpose)
        => purpose == MfaChallengePurpose.Setup ? SetupLifetime : Lifetime;

    /// <summary>After this many wrong codes the challenge dies and the login starts again.</summary>
    public const int MaxFailedAttempts = 5;

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public static string NewToken() => AuthSession.NewToken();

    public static string HashOf(string token) => Invitation.HashOf(token);
}

/// <summary>
/// Single-use codes for when the phone is not at hand. High-entropy and
/// random, so SHA-256 is enough: bcrypt's slowness protects guessable
/// passwords and buys nothing here.
/// </summary>
public static class RecoveryCodes
{
    public const int Count = 10;

    private const int Length = 12;

    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz234567";

    /// <summary>Ten codes like <c>k7qm-x2ab-9fde</c>: 60 bits each.</summary>
    public static IReadOnlyList<string> Generate()
    {
        var codes = new List<string>(Count);
        for (var i = 0; i < Count; i++)
        {
            var chars = new char[Length];
            for (var j = 0; j < Length; j++)
            {
                chars[j] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
            }

            var code = new string(chars);
            codes.Add($"{code[..4]}-{code[4..8]}-{code[8..]}");
        }

        return codes;
    }

    /// <summary>Case, spaces and dashes do not matter: people type these over from paper.</summary>
    public static string Normalize(string code)
        => new(code.Where(c => !char.IsWhiteSpace(c) && c != '-').Select(char.ToLowerInvariant).ToArray());

    public static string HashOf(string code)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(code)))).ToLowerInvariant();
}
