using System.Security.Cryptography;
using InvoicePlatform.Domain.Identity;

namespace InvoicePlatform.Application.Identity;

/// <summary>What an authenticator app needs: the secret as text and as the URI in the QR code.</summary>
public sealed record MfaSetupInfo(string Secret, string OtpAuthUri);

public sealed record MfaStatus(bool Enabled, bool Mandatory, int RecoveryCodesLeft);

/// <summary>Outcome of an MFA change outside the login itself. Never carries a secret except <see cref="Setup"/>, once.</summary>
public sealed record MfaResult(
    bool Succeeded,
    string? Error = null,
    MfaSetupInfo? Setup = null,
    IReadOnlyList<string>? RecoveryCodes = null,
    bool LockedOut = false,
    bool ChallengeGone = false,
    bool NotFound = false)
{
    public static MfaResult Fail(string error) => new(false, error);
}

/// <summary>
/// Two-step verification: the second step of a login, setting it up (during a
/// login when it is mandatory, or from the settings when it is not), turning it
/// off, new recovery codes, and the platform administrator's reset for someone
/// who lost their phone.
///
/// Every wrong code counts towards the same per-address lockout as a wrong
/// password: six digits are a million possibilities, and an unthrottled check
/// is guessed in minutes.
/// </summary>
public sealed class MfaService(
    IUserStore users,
    IMfaStore store,
    ISecretProtector protector,
    ILoginAttemptStore attempts,
    IClock clock,
    AuthService auth)
{
    /// <summary>The name authenticator apps show above the code.</summary>
    public const string Issuer = "xChange";

    // ── During login ─────────────────────────────────────────────────────────

    /// <summary>Completes a login with a code from the app or, failing that, a recovery code.</summary>
    public async Task<AuthResult> VerifyLoginAsync(
        string challengeToken,
        string? code,
        string? recoveryCode,
        string clientKey,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var record = await attempts.GetAsync(clientKey, cancellationToken).ConfigureAwait(false);
        if (record is not null && record.IsLocked(now))
        {
            return AuthResult.Locked();
        }

        var (challenge, user, failure) = await OpenChallengeAsync(
            challengeToken, MfaChallengePurpose.SignIn, clientKey, record, now, cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            return failure;
        }

        var state = await store.GetAsync(user!.OrganizationId, user.Id, cancellationToken).ConfigureAwait(false);
        if (state is not { IsEnabled: true })
        {
            // Reset by the platform administrator since the password was given.
            await store.TryDeleteChallengeAsync(challenge!.TokenHash, cancellationToken).ConfigureAwait(false);
            return AuthResult.Gone(IdentityText.Current.LoginExpired);
        }

        int? recoveryCodesLeft = null;
        bool accepted;
        if (!string.IsNullOrWhiteSpace(recoveryCode))
        {
            recoveryCodesLeft = await store.TryConsumeRecoveryCodeAsync(
                user.OrganizationId, user.Id, RecoveryCodes.HashOf(recoveryCode), cancellationToken).ConfigureAwait(false);
            accepted = recoveryCodesLeft is not null;
        }
        else
        {
            accepted = await TryTotpAsync(user, state.ProtectedSecret!, state.LastUsedStep, code, now, cancellationToken).ConfigureAwait(false);
        }

        if (!accepted)
        {
            return await WrongCodeAsync(challenge!, clientKey, record, now, cancellationToken).ConfigureAwait(false);
        }

        // Single use: of two requests with the same challenge, only one gets a session.
        if (!await store.TryDeleteChallengeAsync(challenge!.TokenHash, cancellationToken).ConfigureAwait(false))
        {
            return AuthResult.Gone(IdentityText.Current.LoginExpired);
        }

        var session = await auth.CompleteSignInAsync(user, clientKey, cancellationToken).ConfigureAwait(false);
        return session with { RecoveryCodesLeft = recoveryCodesLeft };
    }

    /// <summary>The secret for the setup a login requires. Calling it again replaces the secret (a new QR code).</summary>
    public async Task<MfaResult> StartLoginSetupAsync(
        string challengeToken,
        string clientKey,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var record = await attempts.GetAsync(clientKey, cancellationToken).ConfigureAwait(false);
        if (record is not null && record.IsLocked(now))
        {
            return new MfaResult(false, IdentityText.Current.TooManyAttempts, LockedOut: true);
        }

        var (challenge, user, failure) = await OpenChallengeAsync(
            challengeToken, MfaChallengePurpose.Setup, clientKey, record, now, cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            return new MfaResult(false, failure.Error, LockedOut: failure.LockedOut, ChallengeGone: failure.ChallengeGone);
        }

        var state = await store.GetAsync(user!.OrganizationId, user.Id, cancellationToken).ConfigureAwait(false);
        if (state is { IsEnabled: true })
        {
            await store.TryDeleteChallengeAsync(challenge!.TokenHash, cancellationToken).ConfigureAwait(false);
            return new MfaResult(false, IdentityText.Current.LoginExpired, ChallengeGone: true);
        }

        return new MfaResult(true, Setup: await BeginSetupAsync(user, state, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>The first code from the app: MFA is on, the recovery codes are handed out once, and the session follows.</summary>
    public async Task<AuthResult> ConfirmLoginSetupAsync(
        string challengeToken,
        string? code,
        string clientKey,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var record = await attempts.GetAsync(clientKey, cancellationToken).ConfigureAwait(false);
        if (record is not null && record.IsLocked(now))
        {
            return AuthResult.Locked();
        }

        var (challenge, user, failure) = await OpenChallengeAsync(
            challengeToken, MfaChallengePurpose.Setup, clientKey, record, now, cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            return failure;
        }

        var state = await store.GetAsync(user!.OrganizationId, user.Id, cancellationToken).ConfigureAwait(false);
        if (state is { IsEnabled: true })
        {
            await store.TryDeleteChallengeAsync(challenge!.TokenHash, cancellationToken).ConfigureAwait(false);
            return AuthResult.Gone(IdentityText.Current.LoginExpired);
        }

        if (state?.PendingProtectedSecret is null)
        {
            return AuthResult.Fail(IdentityText.Current.MfaSetupNotStarted);
        }

        var step = await VerifyPendingAsync(user, state.PendingProtectedSecret, code, now, cancellationToken).ConfigureAwait(false);
        if (step is null)
        {
            return await WrongCodeAsync(challenge!, clientKey, record, now, cancellationToken).ConfigureAwait(false);
        }

        if (!await store.TryDeleteChallengeAsync(challenge!.TokenHash, cancellationToken).ConfigureAwait(false))
        {
            return AuthResult.Gone(IdentityText.Current.LoginExpired);
        }

        var codes = await ActivateAsync(state, step.Value, now, cancellationToken).ConfigureAwait(false);
        var session = await auth.CompleteSignInAsync(user, clientKey, cancellationToken).ConfigureAwait(false);
        return session with { RecoveryCodes = codes };
    }

    // ── Signed in ────────────────────────────────────────────────────────────

    public async Task<MfaStatus?> GetStatusAsync(string organizationId, string userId, CancellationToken cancellationToken = default)
    {
        var user = await users.FindByIdAsync(organizationId, userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return null;
        }

        var state = await store.GetAsync(organizationId, userId, cancellationToken).ConfigureAwait(false);
        return new MfaStatus(
            state is { IsEnabled: true },
            UserMfa.IsMandatoryFor(user),
            state is { IsEnabled: true } ? state.RecoveryCodeHashes.Count : 0);
    }

    public async Task<MfaResult> StartAccountSetupAsync(string organizationId, string userId, CancellationToken cancellationToken = default)
    {
        var user = await users.FindByIdAsync(organizationId, userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return new MfaResult(false, NotFound: true);
        }

        var state = await store.GetAsync(organizationId, userId, cancellationToken).ConfigureAwait(false);
        if (state is { IsEnabled: true })
        {
            return MfaResult.Fail(IdentityText.Current.MfaAlreadyEnabled);
        }

        return new MfaResult(true, Setup: await BeginSetupAsync(user, state, cancellationToken).ConfigureAwait(false));
    }

    public async Task<MfaResult> ConfirmAccountSetupAsync(
        string organizationId,
        string userId,
        string? code,
        string clientKey,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var record = await attempts.GetAsync(clientKey, cancellationToken).ConfigureAwait(false);
        if (record is not null && record.IsLocked(now))
        {
            return new MfaResult(false, IdentityText.Current.TooManyAttempts, LockedOut: true);
        }

        var user = await users.FindByIdAsync(organizationId, userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return new MfaResult(false, NotFound: true);
        }

        var state = await store.GetAsync(organizationId, userId, cancellationToken).ConfigureAwait(false);
        if (state is { IsEnabled: true })
        {
            return MfaResult.Fail(IdentityText.Current.MfaAlreadyEnabled);
        }

        if (state?.PendingProtectedSecret is null)
        {
            return MfaResult.Fail(IdentityText.Current.MfaSetupNotStarted);
        }

        var step = await VerifyPendingAsync(user, state.PendingProtectedSecret, code, now, cancellationToken).ConfigureAwait(false);
        if (step is null)
        {
            return await WrongAccountCodeAsync(clientKey, record, now, cancellationToken).ConfigureAwait(false);
        }

        return new MfaResult(true, RecoveryCodes: await ActivateAsync(state, step.Value, now, cancellationToken).ConfigureAwait(false));
    }

    /// <param name="code">A code from the app or a recovery code: someone who lost the phone can still turn it off.</param>
    public async Task<MfaResult> DisableAsync(
        string organizationId,
        string userId,
        string? code,
        string clientKey,
        CancellationToken cancellationToken = default)
    {
        var failure = await OpenAccountChangeAsync(organizationId, userId, code, clientKey, refuseWhenMandatory: true, cancellationToken)
            .ConfigureAwait(false);
        if (failure is not null)
        {
            return failure;
        }

        await store.DeleteAsync(organizationId, userId, cancellationToken).ConfigureAwait(false);
        return new MfaResult(true);
    }

    /// <summary>A new set of ten; the old codes stop working.</summary>
    public async Task<MfaResult> RegenerateRecoveryCodesAsync(
        string organizationId,
        string userId,
        string? code,
        string clientKey,
        CancellationToken cancellationToken = default)
    {
        var failure = await OpenAccountChangeAsync(organizationId, userId, code, clientKey, refuseWhenMandatory: false, cancellationToken)
            .ConfigureAwait(false);
        if (failure is not null)
        {
            return failure;
        }

        var codes = RecoveryCodes.Generate();
        await store.SetRecoveryCodesAsync(organizationId, userId, codes.Select(RecoveryCodes.HashOf).ToList(), cancellationToken)
            .ConfigureAwait(false);
        return new MfaResult(true, RecoveryCodes: codes);
    }

    // ── Platform ─────────────────────────────────────────────────────────────

    /// <summary>
    /// For someone who lost both phone and recovery codes. MFA is off afterwards;
    /// where it is mandatory, the next login sets it up again.
    /// </summary>
    public async Task<MfaResult> ResetAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = string.IsNullOrWhiteSpace(email)
            ? null
            : await users.FindByEmailAsync(User.NormalizeEmail(email), cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return new MfaResult(false, IdentityText.Current.UserNotFound, NotFound: true);
        }

        await store.DeleteAsync(user.OrganizationId, user.Id, cancellationToken).ConfigureAwait(false);
        return new MfaResult(true);
    }

    // ── Plumbing ─────────────────────────────────────────────────────────────

    private async Task<(MfaChallenge? Challenge, User? User, AuthResult? Failure)> OpenChallengeAsync(
        string challengeToken,
        MfaChallengePurpose purpose,
        string clientKey,
        LoginAttempts? record,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var challenge = string.IsNullOrWhiteSpace(challengeToken)
            ? null
            : await store.FindChallengeAsync(MfaChallenge.HashOf(challengeToken), cancellationToken).ConfigureAwait(false);

        if (challenge is null)
        {
            // Unknown tokens count like wrong passwords; an expired one does not, its owner just waited.
            await auth.RecordAttemptAsync(clientKey, record, now, cancellationToken).ConfigureAwait(false);
            return (null, null, AuthResult.Gone(IdentityText.Current.LoginExpired));
        }

        if (challenge.IsExpired(now) || challenge.Purpose != purpose)
        {
            return (null, null, AuthResult.Gone(IdentityText.Current.LoginExpired));
        }

        var user = await users.FindByIdAsync(challenge.OrganizationId, challenge.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null || !user.IsActive)
        {
            await store.TryDeleteChallengeAsync(challenge.TokenHash, cancellationToken).ConfigureAwait(false);
            return (null, null, AuthResult.Gone(user is null ? IdentityText.Current.LoginExpired : IdentityText.Current.AccountDeactivated));
        }

        return (challenge, user, null);
    }

    private async Task<AuthResult> WrongCodeAsync(
        MfaChallenge challenge,
        string clientKey,
        LoginAttempts? record,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await auth.RecordAttemptAsync(clientKey, record, now, cancellationToken).ConfigureAwait(false);

        var failures = await store.RecordChallengeFailureAsync(challenge.TokenHash, cancellationToken).ConfigureAwait(false);
        if (failures >= MfaChallenge.MaxFailedAttempts)
        {
            await store.TryDeleteChallengeAsync(challenge.TokenHash, cancellationToken).ConfigureAwait(false);
            return AuthResult.Gone(IdentityText.Current.TooManyWrongCodes);
        }

        return (record?.FailedCount ?? 0) + 1 >= LoginAttempts.MaxAttempts
            ? AuthResult.Locked()
            : AuthResult.Fail(IdentityText.Current.InvalidCode);
    }

    private async Task<MfaResult> WrongAccountCodeAsync(
        string clientKey,
        LoginAttempts? record,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await auth.RecordAttemptAsync(clientKey, record, now, cancellationToken).ConfigureAwait(false);
        return (record?.FailedCount ?? 0) + 1 >= LoginAttempts.MaxAttempts
            ? new MfaResult(false, IdentityText.Current.TooManyAttempts, LockedOut: true)
            : MfaResult.Fail(IdentityText.Current.InvalidCode);
    }

    /// <summary>The checks shared by turning MFA off and new recovery codes: on, not locked out, and a valid code. Null when all pass.</summary>
    private async Task<MfaResult?> OpenAccountChangeAsync(
        string organizationId,
        string userId,
        string? code,
        string clientKey,
        bool refuseWhenMandatory,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var record = await attempts.GetAsync(clientKey, cancellationToken).ConfigureAwait(false);
        if (record is not null && record.IsLocked(now))
        {
            return new MfaResult(false, IdentityText.Current.TooManyAttempts, LockedOut: true);
        }

        var user = await users.FindByIdAsync(organizationId, userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return new MfaResult(false, NotFound: true);
        }

        // Refused before the code is checked, so a recovery code is not used up for nothing.
        if (refuseWhenMandatory && UserMfa.IsMandatoryFor(user))
        {
            return MfaResult.Fail(IdentityText.Current.MfaMandatory);
        }

        var state = await store.GetAsync(organizationId, userId, cancellationToken).ConfigureAwait(false);
        if (state is not { IsEnabled: true })
        {
            return MfaResult.Fail(IdentityText.Current.MfaNotEnabled);
        }

        var accepted = Totp.Normalize(code) is not null
            ? await TryTotpAsync(user, state.ProtectedSecret!, state.LastUsedStep, code, now, cancellationToken).ConfigureAwait(false)
            : !string.IsNullOrWhiteSpace(code)
                && await store.TryConsumeRecoveryCodeAsync(organizationId, userId, RecoveryCodes.HashOf(code), cancellationToken).ConfigureAwait(false) is not null;

        return accepted ? null : await WrongAccountCodeAsync(clientKey, record, now, cancellationToken).ConfigureAwait(false);
    }

    private async Task<MfaSetupInfo> BeginSetupAsync(User user, UserMfa? state, CancellationToken cancellationToken)
    {
        var secret = Totp.NewSecret();
        try
        {
            var protectedSecret = await protector.ProtectAsync(
                secret, UserMfa.ProtectionContext(user.OrganizationId, user.Id), cancellationToken).ConfigureAwait(false);

            await store.SaveAsync(
                (state ?? new UserMfa { OrganizationId = user.OrganizationId, UserId = user.Id })
                    with { PendingProtectedSecret = protectedSecret },
                cancellationToken).ConfigureAwait(false);

            return new MfaSetupInfo(Base32.Encode(secret), Totp.OtpAuthUri(Issuer, user.Email, secret));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    /// <returns>The step the code belongs to, or null when it is wrong.</returns>
    private async Task<long?> VerifyPendingAsync(
        User user, string pendingProtectedSecret, string? code, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var secret = await protector.UnprotectAsync(
            pendingProtectedSecret, UserMfa.ProtectionContext(user.OrganizationId, user.Id), cancellationToken).ConfigureAwait(false);
        try
        {
            return Totp.Verify(secret, code, now, lastUsedStep: null);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    private async Task<bool> TryTotpAsync(
        User user, string protectedSecret, long? lastUsedStep, string? code, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var secret = await protector.UnprotectAsync(
            protectedSecret, UserMfa.ProtectionContext(user.OrganizationId, user.Id), cancellationToken).ConfigureAwait(false);
        long? step;
        try
        {
            step = Totp.Verify(secret, code, now, lastUsedStep);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }

        // The conditional write is the real replay check: the read above may be stale.
        return step is { } matched
            && await store.TryUseStepAsync(user.OrganizationId, user.Id, matched, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The pending secret becomes the secret; ten recovery codes are made and returned once.</summary>
    private async Task<IReadOnlyList<string>> ActivateAsync(UserMfa state, long step, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var codes = RecoveryCodes.Generate();
        await store.SaveAsync(
            state with
            {
                ProtectedSecret = state.PendingProtectedSecret,
                PendingProtectedSecret = null,
                LastUsedStep = step,
                RecoveryCodeHashes = codes.Select(RecoveryCodes.HashOf).ToList(),
                EnabledAt = now,
            },
            cancellationToken).ConfigureAwait(false);
        return codes;
    }
}
