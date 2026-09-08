using InvoicePlatform.Domain.Identity;

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
    UserRole Role);

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
    IClock clock)
{
    /// <summary>
    /// Creates the first account and its organization.
    ///
    /// Mirrors Bookkeeping's setup.php: it works only while no user exists, so
    /// it closes itself permanently the moment the first account is made. The
    /// endpoint is public by necessity - there is nobody to authenticate as yet.
    /// </summary>
    public async Task<AuthResult> SetupFirstUserAsync(
        string email,
        string password,
        string name,
        string organizationName,
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

        _ = organizationName; // Reserved for the organisation record; not stored yet.

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

        return new AuthenticatedUser(user.Id, user.OrganizationId, user.Email, user.Name, user.Role);
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
            User: new AuthenticatedUser(user.Id, user.OrganizationId, user.Email, user.Name, user.Role));
    }

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
