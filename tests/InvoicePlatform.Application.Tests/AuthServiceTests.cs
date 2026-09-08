using InvoicePlatform.Application.Identity;
using InvoicePlatform.Domain.Identity;

namespace InvoicePlatform.Application.Tests;

/// <summary>
/// Covers the parts of login where a mistake is a security hole rather than a
/// bug: lockout, inactive accounts, session expiry, and immediate revocation.
/// </summary>
public class AuthServiceTests
{
    private readonly FakeStore _store = new();
    private readonly TestClock _clock = new(DateTimeOffset.Parse("2026-09-08T10:00:00Z"));
    private readonly AuthService _auth;

    public AuthServiceTests()
        => _auth = new AuthService(_store, _store, _store, new FakeHasher(), _clock);

    private async Task<AuthResult> SetupAsync(string password = "correct-horse-battery")
        => await _auth.SetupFirstUserAsync("Stefan@Example.NL ", password, "Stefan", "Green IT");

    [Fact]
    public async Task Setup_creates_the_first_admin_and_signs_them_in()
    {
        var result = await SetupAsync();

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Token);
        Assert.Equal(UserRole.Admin, result.User!.Role);
        // Email is normalised, so casing and stray spaces cannot create a second account.
        Assert.Equal("stefan@example.nl", result.User.Email);
        Assert.NotEmpty(result.User.OrganizationId);
    }

    [Fact]
    public async Task Setup_is_refused_once_an_account_exists()
    {
        await SetupAsync();

        var second = await _auth.SetupFirstUserAsync("other@example.nl", "another-long-password", "Other", "Org");

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
            "stefan@example.nl", "another-long-password", "Stefan", "Org");

        Assert.False(second.Succeeded);
        Assert.Contains("bestaat al", second.Error);
    }

    [Fact]
    public async Task Setup_rejects_a_short_password()
    {
        var result = await _auth.SetupFirstUserAsync("a@b.nl", "short", "Name", "Org");

        Assert.False(result.Succeeded);
        Assert.Contains("12 tekens", result.Error);
    }

    [Fact]
    public async Task Login_succeeds_with_the_right_password_and_is_case_insensitive_on_email()
    {
        await SetupAsync();

        var result = await _auth.LoginAsync("STEFAN@example.nl", "correct-horse-battery", "1.2.3.4");

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Token);
    }

    [Fact]
    public async Task Login_fails_with_the_wrong_password()
    {
        await SetupAsync();

        var result = await _auth.LoginAsync("stefan@example.nl", "wrong-password-here", "1.2.3.4");

        Assert.False(result.Succeeded);
        Assert.Equal("Ongeldige inloggegevens.", result.Error);
    }

    [Fact]
    public async Task An_unknown_account_and_a_wrong_password_are_indistinguishable()
    {
        await SetupAsync();

        var unknown = await _auth.LoginAsync("nobody@example.nl", "some-long-password", "1.2.3.4");
        var wrong = await _auth.LoginAsync("stefan@example.nl", "some-long-password", "5.6.7.8");

        // Different messages here would let someone enumerate which addresses
        // have accounts.
        Assert.Equal(unknown.Error, wrong.Error);
    }

    [Fact]
    public async Task Five_failures_lock_the_address_out()
    {
        await SetupAsync();

        for (var attempt = 0; attempt < LoginAttempts.MaxAttempts; attempt++)
        {
            await _auth.LoginAsync("stefan@example.nl", "wrong-password-here", "9.9.9.9");
        }

        // Now even the correct password is refused.
        var locked = await _auth.LoginAsync("stefan@example.nl", "correct-horse-battery", "9.9.9.9");

        Assert.False(locked.Succeeded);
        Assert.Contains("Te veel pogingen", locked.Error);
    }

    [Fact]
    public async Task The_lockout_is_per_address_not_global()
    {
        await SetupAsync();

        for (var attempt = 0; attempt < LoginAttempts.MaxAttempts; attempt++)
        {
            await _auth.LoginAsync("stefan@example.nl", "wrong-password-here", "9.9.9.9");
        }

        // One attacker must not be able to lock the real user out.
        var elsewhere = await _auth.LoginAsync("stefan@example.nl", "correct-horse-battery", "1.1.1.1");

        Assert.True(elsewhere.Succeeded);
    }

    [Fact]
    public async Task The_lockout_expires()
    {
        await SetupAsync();

        for (var attempt = 0; attempt < LoginAttempts.MaxAttempts; attempt++)
        {
            await _auth.LoginAsync("stefan@example.nl", "wrong-password-here", "9.9.9.9");
        }

        _clock.Advance(LoginAttempts.LockoutDuration + TimeSpan.FromMinutes(1));

        var later = await _auth.LoginAsync("stefan@example.nl", "correct-horse-battery", "9.9.9.9");

        Assert.True(later.Succeeded);
    }

    [Fact]
    public async Task A_successful_login_clears_the_failure_count()
    {
        await SetupAsync();

        for (var attempt = 0; attempt < LoginAttempts.MaxAttempts - 1; attempt++)
        {
            await _auth.LoginAsync("stefan@example.nl", "wrong-password-here", "9.9.9.9");
        }

        Assert.True((await _auth.LoginAsync("stefan@example.nl", "correct-horse-battery", "9.9.9.9")).Succeeded);

        // The earlier failures must not carry over and lock the account on the
        // next single mistake.
        await _auth.LoginAsync("stefan@example.nl", "wrong-password-here", "9.9.9.9");
        var stillFine = await _auth.LoginAsync("stefan@example.nl", "correct-horse-battery", "9.9.9.9");

        Assert.True(stillFine.Succeeded);
    }

    [Fact]
    public async Task A_deactivated_account_cannot_log_in_even_with_the_right_password()
    {
        var setup = await SetupAsync();
        _store.Deactivate(setup.User!.Id);

        var result = await _auth.LoginAsync("stefan@example.nl", "correct-horse-battery", "1.2.3.4");

        Assert.False(result.Succeeded);
        Assert.Equal("Account gedeactiveerd.", result.Error);
    }

    [Fact]
    public async Task A_valid_token_resolves_to_the_user()
    {
        var setup = await SetupAsync();

        var user = await _auth.ValidateAsync(setup.Token!);

        Assert.NotNull(user);
        Assert.Equal(setup.User!.Id, user!.Id);
    }

    [Fact]
    public async Task An_unknown_or_empty_token_resolves_to_nothing()
    {
        await SetupAsync();

        Assert.Null(await _auth.ValidateAsync("not-a-real-token"));
        Assert.Null(await _auth.ValidateAsync(""));
    }

    [Fact]
    public async Task A_session_stops_working_once_it_expires()
    {
        var setup = await SetupAsync();

        _clock.Advance(AuthSession.Lifetime + TimeSpan.FromMinutes(1));

        Assert.Null(await _auth.ValidateAsync(setup.Token!));
    }

    [Fact]
    public async Task Logging_out_invalidates_the_token_immediately()
    {
        var setup = await SetupAsync();

        await _auth.LogoutAsync(setup.Token!);

        Assert.Null(await _auth.ValidateAsync(setup.Token!));
    }

    [Fact]
    public async Task Deactivating_an_account_invalidates_its_live_session()
    {
        // The reason sessions are server-side rather than self-contained tokens:
        // revocation has to take effect now, not when the token expires.
        var setup = await SetupAsync();
        Assert.NotNull(await _auth.ValidateAsync(setup.Token!));

        _store.Deactivate(setup.User!.Id);

        Assert.Null(await _auth.ValidateAsync(setup.Token!));
    }

    [Fact]
    public async Task Tokens_are_unique_and_not_guessable_in_shape()
    {
        await SetupAsync();

        var first = await _auth.LoginAsync("stefan@example.nl", "correct-horse-battery", "1.2.3.4");
        var second = await _auth.LoginAsync("stefan@example.nl", "correct-horse-battery", "1.2.3.4");

        Assert.NotEqual(first.Token, second.Token);
        Assert.Equal(64, first.Token!.Length); // 32 bytes, hex encoded
    }

    [Fact]
    public async Task The_password_hash_is_never_returned()
    {
        var result = await SetupAsync();

        var serialized = System.Text.Json.JsonSerializer.Serialize(result);

        Assert.DoesNotContain("hash", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("correct-horse-battery", serialized, StringComparison.Ordinal);
    }

    // ── Test doubles ─────────────────────────────────────────────────────────

    private sealed class TestClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = now;

        public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
    }

    /// <summary>Reversible stand-in for bcrypt: the real one is deliberately slow.</summary>
    private sealed class FakeHasher : IPasswordHasher
    {
        public string Hash(string password) => "hashed:" + password;

        public bool Verify(string password, string hash) => hash == "hashed:" + password;
    }

    private sealed class FakeStore : IUserStore, ISessionStore, ILoginAttemptStore
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
