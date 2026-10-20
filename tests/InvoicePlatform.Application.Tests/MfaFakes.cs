using System.Security.Cryptography;
using InvoicePlatform.Application.Identity;
using InvoicePlatform.Domain.Identity;

namespace InvoicePlatform.Application.Tests;

/// <summary>MFA state and challenges in memory, with the same conditional behaviour as the DynamoDB store.</summary>
internal sealed class FakeMfaStore : IMfaStore
{
    private readonly Dictionary<(string Org, string User), UserMfa> _users = [];
    private readonly Dictionary<string, MfaChallenge> _challenges = [];

    public IReadOnlyDictionary<string, MfaChallenge> Challenges => _challenges;

    public Task<UserMfa?> GetAsync(string organizationId, string userId, CancellationToken cancellationToken = default)
        => Task.FromResult(_users.GetValueOrDefault((organizationId, userId)));

    public Task SaveAsync(UserMfa mfa, CancellationToken cancellationToken = default)
    {
        _users[(mfa.OrganizationId, mfa.UserId)] = mfa;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string organizationId, string userId, CancellationToken cancellationToken = default)
    {
        _users.Remove((organizationId, userId));
        return Task.CompletedTask;
    }

    public Task<bool> TryUseStepAsync(string organizationId, string userId, long step, CancellationToken cancellationToken = default)
    {
        if (!_users.TryGetValue((organizationId, userId), out var mfa) || mfa.LastUsedStep >= step)
        {
            return Task.FromResult(false);
        }

        _users[(organizationId, userId)] = mfa with { LastUsedStep = step };
        return Task.FromResult(true);
    }

    public Task<int?> TryConsumeRecoveryCodeAsync(string organizationId, string userId, string codeHash, CancellationToken cancellationToken = default)
    {
        if (!_users.TryGetValue((organizationId, userId), out var mfa) || !mfa.RecoveryCodeHashes.Contains(codeHash))
        {
            return Task.FromResult<int?>(null);
        }

        var left = mfa.RecoveryCodeHashes.Where(h => h != codeHash).ToList();
        _users[(organizationId, userId)] = mfa with { RecoveryCodeHashes = left };
        return Task.FromResult<int?>(left.Count);
    }

    public Task SetRecoveryCodesAsync(string organizationId, string userId, IReadOnlyList<string> codeHashes, CancellationToken cancellationToken = default)
    {
        _users[(organizationId, userId)] = _users[(organizationId, userId)] with { RecoveryCodeHashes = codeHashes };
        return Task.CompletedTask;
    }

    public Task SaveChallengeAsync(MfaChallenge challenge, CancellationToken cancellationToken = default)
    {
        _challenges[challenge.TokenHash] = challenge;
        return Task.CompletedTask;
    }

    public Task<MfaChallenge?> FindChallengeAsync(string tokenHash, CancellationToken cancellationToken = default)
        => Task.FromResult(_challenges.GetValueOrDefault(tokenHash));

    public Task<int> RecordChallengeFailureAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        if (!_challenges.TryGetValue(tokenHash, out var challenge))
        {
            return Task.FromResult(MfaChallenge.MaxFailedAttempts);
        }

        _challenges[tokenHash] = challenge with { FailedAttempts = challenge.FailedAttempts + 1 };
        return Task.FromResult(challenge.FailedAttempts + 1);
    }

    public Task<bool> TryDeleteChallengeAsync(string tokenHash, CancellationToken cancellationToken = default)
        => Task.FromResult(_challenges.Remove(tokenHash));
}

/// <summary>Stands in for KMS: reversible, but not plain text, and bound to its context like encryption context.</summary>
internal sealed class FakeProtector : ISecretProtector
{
    public Task<string> ProtectAsync(byte[] plaintext, string context, CancellationToken cancellationToken = default)
        => Task.FromResult($"{context}|{Convert.ToBase64String(plaintext.Select(b => (byte)(b ^ 0x5a)).ToArray())}");

    public Task<byte[]> UnprotectAsync(string protectedValue, string context, CancellationToken cancellationToken = default)
    {
        var parts = protectedValue.Split('|');
        if (parts[0] != context)
        {
            throw new CryptographicException("Wrong context.");
        }

        return Task.FromResult(Convert.FromBase64String(parts[1]).Select(b => (byte)(b ^ 0x5a)).ToArray());
    }
}

/// <summary>An authenticator app: the codes a phone would show for a secret given as base32.</summary>
internal static class Authenticator
{
    public static string CodeFor(string base32Secret, DateTimeOffset at, int stepOffset = 0)
        => Totp.CodeAt(Base32.Decode(base32Secret), Totp.StepAt(at) + stepOffset);
}
