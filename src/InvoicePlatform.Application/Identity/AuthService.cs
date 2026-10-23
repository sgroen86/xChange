using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Domain.Identity;
using InvoicePlatform.Domain.Organizations;

namespace InvoicePlatform.Application.Identity;

/// <summary>Outcome of a login or setup attempt. Never carries the hash.</summary>
/// <param name="LockedOut">Refused because the address or the account failed too often: the caller should wait, whatever the message says.</param>
/// <param name="Mfa">
/// Set instead of a session when the password was right and a second step
/// must follow: a code, or setting MFA up. Nothing about the user is returned
/// until that step is done.
/// </param>
/// <param name="RecoveryCodesLeft">After a login with a recovery code: how many remain.</param>
/// <param name="RecoveryCodes">After setting MFA up: the codes, shown once and never stored in plain text.</param>
/// <param name="ChallengeGone">The challenge expired, was used up or never existed: the login must start again.</param>
public sealed record AuthResult(
    bool Succeeded,
    string? Error = null,
    string? Token = null,
    DateTimeOffset? ExpiresAt = null,
    AuthenticatedUser? User = null,
    bool LockedOut = false,
    MfaPending? Mfa = null,
    int? RecoveryCodesLeft = null,
    IReadOnlyList<string>? RecoveryCodes = null,
    bool ChallengeGone = false)
{
    public static AuthResult Fail(string error) => new(false, error);

    public static AuthResult Locked() => new(false, IdentityText.Current.TooManyAttempts, LockedOut: true);

    public static AuthResult AccountLocked() => new(false, IdentityText.Current.AccountLocked, LockedOut: true);

    public static AuthResult Gone(string error) => new(false, error, ChallengeGone: true);
}

/// <summary>The second step a login still needs.</summary>
/// <param name="Challenge">Opaque, single-use, short-lived; only its hash is stored.</param>
/// <param name="SetupRequired">MFA is mandatory for this user and not set up yet.</param>
public sealed record MfaPending(string Challenge, bool SetupRequired, DateTimeOffset ExpiresAt);

/// <summary>What the API may safely tell the browser about the signed-in user.</summary>
public sealed record AuthenticatedUser(
    string Id,
    string OrganizationId,
    string Email,
    string Name,
    UserRole Role,
    bool IsPlatformAdmin = false);

/// <summary>What an invitation link shows before the account exists. Never carries the code.</summary>
public sealed record InvitationInfo(string OrganizationName, string CountryCode, string Email, string Name);

/// <param name="Invitation">Null when the code cannot be used; Error says why.</param>
public sealed record InvitationLookup(InvitationInfo? Invitation, string? Error);

/// <summary>
/// Login, logout and session validation.
///
/// Behaviour follows Bookkeeping's Auth class deliberately - per-address
/// lockout after repeated failures, an inactive account refused separately from
/// a wrong password, last-login recorded on success, and the same Dutch
/// messages - so the two applications behave the same way even though they hold
/// separate accounts. On top of that, failures are counted per account
/// (<see cref="LoginAttempts.MaxAccountAttempts"/>), which Bookkeeping does not.
/// </summary>
public sealed class AuthService(
    IUserStore users,
    ISessionStore sessions,
    ILoginAttemptStore attempts,
    IPasswordHasher passwords,
    IClock clock,
    IPlatformStore platform,
    IMfaStore mfa)
{
    /// <summary>
    /// Creates the first account and its organization.
    ///
    /// Mirrors Bookkeeping's setup.php: it works only while no user exists, so
    /// it closes itself permanently the moment the first account is made. The
    /// endpoint is public by necessity - there is nobody to authenticate as yet.
    ///
    /// The first account is also the platform administrator: on a new
    /// installation there is nobody else to create organisations.
    /// </summary>
    public async Task<AuthResult> SetupFirstUserAsync(
        string email,
        string password,
        string name,
        string organizationName,
        string countryCode,
        CancellationToken cancellationToken = default)
    {
        if (await users.AnyUsersExistAsync(cancellationToken).ConfigureAwait(false))
        {
            return AuthResult.Fail(IdentityText.Current.AccountExists);
        }

        var validation = ValidateCredentials(email, password, name);
        if (validation is not null)
        {
            return AuthResult.Fail(validation);
        }

        if (string.IsNullOrWhiteSpace(organizationName))
        {
            return AuthResult.Fail(IdentityText.Current.OrganizationNameRequired);
        }

        if (!Countries.IsSupported(countryCode))
        {
            return AuthResult.Fail(IdentityText.Current.ChooseCountry);
        }

        var now = clock.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid().ToString("n"),
            // The first user founds the organization and owns it, exactly as
            // Bookkeeping's first user creates the first company.
            OrganizationId = Guid.NewGuid().ToString("n"),
            Email = User.NormalizeEmail(email),
            PasswordHash = passwords.Hash(password),
            Name = name.Trim(),
            Role = UserRole.Admin,
            IsActive = true,
            IsPlatformAdmin = true,
            CreatedAt = now,
        };

        try
        {
            await users.AddAsync(user, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // The store refused a duplicate. Reaching here means the existence
            // check above was wrong, so say something useful rather than
            // letting an unhandled exception become a 500.
            return AuthResult.Fail(IdentityText.Current.AccountExists);
        }

        await platform.SaveOrganizationAsync(
            new Organization
            {
                OrganizationId = user.OrganizationId,
                Name = organizationName.Trim(),
                CountryCode = Countries.Find(countryCode)!.Code,
                CreatedAt = now,
                CreatedByUserId = user.Id,
            },
            cancellationToken).ConfigureAwait(false);

        // A platform administrator: MFA is set up before the first session.
        return await ContinueSignInAsync(user, clientKey: null, cancellationToken).ConfigureAwait(false);
    }

    /// <param name="clientKey">
    /// Identifies the caller for rate limiting - the client IP. Failures are
    /// counted per address, so guessing many usernames from one place is
    /// throttled, and per account, so guessing one password from many places
    /// is throttled too.
    /// </param>
    public async Task<AuthResult> LoginAsync(
        string email,
        string password,
        string clientKey,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        var record = await attempts.GetAsync(clientKey, cancellationToken).ConfigureAwait(false);
        if (record is not null && record.IsLocked(now))
        {
            return AuthResult.Locked();
        }

        // Keyed by the address as typed (normalised), whether or not an account
        // has it: an unknown address locks exactly like a known one.
        var address = User.NormalizeEmail(email);
        var accountKey = LoginAttempts.AccountKey(address);
        var accountRecord = await attempts.GetAsync(accountKey, cancellationToken).ConfigureAwait(false);
        if (accountRecord is not null && accountRecord.IsLocked(now))
        {
            return AuthResult.AccountLocked();
        }

        var user = await users.FindByEmailAsync(address, cancellationToken).ConfigureAwait(false);

        // One message for "no such account" and for "wrong password": telling
        // them apart would confirm which addresses are registered. The same
        // bcrypt work for both, too, or the response time would tell them apart.
        var verified = user is null
            ? VerifyAgainstNoAccount(password)
            : passwords.Verify(password, user.PasswordHash);

        if (!verified || user is null)
        {
            await RecordFailureAsync(clientKey, record, now, cancellationToken).ConfigureAwait(false);
            await attempts.SaveAsync(
                LoginAttempts.AfterFailure(accountKey, accountRecord, now, LoginAttempts.MaxAccountAttempts, LoginAttempts.LockoutDuration),
                cancellationToken).ConfigureAwait(false);
            return AuthResult.Fail(IdentityText.Current.InvalidCredentials);
        }

        if (!user.IsActive)
        {
            // Distinct from a wrong password on purpose: the credentials were
            // right, and the person needs to know it is an account problem.
            return AuthResult.Fail(IdentityText.Current.AccountDeactivated);
        }

        return await ContinueSignInAsync(user, clientKey, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Resolves a bearer token to a user, or null if it is not usable.</summary>
    public async Task<AuthenticatedUser?> ValidateAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var session = await sessions.FindAsync(AuthSession.HashOf(token), cancellationToken).ConfigureAwait(false);
        if (session is null || session.IsExpired(clock.UtcNow))
        {
            return null;
        }

        var user = await users.FindByIdAsync(session.OrganizationId, session.UserId, cancellationToken)
            .ConfigureAwait(false);

        // Re-checked on every request rather than trusted from the session:
        // deactivating an account takes effect immediately.
        if (user is null || !user.IsActive)
        {
            return null;
        }

        return ToAuthenticated(user);
    }

    /// <summary>
    /// What an invitation link shows: organisation and email. Unknown, used
    /// and expired codes count as failed attempts for the caller's address,
    /// like wrong passwords, so codes cannot be guessed at speed.
    /// </summary>
    public async Task<InvitationLookup> GetInvitationAsync(
        string code,
        string clientKey,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var record = await attempts.GetAsync(clientKey, cancellationToken).ConfigureAwait(false);
        if (record is not null && record.IsLocked(now))
        {
            return new InvitationLookup(null, IdentityText.Current.TooManyAttempts);
        }

        var invitation = await FindUsableInvitationAsync(code, now, cancellationToken).ConfigureAwait(false);
        if (invitation is null)
        {
            await RecordFailureAsync(clientKey, record, now, cancellationToken).ConfigureAwait(false);
            // One message for every unusable invitation, so the API does not reveal which codes existed.
            return new InvitationLookup(null, IdentityText.Current.InvalidInvitation);
        }

        var organization = await platform.FindOrganizationAsync(invitation.OrganizationId, cancellationToken)
            .ConfigureAwait(false);
        return new InvitationLookup(
            new InvitationInfo(organization?.Name ?? "", organization?.CountryCode ?? "", invitation.Email, invitation.Name),
            null);
    }

    /// <summary>
    /// Turns an invitation into an account: Admin of the invited organisation,
    /// who sets up MFA (the returned challenge) and is then signed in. The
    /// invitation is consumed before the account is
    /// made, so it cannot be used twice, even by two simultaneous requests.
    /// </summary>
    public async Task<AuthResult> AcceptInvitationAsync(
        string code,
        string name,
        string password,
        string clientKey,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var record = await attempts.GetAsync(clientKey, cancellationToken).ConfigureAwait(false);
        if (record is not null && record.IsLocked(now))
        {
            return AuthResult.Locked();
        }

        var invitation = await FindUsableInvitationAsync(code, now, cancellationToken).ConfigureAwait(false);
        if (invitation is null)
        {
            await RecordFailureAsync(clientKey, record, now, cancellationToken).ConfigureAwait(false);
            return AuthResult.Fail(IdentityText.Current.InvalidInvitation);
        }

        var validation = ValidateCredentials(invitation.Email, password, name);
        if (validation is not null)
        {
            return AuthResult.Fail(validation);
        }

        if (await users.FindByEmailAsync(invitation.Email, cancellationToken).ConfigureAwait(false) is not null)
        {
            return AuthResult.Fail(IdentityText.Current.AccountWithEmailExistsLogIn);
        }

        if (!await platform.TryDeleteInvitationAsync(invitation.CodeHash, cancellationToken).ConfigureAwait(false))
        {
            return AuthResult.Fail(IdentityText.Current.InvalidInvitation);
        }

        var user = new User
        {
            Id = Guid.NewGuid().ToString("n"),
            OrganizationId = invitation.OrganizationId,
            Email = invitation.Email,
            PasswordHash = passwords.Hash(password),
            Name = name.Trim(),
            Role = UserRole.Admin,
            IsActive = true,
            CreatedAt = now,
        };

        try
        {
            await users.AddAsync(user, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            return AuthResult.Fail(IdentityText.Current.AccountWithEmailExistsLogIn);
        }

        var organization = await platform.FindOrganizationAsync(invitation.OrganizationId, cancellationToken)
            .ConfigureAwait(false);
        if (organization is not null && organization.PendingInvitationHash == invitation.CodeHash)
        {
            await platform.SaveOrganizationAsync(
                organization with { PendingInvitationHash = null, PendingInvitationEmail = null, PendingInvitationExpiresAt = null },
                cancellationToken).ConfigureAwait(false);
        }

        // The invitation proved who this is; MFA is optional for the new Admin, so this is the session.
        await attempts.ClearAsync(clientKey, cancellationToken).ConfigureAwait(false);
        return await ContinueSignInAsync(user, clientKey, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Invitation?> FindUsableInvitationAsync(
        string code,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var invitation = await platform.FindInvitationAsync(Invitation.HashOf(code), cancellationToken)
            .ConfigureAwait(false);
        return invitation is null || invitation.IsExpired(now) ? null : invitation;
    }

    public Task LogoutAsync(string token, CancellationToken cancellationToken = default)
        => sessions.DeleteAsync(AuthSession.HashOf(token), cancellationToken);

    /// <summary>
    /// Signs a user in that was just created elsewhere (self sign-up), clearing
    /// the address's attempts. MFA is optional for such a user, so this returns
    /// the session.
    /// </summary>
    internal async Task<AuthResult> SignInAsync(User user, string clientKey, CancellationToken cancellationToken)
    {
        await attempts.ClearAsync(clientKey, cancellationToken).ConfigureAwait(false);
        return await ContinueSignInAsync(user, clientKey, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// After the password (or an invitation, or a confirmation link) proved who
    /// this is: a code if MFA is on, setting it up if it is mandatory and not
    /// on, otherwise the session.
    ///
    /// The address's failed attempts are only cleared once the session exists.
    /// Clearing them at the password would hand out a fresh set of guesses at
    /// the code with every new challenge.
    /// </summary>
    internal async Task<AuthResult> ContinueSignInAsync(User user, string? clientKey, CancellationToken cancellationToken)
    {
        var state = await mfa.GetAsync(user.OrganizationId, user.Id, cancellationToken).ConfigureAwait(false);

        if (state is { IsEnabled: true })
        {
            return await ChallengeAsync(user, MfaChallengePurpose.SignIn, cancellationToken).ConfigureAwait(false);
        }

        if (UserMfa.IsMandatoryFor(user))
        {
            return await ChallengeAsync(user, MfaChallengePurpose.Setup, cancellationToken).ConfigureAwait(false);
        }

        return await CompleteSignInAsync(user, clientKey, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Every step is done: clear the address's and the account's failures, record the login, issue the session.</summary>
    internal async Task<AuthResult> CompleteSignInAsync(User user, string? clientKey, CancellationToken cancellationToken)
    {
        if (clientKey is not null)
        {
            await attempts.ClearAsync(clientKey, cancellationToken).ConfigureAwait(false);
        }

        await attempts.ClearAsync(LoginAttempts.AccountKey(user.Email), cancellationToken).ConfigureAwait(false);

        await users.UpdateLastLoginAsync(user.OrganizationId, user.Id, clock.UtcNow, cancellationToken)
            .ConfigureAwait(false);

        return await IssueSessionAsync(user, cancellationToken).ConfigureAwait(false);
    }

    private async Task<AuthResult> ChallengeAsync(User user, MfaChallengePurpose purpose, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var token = MfaChallenge.NewToken();
        var challenge = new MfaChallenge(
            MfaChallenge.HashOf(token),
            user.OrganizationId,
            user.Id,
            purpose,
            now,
            now.Add(MfaChallenge.LifetimeFor(purpose)));

        await mfa.SaveChallengeAsync(challenge, cancellationToken).ConfigureAwait(false);

        return new AuthResult(
            true,
            Mfa: new MfaPending(token, purpose == MfaChallengePurpose.Setup, challenge.ExpiresAt));
    }

    /// <summary>Counts an attempt towards the per-address lockout (login, invitations, sign-up).</summary>
    /// <returns>The record after counting it.</returns>
    internal Task<LoginAttempts> RecordAttemptAsync(string clientKey, LoginAttempts? existing, DateTimeOffset now, CancellationToken cancellationToken)
        => RecordFailureAsync(clientKey, existing, now, cancellationToken);

    /// <summary>Whether the account is locked out by failures from any address (<see cref="LoginAttempts.AccountKey"/>).</summary>
    internal async Task<bool> IsAccountLockedAsync(User user, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var record = await attempts.GetAsync(LoginAttempts.AccountKey(user.Email), cancellationToken).ConfigureAwait(false);
        return record is not null && record.IsLocked(now);
    }

    /// <summary>Counts a wrong second-step code against the account, from whatever address it came.</summary>
    /// <returns>The record after counting it.</returns>
    internal async Task<LoginAttempts> RecordAccountFailureAsync(User user, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var key = LoginAttempts.AccountKey(user.Email);
        var existing = await attempts.GetAsync(key, cancellationToken).ConfigureAwait(false);
        var updated = LoginAttempts.AfterFailure(key, existing, now, LoginAttempts.MaxAccountAttempts, LoginAttempts.LockoutDuration);
        await attempts.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    private async Task<AuthResult> IssueSessionAsync(User user, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var token = AuthSession.NewToken();
        var session = new AuthSession(
            AuthSession.HashOf(token),
            user.Id,
            user.OrganizationId,
            now,
            now.Add(AuthSession.Lifetime));

        await sessions.SaveAsync(session, cancellationToken).ConfigureAwait(false);

        return new AuthResult(
            true,
            Token: token,
            ExpiresAt: session.ExpiresAt,
            User: ToAuthenticated(user));
    }

    private static AuthenticatedUser ToAuthenticated(User user)
        => new(user.Id, user.OrganizationId, user.Email, user.Name, user.Role, user.IsPlatformAdmin);

    private async Task<LoginAttempts> RecordFailureAsync(
        string clientKey,
        LoginAttempts? existing,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var updated = LoginAttempts.AfterFailure(clientKey, existing, now, LoginAttempts.MaxAttempts, LoginAttempts.LockoutDuration);
        await attempts.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    /// <summary>
    /// A hash no password matches, per hasher, made on first use. Verifying
    /// against it costs what verifying a real one costs.
    /// </summary>
    private static readonly ConditionalWeakTable<IPasswordHasher, string> NoAccountHashes = new();

    /// <summary>The bcrypt work of a real check, for a login whose account does not exist. Always false.</summary>
    private bool VerifyAgainstNoAccount(string password)
    {
        var hash = NoAccountHashes.GetValue(
            passwords,
            static hasher => hasher.Hash(Convert.ToHexString(RandomNumberGenerator.GetBytes(32))));
        _ = passwords.Verify(password, hash);
        return false;
    }

    /// <returns>An error message, or null when the input is acceptable.</returns>
    internal static string? ValidateCredentials(string email, string password, string name)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
        {
            return IdentityText.Current.InvalidEmail;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return IdentityText.Current.NameRequired;
        }

        // Length is the property that actually matters; composition rules push
        // people towards predictable substitutions.
        return password.Length < 12
            ? IdentityText.Current.PasswordTooShort
            : null;
    }
}
