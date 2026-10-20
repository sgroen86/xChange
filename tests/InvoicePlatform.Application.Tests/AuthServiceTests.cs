using InvoicePlatform.Application.Identity;
using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Domain.Identity;

namespace InvoicePlatform.Application.Tests;

/// <summary>
/// Covers the parts of login where a mistake is a security hole rather than a
/// bug: lockout, inactive accounts, session expiry, and immediate revocation.
///
/// The login tests use an ordinary user, for whom MFA is optional and off, so
/// the password alone gives a session. Admins and platform administrators get
/// an MFA setup challenge instead; that is asserted here and taken further in
/// <see cref="MfaServiceTests"/>.
/// </summary>
public class AuthServiceTests
{
    private const string Password = "correct-horse-battery";

    private readonly FakeStore _store = new();
    private readonly FakePlatformStore _platform = new();
    private readonly FakeMfaStore _mfa = new();
    private readonly TestClock _clock = new(DateTimeOffset.Parse("2026-09-08T10:00:00Z"));
    private readonly AuthService _auth;

    public AuthServiceTests()
        => _auth = new AuthService(_store, _store, _store, new FakeHasher(), _clock, _platform, _mfa);

    private async Task<AuthResult> SetupAsync(string password = Password)
        => await _auth.SetupFirstUserAsync("Stefan@Example.NL ", password, "Stefan", "Green IT", "SG");

    /// <summary>An ordinary user of an organisation, as an admin would have added them.</summary>
    private async Task<User> SeedUserAsync(string email = "piet@example.nl", UserRole role = UserRole.User)
    {
        var user = new User
        {
            Id = Guid.NewGuid().ToString("n"),
            OrganizationId = "org-1",
            Email = email,
            PasswordHash = new FakeHasher().Hash(Password),
            Name = "Piet",
            Role = role,
        };
        await _store.AddAsync(user);
        return user;
    }

    private async Task<AuthResult> SignInAsync()
    {
        await SeedUserAsync();
        var result = await _auth.LoginAsync("piet@example.nl", Password, "1.2.3.4");
        Assert.NotNull(result.Token);
        return result;
    }

    [Fact]
    public async Task Setup_creates_the_first_admin_who_sets_up_mfa_before_any_session()
    {
        var result = await SetupAsync();

        Assert.True(result.Succeeded);
        Assert.Null(result.Token);
        Assert.Null(result.User);
        Assert.True(result.Mfa!.SetupRequired);
        // Email is normalised, so casing and stray spaces cannot create a second account.
        var stored = await _store.FindByEmailAsync("stefan@example.nl");
        Assert.Equal(UserRole.Admin, stored!.Role);
        Assert.NotEmpty(stored.OrganizationId);
    }

    [Fact]
    public async Task Setup_is_refused_once_an_account_exists()
    {
        await SetupAsync();

        var second = await _auth.SetupFirstUserAsync("other@example.nl", "another-long-password", "Other", "Org", "NL");

        Assert.False(second.Succeeded);
        Assert.Contains("bestaat al", second.Error);
    }

    [Fact]
    public async Task Setup_reports_a_duplicate_account_instead_of_throwing()
    {
        // Defence in depth behind AnyUsersExistAsync. If that check is ever
        // wrong - it was, because a DynamoDB Limit caps items examined rather
        // than items returned - the user should see a clear message rather than
        // a 500 from an unhandled exception.
        await SetupAsync();
        _store.PretendNoUsersExist = true;

        var second = await _auth.SetupFirstUserAsync(
            "stefan@example.nl", "another-long-password", "Stefan", "Org", "NL");

        Assert.False(second.Succeeded);
        Assert.Contains("bestaat al", second.Error);
    }

    [Fact]
    public async Task Setup_rejects_a_short_password()
    {
        var result = await _auth.SetupFirstUserAsync("a@b.nl", "short", "Name", "Org", "NL");

        Assert.False(result.Succeeded);
        Assert.Contains("12 tekens", result.Error);
    }

    [Fact]
    public async Task Login_succeeds_with_the_right_password_and_is_case_insensitive_on_email()
    {
        await SeedUserAsync();

        var result = await _auth.LoginAsync("PIET@example.nl", Password, "1.2.3.4");

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Token);
        Assert.Null(result.Mfa);
    }

    [Fact]
    public async Task Login_fails_with_the_wrong_password()
    {
        await SeedUserAsync();

        var result = await _auth.LoginAsync("piet@example.nl", "wrong-password-here", "1.2.3.4");

        Assert.False(result.Succeeded);
        Assert.Equal("Ongeldige inloggegevens.", result.Error);
    }

    [Fact]
    public async Task An_unknown_account_and_a_wrong_password_are_indistinguishable()
    {
        await SeedUserAsync();

        var unknown = await _auth.LoginAsync("nobody@example.nl", "some-long-password", "1.2.3.4");
        var wrong = await _auth.LoginAsync("piet@example.nl", "some-long-password", "5.6.7.8");

        // Different messages here would let someone enumerate which addresses
        // have accounts.
        Assert.Equal(unknown.Error, wrong.Error);
    }

    [Fact]
    public async Task Five_failures_lock_the_address_out()
    {
        await SeedUserAsync();

        for (var attempt = 0; attempt < LoginAttempts.MaxAttempts; attempt++)
        {
            await _auth.LoginAsync("piet@example.nl", "wrong-password-here", "9.9.9.9");
        }

        // Now even the correct password is refused.
        var locked = await _auth.LoginAsync("piet@example.nl", Password, "9.9.9.9");

        Assert.False(locked.Succeeded);
        Assert.Contains("Te veel pogingen", locked.Error);
    }

    [Fact]
    public async Task The_lockout_is_per_address_not_global()
    {
        await SeedUserAsync();

        for (var attempt = 0; attempt < LoginAttempts.MaxAttempts; attempt++)
        {
            await _auth.LoginAsync("piet@example.nl", "wrong-password-here", "9.9.9.9");
        }

        // One attacker must not be able to lock the real user out.
        var elsewhere = await _auth.LoginAsync("piet@example.nl", Password, "1.1.1.1");

        Assert.True(elsewhere.Succeeded);
        Assert.NotNull(elsewhere.Token);
    }

    [Fact]
    public async Task The_lockout_expires()
    {
        await SeedUserAsync();

        for (var attempt = 0; attempt < LoginAttempts.MaxAttempts; attempt++)
        {
            await _auth.LoginAsync("piet@example.nl", "wrong-password-here", "9.9.9.9");
        }

        _clock.Advance(LoginAttempts.LockoutDuration + TimeSpan.FromMinutes(1));

        var later = await _auth.LoginAsync("piet@example.nl", Password, "9.9.9.9");

        Assert.NotNull(later.Token);
    }

    [Fact]
    public async Task A_successful_login_clears_the_failure_count()
    {
        await SeedUserAsync();

        for (var attempt = 0; attempt < LoginAttempts.MaxAttempts - 1; attempt++)
        {
            await _auth.LoginAsync("piet@example.nl", "wrong-password-here", "9.9.9.9");
        }

        Assert.NotNull((await _auth.LoginAsync("piet@example.nl", Password, "9.9.9.9")).Token);

        // The earlier failures must not carry over and lock the account on the
        // next single mistake.
        await _auth.LoginAsync("piet@example.nl", "wrong-password-here", "9.9.9.9");
        var stillFine = await _auth.LoginAsync("piet@example.nl", Password, "9.9.9.9");

        Assert.NotNull(stillFine.Token);
    }

    [Fact]
    public async Task A_right_password_does_not_clear_the_failure_count_while_mfa_is_still_to_come()
    {
        // Otherwise every new challenge would hand out a fresh set of guesses at the code.
        await SeedUserAsync("admin@example.nl", UserRole.Admin);

        for (var attempt = 0; attempt < LoginAttempts.MaxAttempts - 1; attempt++)
        {
            await _auth.LoginAsync("admin@example.nl", "wrong-password-here", "9.9.9.9");
        }

        Assert.NotNull((await _auth.LoginAsync("admin@example.nl", Password, "9.9.9.9")).Mfa);
        await _auth.LoginAsync("admin@example.nl", "wrong-password-here", "9.9.9.9");

        Assert.True((await _auth.LoginAsync("admin@example.nl", Password, "9.9.9.9")).LockedOut);
    }

    [Theory]
    [InlineData(UserRole.Admin, false)]
    [InlineData(UserRole.User, true)]
    public async Task Admins_and_platform_administrators_get_an_mfa_setup_challenge_instead_of_a_session(UserRole role, bool platformAdmin)
    {
        var user = await SeedUserAsync("boss@example.nl", role);
        if (platformAdmin)
        {
            _store.MakePlatformAdmin(user.Id);
        }

        var result = await _auth.LoginAsync("boss@example.nl", Password, "1.2.3.4");

        Assert.True(result.Succeeded);
        Assert.Null(result.Token);
        Assert.Null(result.User);
        Assert.True(result.Mfa!.SetupRequired);
        Assert.Equal(64, result.Mfa.Challenge.Length);
        // Only the hash is stored, bound to this user; setting up an
        // authenticator gets fifteen minutes rather than the five a code gets.
        var stored = Assert.Single(_mfa.Challenges.Values);
        Assert.Equal(MfaChallenge.HashOf(result.Mfa.Challenge), stored.TokenHash);
        Assert.Equal((user.Id, MfaChallengePurpose.Setup), (stored.UserId, stored.Purpose));
        Assert.Equal(_clock.UtcNow + TimeSpan.FromMinutes(15), stored.ExpiresAt);
    }

    [Fact]
    public async Task A_user_with_mfa_on_gets_a_code_challenge_instead_of_a_session()
    {
        var user = await SeedUserAsync();
        await _mfa.SaveAsync(new UserMfa { OrganizationId = user.OrganizationId, UserId = user.Id, ProtectedSecret = "x" });

        var result = await _auth.LoginAsync("piet@example.nl", Password, "1.2.3.4");

        Assert.Null(result.Token);
        Assert.False(result.Mfa!.SetupRequired);
        Assert.Equal(MfaChallengePurpose.SignIn, Assert.Single(_mfa.Challenges.Values).Purpose);
    }

    [Fact]
    public async Task A_deactivated_account_cannot_log_in_even_with_the_right_password()
    {
        var user = await SeedUserAsync();
        _store.Deactivate(user.Id);

        var result = await _auth.LoginAsync("piet@example.nl", Password, "1.2.3.4");

        Assert.False(result.Succeeded);
        Assert.Equal("Account gedeactiveerd.", result.Error);
    }

    [Fact]
    public async Task A_valid_token_resolves_to_the_user()
    {
        var session = await SignInAsync();

        var user = await _auth.ValidateAsync(session.Token!);

        Assert.NotNull(user);
        Assert.Equal(session.User!.Id, user!.Id);
    }

    [Fact]
    public async Task An_unknown_or_empty_token_resolves_to_nothing()
    {
        await SignInAsync();

        Assert.Null(await _auth.ValidateAsync("not-a-real-token"));
        Assert.Null(await _auth.ValidateAsync(""));
    }

    [Fact]
    public async Task A_session_stops_working_once_it_expires()
    {
        var session = await SignInAsync();

        _clock.Advance(AuthSession.Lifetime + TimeSpan.FromMinutes(1));

        Assert.Null(await _auth.ValidateAsync(session.Token!));
    }

    [Fact]
    public async Task Logging_out_invalidates_the_token_immediately()
    {
        var session = await SignInAsync();

        await _auth.LogoutAsync(session.Token!);

        Assert.Null(await _auth.ValidateAsync(session.Token!));
    }

    [Fact]
    public async Task Deactivating_an_account_invalidates_its_live_session()
    {
        // The reason sessions are server-side rather than self-contained tokens:
        // revocation has to take effect now, not when the token expires.
        var session = await SignInAsync();
        Assert.NotNull(await _auth.ValidateAsync(session.Token!));

        _store.Deactivate(session.User!.Id);

        Assert.Null(await _auth.ValidateAsync(session.Token!));
    }

    [Fact]
    public async Task Tokens_are_unique_and_not_guessable_in_shape()
    {
        var first = await SignInAsync();
        var second = await _auth.LoginAsync("piet@example.nl", Password, "1.2.3.4");

        Assert.NotEqual(first.Token, second.Token);
        Assert.Equal(64, first.Token!.Length); // 32 bytes, hex encoded
    }

    [Fact]
    public async Task The_password_hash_is_never_returned()
    {
        foreach (var result in new[] { await SetupAsync(), await SignInAsync() })
        {
            var serialized = System.Text.Json.JsonSerializer.Serialize(result);

            Assert.DoesNotContain("hash", serialized, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(Password, serialized, StringComparison.Ordinal);
        }
    }

    // ── Organisations and invitations ────────────────────────────────────────

    [Fact]
    public async Task Setup_makes_the_first_account_platform_administrator_and_records_the_organisation()
    {
        await SetupAsync();

        var user = await _store.FindByEmailAsync("stefan@example.nl");
        Assert.True(user!.IsPlatformAdmin);
        var organization = await _platform.FindOrganizationAsync(user.OrganizationId);
        Assert.Equal(("Green IT", "SG"), (organization!.Name, organization.CountryCode));
    }

    [Theory]
    [InlineData("UK")]
    [InlineData("")]
    public async Task Setup_needs_a_served_country(string country)
    {
        var result = await _auth.SetupFirstUserAsync("a@b.nl", "long-enough-password", "A", "Org", country);

        Assert.False(result.Succeeded);
    }

    private async Task<(string OrganizationId, string Code)> InviteAsync(string email = "Jan@Klant.SG")
    {
        var platform = new PlatformService(_platform, _store, _clock);
        var created = await platform.CreateOrganizationAsync("platform-admin", "Klant Pte Ltd", "SG", "Jan", email);
        return (created.Organization!.OrganizationId, created.InvitationCode!);
    }

    [Fact]
    public async Task Accepting_an_invitation_makes_an_admin_of_that_organisation_who_sets_up_mfa_first()
    {
        var (organizationId, code) = await InviteAsync();

        var result = await _auth.AcceptInvitationAsync(code, "Jan Tan", "a-long-enough-password", "10.0.0.1");

        Assert.True(result.Succeeded);
        Assert.Null(result.Token);
        Assert.True(result.Mfa!.SetupRequired);
        var user = await _store.FindByEmailAsync("jan@klant.sg");
        Assert.Equal(organizationId, user!.OrganizationId);
        Assert.Equal(UserRole.Admin, user.Role);
        Assert.False(user.IsPlatformAdmin);
        Assert.Null((await _platform.FindOrganizationAsync(organizationId))!.PendingInvitationHash);
    }

    [Fact]
    public async Task An_invitation_works_once()
    {
        var (_, code) = await InviteAsync();
        await _auth.AcceptInvitationAsync(code, "Jan", "a-long-enough-password", "10.0.0.1");

        var again = await _auth.AcceptInvitationAsync(code, "Jan", "a-long-enough-password", "10.0.0.2");

        Assert.False(again.Succeeded);
        Assert.Equal("Deze uitnodiging is ongeldig of verlopen.", again.Error);
    }

    [Fact]
    public async Task An_invitation_expires_after_seven_days()
    {
        var (_, code) = await InviteAsync();
        _clock.Advance(TimeSpan.FromDays(7));

        var result = await _auth.AcceptInvitationAsync(code, "Jan", "a-long-enough-password", "10.0.0.1");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Reissuing_an_invitation_invalidates_the_previous_link()
    {
        var (organizationId, oldCode) = await InviteAsync();
        var platform = new PlatformService(_platform, _store, _clock);
        var reissued = await platform.ReissueInvitationAsync(organizationId, "Jan", "jan@klant.sg");

        var withOld = await _auth.AcceptInvitationAsync(oldCode, "Jan", "a-long-enough-password", "10.0.0.1");
        var withNew = await _auth.AcceptInvitationAsync(reissued.InvitationCode!, "Jan", "a-long-enough-password", "10.0.0.1");

        Assert.False(withOld.Succeeded);
        Assert.True(withNew.Succeeded);
    }

    [Fact]
    public async Task Only_the_hash_of_an_invitation_code_is_stored()
    {
        var (_, code) = await InviteAsync();

        var stored = Assert.Single(_platform.Invitations.Values);
        Assert.NotEqual(code, stored.CodeHash);
        Assert.Equal(Invitation.HashOf(code), stored.CodeHash);
    }

    [Fact]
    public async Task A_short_password_does_not_use_up_the_invitation()
    {
        var (_, code) = await InviteAsync();

        var tooShort = await _auth.AcceptInvitationAsync(code, "Jan", "short", "10.0.0.1");
        var proper = await _auth.AcceptInvitationAsync(code, "Jan", "a-long-enough-password", "10.0.0.1");

        Assert.False(tooShort.Succeeded);
        Assert.True(proper.Succeeded);
    }

    [Fact]
    public async Task Guessing_invitation_codes_locks_the_address_out()
    {
        for (var i = 0; i < LoginAttempts.MaxAttempts; i++)
        {
            await _auth.GetInvitationAsync($"guess-{i}", "10.0.0.9");
        }

        var (_, code) = await InviteAsync();
        var lookup = await _auth.GetInvitationAsync(code, "10.0.0.9");

        Assert.Null(lookup.Invitation);
        Assert.Contains("Te veel pogingen", lookup.Error);
    }

    [Fact]
    public async Task An_invitation_shows_its_organisation_and_email()
    {
        var (_, code) = await InviteAsync();

        var lookup = await _auth.GetInvitationAsync(code, "10.0.0.1");

        Assert.Equal(("Klant Pte Ltd", "SG", "jan@klant.sg", "Jan"),
            (lookup.Invitation!.OrganizationName, lookup.Invitation.CountryCode, lookup.Invitation.Email, lookup.Invitation.Name));
    }

    // ── Test doubles ─────────────────────────────────────────────────────────

    private sealed class TestClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = now;

        public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
    }

    /// <summary>Reversible stand-in for bcrypt: the real one is deliberately slow.</summary>
    internal sealed class FakeHasher : IPasswordHasher
    {
        public string Hash(string password) => "hashed:" + password;

        public bool Verify(string password, string hash) => hash == "hashed:" + password;
    }

    internal sealed class FakeStore : IUserStore, ISessionStore, ILoginAttemptStore
    {
        private readonly Dictionary<string, User> _byEmail = [];
        private readonly Dictionary<string, AuthSession> _sessions = [];
        private readonly Dictionary<string, LoginAttempts> _attempts = [];

        public void Deactivate(string userId)
        {
            foreach (var (email, user) in _byEmail.ToList())
            {
                if (user.Id == userId)
                {
                    _byEmail[email] = user with { IsActive = false };
                }
            }
        }

        public void MakePlatformAdmin(string userId)
        {
            foreach (var (email, user) in _byEmail.ToList())
            {
                if (user.Id == userId)
                {
                    _byEmail[email] = user with { IsPlatformAdmin = true };
                }
            }
        }

        public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
            => Task.FromResult(_byEmail.GetValueOrDefault(email));

        public Task<User?> FindByIdAsync(string organizationId, string userId, CancellationToken cancellationToken = default)
            => Task.FromResult(_byEmail.Values.FirstOrDefault(
                u => u.Id == userId && u.OrganizationId == organizationId));

        /// <summary>Simulates the existence check returning a wrong answer.</summary>
        public bool PretendNoUsersExist { get; set; }

        public Task<bool> AnyUsersExistAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(!PretendNoUsersExist && _byEmail.Count > 0);

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            if (!_byEmail.TryAdd(user.Email, user))
            {
                throw new InvalidOperationException("duplicate");
            }

            return Task.CompletedTask;
        }

        public Task UpdateLastLoginAsync(string organizationId, string userId, DateTimeOffset at, CancellationToken cancellationToken = default)
        {
            foreach (var (email, user) in _byEmail.ToList())
            {
                if (user.Id == userId)
                {
                    _byEmail[email] = user with { LastLoginAt = at };
                }
            }

            return Task.CompletedTask;
        }

        public Task SaveAsync(AuthSession session, CancellationToken cancellationToken = default)
        {
            _sessions[session.Token] = session;
            return Task.CompletedTask;
        }

        public Task<AuthSession?> FindAsync(string token, CancellationToken cancellationToken = default)
            => Task.FromResult(_sessions.GetValueOrDefault(token));

        public Task DeleteAsync(string token, CancellationToken cancellationToken = default)
        {
            _sessions.Remove(token);
            return Task.CompletedTask;
        }

        public Task<LoginAttempts?> GetAsync(string clientKey, CancellationToken cancellationToken = default)
            => Task.FromResult(_attempts.GetValueOrDefault(clientKey));

        public Task SaveAsync(LoginAttempts attempts, CancellationToken cancellationToken = default)
        {
            _attempts[attempts.ClientKey] = attempts;
            return Task.CompletedTask;
        }

        public Task ClearAsync(string clientKey, CancellationToken cancellationToken = default)
        {
            _attempts.Remove(clientKey);
            return Task.CompletedTask;
        }
    }
}
