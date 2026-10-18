using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Domain.Identity;
using InvoicePlatform.Domain.Organizations;

namespace InvoicePlatform.Application.Identity;

/// <summary>Outcome of a login or setup attempt. Never carries the hash.</summary>
public sealed record AuthResult(
    bool Succeeded,
    string? Error = null,
    string? Token = null,
    DateTimeOffset? ExpiresAt = null,
    AuthenticatedUser? User = null)
{
    public static AuthResult Fail(string error) => new(false, error);
}

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
/// separate accounts.
/// </summary>
public sealed class AuthService(
    IUserStore users,
    ISessionStore sessions,
    ILoginAttemptStore attempts,
    IPasswordHasher passwords,
    IClock clock,
    IPlatformStore platform)
{
    /// <summary>One message for every unusable invitation, so the API does not reveal which codes existed.</summary>
    private const string InvalidInvitation = "Deze uitnodiging is ongeldig of verlopen.";

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
            return AuthResult.Fail("Er bestaat al een account. Gebruik inloggen.");
        }

        var validation = ValidateCredentials(email, password, name);
        if (validation is not null)
        {
            return AuthResult.Fail(validation);
        }

        if (string.IsNullOrWhiteSpace(organizationName))
        {
            return AuthResult.Fail("Naam van de organisatie is verplicht.");
        }

        if (!Countries.IsSupported(countryCode))
        {
            return AuthResult.Fail("Kies een land uit de lijst.");
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
            return AuthResult.Fail("Er bestaat al een account. Gebruik inloggen.");
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

        return await IssueSessionAsync(user, cancellationToken).ConfigureAwait(false);
    }

    /// <param name="clientKey">
    /// Identifies the caller for rate limiting - the client IP. Rate limiting is
    /// per address rather than per account so that guessing many usernames from
    /// one place is throttled too.
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
            return AuthResult.Fail("Te veel pogingen. Wacht 15 minuten.");
        }

        var user = await users.FindByEmailAsync(User.NormalizeEmail(email), cancellationToken)
            .ConfigureAwait(false);

        // One message for "no such account" and for "wrong password": telling
        // them apart would confirm which addresses are registered.
        if (user is null || !passwords.Verify(password, user.PasswordHash))
        {
            await RecordFailureAsync(clientKey, record, now, cancellationToken).ConfigureAwait(false);
            return AuthResult.Fail("Ongeldige inloggegevens.");
        }

        if (!user.IsActive)
        {
            // Distinct from a wrong password on purpose: the credentials were
            // right, and the person needs to know it is an account problem.
            return AuthResult.Fail("Account gedeactiveerd.");
        }

        await attempts.ClearAsync(clientKey, cancellationToken).ConfigureAwait(false);
        await users.UpdateLastLoginAsync(user.OrganizationId, user.Id, now, cancellationToken)
            .ConfigureAwait(false);

        return await IssueSessionAsync(user, cancellationToken).ConfigureAwait(false);
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

        var session = await sessions.FindAsync(token, cancellationToken).ConfigureAwait(false);
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
            return new InvitationLookup(null, "Te veel pogingen. Wacht 15 minuten.");
        }

        var invitation = await FindUsableInvitationAsync(code, now, cancellationToken).ConfigureAwait(false);
        if (invitation is null)
        {
            await RecordFailureAsync(clientKey, record, now, cancellationToken).ConfigureAwait(false);
            return new InvitationLookup(null, InvalidInvitation);
        }

        var organization = await platform.FindOrganizationAsync(invitation.OrganizationId, cancellationToken)
            .ConfigureAwait(false);
        return new InvitationLookup(
            new InvitationInfo(organization?.Name ?? "", organization?.CountryCode ?? "", invitation.Email, invitation.Name),
            null);
    }

    /// <summary>
    /// Turns an invitation into an account: Admin of the invited organisation,
    /// signed in at once. The invitation is consumed before the account is
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
            return AuthResult.Fail("Te veel pogingen. Wacht 15 minuten.");
        }

        var invitation = await FindUsableInvitationAsync(code, now, cancellationToken).ConfigureAwait(false);
        if (invitation is null)
        {
            await RecordFailureAsync(clientKey, record, now, cancellationToken).ConfigureAwait(false);
            return AuthResult.Fail(InvalidInvitation);
        }

        var validation = ValidateCredentials(invitation.Email, password, name);
        if (validation is not null)
        {
            return AuthResult.Fail(validation);
        }

        if (await users.FindByEmailAsync(invitation.Email, cancellationToken).ConfigureAwait(false) is not null)
        {
            return AuthResult.Fail("Er bestaat al een account met dit e-mailadres. Gebruik inloggen.");
        }

        if (!await platform.TryDeleteInvitationAsync(invitation.CodeHash, cancellationToken).ConfigureAwait(false))
        {
            return AuthResult.Fail(InvalidInvitation);
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
            return AuthResult.Fail("Er bestaat al een account met dit e-mailadres. Gebruik inloggen.");
        }

        var organization = await platform.FindOrganizationAsync(invitation.OrganizationId, cancellationToken)
            .ConfigureAwait(false);
        if (organization is not null && organization.PendingInvitationHash == invitation.CodeHash)
        {
            await platform.SaveOrganizationAsync(
                organization with { PendingInvitationHash = null, PendingInvitationEmail = null, PendingInvitationExpiresAt = null },
                cancellationToken).ConfigureAwait(false);
        }

        await attempts.ClearAsync(clientKey, cancellationToken).ConfigureAwait(false);
        return await IssueSessionAsync(user, cancellationToken).ConfigureAwait(false);
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
        => sessions.DeleteAsync(token, cancellationToken);

    private async Task<AuthResult> IssueSessionAsync(User user, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var session = new AuthSession(
            AuthSession.NewToken(),
            user.Id,
            user.OrganizationId,
            now,
            now.Add(AuthSession.Lifetime));

        await sessions.SaveAsync(session, cancellationToken).ConfigureAwait(false);

        return new AuthResult(
            true,
            Token: session.Token,
            ExpiresAt: session.ExpiresAt,
            User: ToAuthenticated(user));
    }

    private static AuthenticatedUser ToAuthenticated(User user)
        => new(user.Id, user.OrganizationId, user.Email, user.Name, user.Role, user.IsPlatformAdmin);

    private async Task RecordFailureAsync(
        string clientKey,
        LoginAttempts? existing,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var failed = (existing?.FailedCount ?? 0) + 1;
        var lockedUntil = failed >= LoginAttempts.MaxAttempts
            ? now.Add(LoginAttempts.LockoutDuration)
            : (DateTimeOffset?)null;

        await attempts.SaveAsync(new LoginAttempts(clientKey, failed, lockedUntil), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <returns>An error message, or null when the input is acceptable.</returns>
    private static string? ValidateCredentials(string email, string password, string name)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
        {
            return "Ongeldig e-mailadres.";
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return "Naam is verplicht.";
        }

        // Length is the property that actually matters; composition rules push
        // people towards predictable substitutions.
        return password.Length < 12
            ? "Wachtwoord moet minimaal 12 tekens bevatten."
            : null;
    }
}
