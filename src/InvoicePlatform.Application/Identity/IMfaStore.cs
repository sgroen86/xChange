using InvoicePlatform.Domain.Identity;

namespace InvoicePlatform.Application.Identity;

/// <summary>
/// Two-step verification state per user, and the challenges of half-finished
/// logins. The conditional operations are what make codes single-use when two
/// requests arrive at once; a read-then-write would let both through.
/// </summary>
public interface IMfaStore
{
    Task<UserMfa?> GetAsync(string organizationId, string userId, CancellationToken cancellationToken = default);

    /// <summary>Replaces the whole record.</summary>
    Task SaveAsync(UserMfa mfa, CancellationToken cancellationToken = default);

    /// <summary>Turns MFA off for the user: secret, recovery codes and all.</summary>
    Task DeleteAsync(string organizationId, string userId, CancellationToken cancellationToken = default);

    /// <returns>False when this step, or a later one, was already used: a replay.</returns>
    Task<bool> TryUseStepAsync(string organizationId, string userId, long step, CancellationToken cancellationToken = default);

    /// <returns>How many recovery codes remain, or null when this one was not (or no longer) there.</returns>
    Task<int?> TryConsumeRecoveryCodeAsync(string organizationId, string userId, string codeHash, CancellationToken cancellationToken = default);

    /// <summary>Replaces only the recovery codes, leaving the last used step as it is.</summary>
    Task SetRecoveryCodesAsync(string organizationId, string userId, IReadOnlyList<string> codeHashes, CancellationToken cancellationToken = default);

    Task SaveChallengeAsync(MfaChallenge challenge, CancellationToken cancellationToken = default);

    Task<MfaChallenge?> FindChallengeAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <returns>The number of wrong codes so far, including this one.</returns>
    Task<int> RecordChallengeFailureAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <returns>False when it was already gone: used by a simultaneous request, or never there.</returns>
    Task<bool> TryDeleteChallengeAsync(string tokenHash, CancellationToken cancellationToken = default);
}

/// <summary>
/// Encrypts secrets that must be read back (TOTP secrets), with a key the
/// database does not hold. <paramref name="context"/> is bound to the
/// ciphertext: unprotecting with another context fails, so a protected value
/// copied to another account is useless.
/// </summary>
public interface ISecretProtector
{
    Task<string> ProtectAsync(byte[] plaintext, string context, CancellationToken cancellationToken = default);

    Task<byte[]> UnprotectAsync(string protectedValue, string context, CancellationToken cancellationToken = default);
}
