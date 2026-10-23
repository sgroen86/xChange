using InvoicePlatform.Application.Identity;
using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Domain.Identity;

namespace InvoicePlatform.Application.Tests;

/// <summary>
/// Two-step verification: the parts where a mistake lets someone in with the
/// password alone - challenges that live too long or too often, codes that work
/// twice, a platform administrator who can switch it off.
/// </summary>
public class MfaServiceTests
{
    private const string Password = "correct-horse-battery";
    private const string Client = "203.0.113.9";

    private readonly AuthServiceTests.FakeStore _users = new();
    private readonly FakePlatformStore _platform = new();
    private readonly FakeMfaStore _store = new();
    private readonly TestClock _clock = new(DateTimeOffset.Parse("2026-10-20T09:00:10Z"));
    private readonly AuthService _auth;
    private readonly MfaService _mfa;

    public MfaServiceTests()
    {
        _auth = new AuthService(_users, _users, _users, new AuthServiceTests.FakeHasher(), _clock, _platform, _store);
        _mfa = new MfaService(_users, _store, new FakeProtector(), _users, _clock, _auth);
    }

    private async Task<User> SeedAsync(string email, UserRole role, bool platformAdmin = false)
    {
        var user = new User
        {
            Id = Guid.NewGuid().ToString("n"),
            OrganizationId = "org-1",
            Email = email,
            PasswordHash = new AuthServiceTests.FakeHasher().Hash(Password),
            Name = email,
            Role = role,
            IsPlatformAdmin = platformAdmin,
        };
        await _users.AddAsync(user);
        return user;
    }

    /// <summary>The one kind of account for which MFA is mandatory: setting it up is part of its login.</summary>
    private Task<User> SeedPlatformAdminAsync(string email) => SeedAsync(email, UserRole.Admin, platformAdmin: true);

    private async Task<string> ChallengeAsync(string email, string client = Client)
    {
        var login = await _auth.LoginAsync(email, Password, client);
        Assert.Null(login.Token);
        return login.Mfa!.Challenge;
    }

    /// <summary>The whole mandatory setup during login, as the web app does it. Returns the secret and recovery codes.</summary>
    private async Task<(string Secret, IReadOnlyList<string> RecoveryCodes, AuthResult Session)> SetUpDuringLoginAsync(string email)
    {
        var challenge = await ChallengeAsync(email);
        var setup = await _mfa.StartLoginSetupAsync(challenge, Client);
        Assert.True(setup.Succeeded);
        var session = await _mfa.ConfirmLoginSetupAsync(challenge, Authenticator.CodeFor(setup.Setup!.Secret, _clock.UtcNow), Client);
        Assert.NotNull(session.Token);
        return (setup.Setup.Secret, session.RecoveryCodes!, session);
    }

    /// <summary>The next 30 seconds, so the following code is a new one rather than a replay.</summary>
    private void NextStep() => _clock.UtcNow += TimeSpan.FromSeconds(Totp.PeriodSeconds);

    [Fact]
    public async Task A_platform_administrator_sets_up_mfa_during_login_and_gets_a_session_and_ten_recovery_codes()
    {
        var admin = await SeedPlatformAdminAsync("admin@example.nl");
        var challenge = await ChallengeAsync("admin@example.nl");

        var setup = await _mfa.StartLoginSetupAsync(challenge, Client);
        var session = await _mfa.ConfirmLoginSetupAsync(challenge, Authenticator.CodeFor(setup.Setup!.Secret, _clock.UtcNow), Client);

        Assert.StartsWith("otpauth://totp/xChange:admin%40example.nl?secret=" + setup.Setup.Secret, setup.Setup.OtpAuthUri);
        Assert.NotNull(session.Token);
        Assert.Equal(admin.Id, session.User!.Id);
        Assert.Equal(RecoveryCodes.Count, session.RecoveryCodes!.Count);

        // Stored: the secret only protected, the recovery codes only hashed, the challenge gone.
        var stored = await _store.GetAsync("org-1", admin.Id);
        Assert.True(stored!.IsEnabled);
        Assert.DoesNotContain(setup.Setup.Secret, stored.ProtectedSecret!, StringComparison.OrdinalIgnoreCase);
        Assert.Null(stored.PendingProtectedSecret);
        Assert.Equal(session.RecoveryCodes.Select(RecoveryCodes.HashOf).Order(), stored.RecoveryCodeHashes.Order());
        Assert.Empty(_store.Challenges);
    }

    [Fact]
    public async Task A_wrong_code_does_not_finish_the_setup()
    {
        await SeedPlatformAdminAsync("admin@example.nl");
        var challenge = await ChallengeAsync("admin@example.nl");
        await _mfa.StartLoginSetupAsync(challenge, Client);

        var result = await _mfa.ConfirmLoginSetupAsync(challenge, "000000", Client);

        Assert.False(result.Succeeded);
        Assert.Equal("Ongeldige code.", result.Error);
        Assert.False((await _store.GetAsync("org-1", (await _users.FindByEmailAsync("admin@example.nl"))!.Id))!.IsEnabled);
    }

    [Fact]
    public async Task With_mfa_on_a_login_needs_the_code_from_the_app()
    {
        await SeedPlatformAdminAsync("admin@example.nl");
        var (secret, _, _) = await SetUpDuringLoginAsync("admin@example.nl");
        NextStep();

        var challenge = await ChallengeAsync("admin@example.nl");
        var wrong = await _mfa.VerifyLoginAsync(challenge, "123456", null, Client);
        var right = await _mfa.VerifyLoginAsync(challenge, Authenticator.CodeFor(secret, _clock.UtcNow), null, Client);

        Assert.False(wrong.Succeeded);
        Assert.False(wrong.ChallengeGone);
        Assert.NotNull(right.Token);
        Assert.Null(right.RecoveryCodes);
    }

    [Fact]
    public async Task A_code_works_one_step_either_side_but_never_twice()
    {
        await SeedPlatformAdminAsync("admin@example.nl");
        var (secret, _, _) = await SetUpDuringLoginAsync("admin@example.nl");
        NextStep();
        var code = Authenticator.CodeFor(secret, _clock.UtcNow, stepOffset: -1);

        // The setup used the previous step: its code is a replay.
        Assert.False((await _mfa.VerifyLoginAsync(await ChallengeAsync("admin@example.nl"), code, null, Client)).Succeeded);

        var nextCode = Authenticator.CodeFor(secret, _clock.UtcNow, stepOffset: 1);
        Assert.NotNull((await _mfa.VerifyLoginAsync(await ChallengeAsync("admin@example.nl"), nextCode, null, Client)).Token);
        Assert.False((await _mfa.VerifyLoginAsync(await ChallengeAsync("admin@example.nl"), nextCode, null, Client)).Succeeded);
    }

    [Fact]
    public async Task A_challenge_expires_after_five_minutes()
    {
        await SeedPlatformAdminAsync("admin@example.nl");
        var (secret, _, _) = await SetUpDuringLoginAsync("admin@example.nl");
        var challenge = await ChallengeAsync("admin@example.nl");

        _clock.UtcNow += MfaChallenge.Lifetime;
        var result = await _mfa.VerifyLoginAsync(challenge, Authenticator.CodeFor(secret, _clock.UtcNow), null, Client);

        Assert.True(result.ChallengeGone);
        Assert.Null(result.Token);
    }

    [Fact]
    public async Task Five_wrong_codes_end_the_challenge_even_from_different_addresses()
    {
        await SeedPlatformAdminAsync("admin@example.nl");
        var (secret, _, _) = await SetUpDuringLoginAsync("admin@example.nl");
        NextStep();
        var challenge = await ChallengeAsync("admin@example.nl");

        AuthResult last = null!;
        for (var i = 0; i < MfaChallenge.MaxFailedAttempts; i++)
        {
            last = await _mfa.VerifyLoginAsync(challenge, "000000", null, $"10.0.0.{i}");
        }

        var right = await _mfa.VerifyLoginAsync(challenge, Authenticator.CodeFor(secret, _clock.UtcNow), null, "10.0.1.1");

        Assert.True(last.ChallengeGone);
        Assert.Equal("Te veel foute codes. Log opnieuw in.", last.Error);
        Assert.True(right.ChallengeGone);
    }

    [Fact]
    public async Task Wrong_codes_count_towards_the_address_lockout_across_challenges()
    {
        await SeedPlatformAdminAsync("admin@example.nl");
        await SetUpDuringLoginAsync("admin@example.nl");

        for (var i = 0; i < LoginAttempts.MaxAttempts - 1; i++)
        {
            await _mfa.VerifyLoginAsync(await ChallengeAsync("admin@example.nl"), "000000", null, Client);
        }

        // The right password does not reset the count while the code is still to come.
        var fifth = await _mfa.VerifyLoginAsync(await ChallengeAsync("admin@example.nl"), "000000", null, Client);

        Assert.True(fifth.LockedOut);
        Assert.True((await _auth.LoginAsync("admin@example.nl", Password, Client)).LockedOut);
    }

    [Fact]
    public async Task Wrong_codes_from_many_addresses_lock_the_account()
    {
        // Each address and each challenge stays under its own limit; the account does not.
        await SeedPlatformAdminAsync("admin@example.nl");
        var (secret, _, _) = await SetUpDuringLoginAsync("admin@example.nl");
        NextStep();
        var earlier = await ChallengeAsync("admin@example.nl", "10.8.8.8");

        AuthResult last = null!;
        for (var i = 0; i < LoginAttempts.MaxAccountAttempts; i++)
        {
            var client = $"10.0.{i}.1";
            last = await _mfa.VerifyLoginAsync(await ChallengeAsync("admin@example.nl", client), "000000", null, client);
        }

        Assert.True(last.LockedOut);
        Assert.Equal("Te veel mislukte pogingen voor dit account. Wacht 15 minuten.", last.Error);

        // Neither the right code on a challenge from before, nor the password, gets in now.
        var right = await _mfa.VerifyLoginAsync(earlier, Authenticator.CodeFor(secret, _clock.UtcNow), null, "10.8.8.8");
        Assert.True(right.LockedOut);
        Assert.Null(right.Token);
        Assert.True((await _auth.LoginAsync("admin@example.nl", Password, "10.9.9.9")).LockedOut);

        // Once the lock has run out, a full login clears the count: one more mistake does not lock again.
        _clock.UtcNow += LoginAttempts.LockoutDuration + TimeSpan.FromMinutes(1);
        var session = await _mfa.VerifyLoginAsync(
            await ChallengeAsync("admin@example.nl", "10.9.9.9"), Authenticator.CodeFor(secret, _clock.UtcNow), null, "10.9.9.9");
        Assert.NotNull(session.Token);

        NextStep();
        var mistake = await _mfa.VerifyLoginAsync(await ChallengeAsync("admin@example.nl", "10.9.9.9"), "000000", null, "10.9.9.9");
        Assert.False(mistake.LockedOut);
        Assert.Equal("Ongeldige code.", mistake.Error);
    }

    [Fact]
    public async Task Wrong_codes_when_signed_in_count_against_the_account_too()
    {
        var user = await SeedAsync("piet@example.nl", UserRole.User);
        var setup = await _mfa.StartAccountSetupAsync("org-1", user.Id);

        MfaResult last = null!;
        for (var i = 0; i < LoginAttempts.MaxAccountAttempts; i++)
        {
            last = await _mfa.ConfirmAccountSetupAsync("org-1", user.Id, "000000", $"10.0.{i}.1");
        }

        Assert.True(last.LockedOut);
        Assert.True((await _mfa.ConfirmAccountSetupAsync(
            "org-1", user.Id, Authenticator.CodeFor(setup.Setup!.Secret, _clock.UtcNow), "10.9.9.9")).LockedOut);
        Assert.True((await _auth.LoginAsync("piet@example.nl", Password, "10.9.9.9")).LockedOut);
    }

    [Fact]
    public async Task A_challenge_is_used_once()
    {
        await SeedPlatformAdminAsync("admin@example.nl");
        var (secret, _, _) = await SetUpDuringLoginAsync("admin@example.nl");
        NextStep();
        var challenge = await ChallengeAsync("admin@example.nl");

        Assert.NotNull((await _mfa.VerifyLoginAsync(challenge, Authenticator.CodeFor(secret, _clock.UtcNow), null, Client)).Token);
        NextStep();
        var again = await _mfa.VerifyLoginAsync(challenge, Authenticator.CodeFor(secret, _clock.UtcNow), null, Client);

        Assert.True(again.ChallengeGone);
    }

    [Fact]
    public async Task A_setup_challenge_cannot_complete_a_code_login_and_the_other_way_round()
    {
        await SeedPlatformAdminAsync("admin@example.nl");
        var setupChallenge = await ChallengeAsync("admin@example.nl");

        Assert.True((await _mfa.VerifyLoginAsync(setupChallenge, "123456", null, Client)).ChallengeGone);

        await SetUpDuringLoginAsync("admin@example.nl");
        var codeChallenge = await ChallengeAsync("admin@example.nl");

        Assert.True((await _mfa.StartLoginSetupAsync(codeChallenge, Client)).ChallengeGone);
    }

    [Fact]
    public async Task An_unknown_challenge_counts_as_a_failed_attempt()
    {
        for (var i = 0; i < LoginAttempts.MaxAttempts; i++)
        {
            Assert.True((await _mfa.VerifyLoginAsync(new string('a', 64), "123456", null, Client)).ChallengeGone);
        }

        Assert.True((await _mfa.VerifyLoginAsync(new string('a', 64), "123456", null, Client)).LockedOut);
    }

    [Fact]
    public async Task A_recovery_code_signs_in_once_and_says_how_many_remain()
    {
        await SeedPlatformAdminAsync("admin@example.nl");
        var (_, codes, _) = await SetUpDuringLoginAsync("admin@example.nl");

        var first = await _mfa.VerifyLoginAsync(await ChallengeAsync("admin@example.nl"), null, codes[3].ToUpperInvariant(), Client);
        var reused = await _mfa.VerifyLoginAsync(await ChallengeAsync("admin@example.nl"), null, codes[3], Client);

        Assert.NotNull(first.Token);
        Assert.Equal(RecoveryCodes.Count - 1, first.RecoveryCodesLeft);
        Assert.False(reused.Succeeded);
        Assert.Equal("Ongeldige code.", reused.Error);
    }

    [Fact]
    public async Task A_platform_administrator_cannot_turn_mfa_off_and_keeps_the_recovery_code_offered()
    {
        var admin = await SeedPlatformAdminAsync("admin@example.nl");
        var (_, codes, _) = await SetUpDuringLoginAsync("admin@example.nl");

        var result = await _mfa.DisableAsync("org-1", admin.Id, codes[0], Client);

        Assert.False(result.Succeeded);
        Assert.Contains("verplicht", result.Error);
        Assert.Equal(new MfaStatus(true, true, RecoveryCodes.Count), await _mfa.GetStatusAsync("org-1", admin.Id));
    }

    [Fact]
    public async Task A_company_admin_signs_in_without_mfa_and_may_turn_it_on_and_off()
    {
        // The owner's rule of 2026-10-23: mandatory only for the platform administrator.
        var admin = await SeedAsync("baas@example.nl", UserRole.Admin);
        Assert.NotNull((await _auth.LoginAsync("baas@example.nl", Password, Client)).Token);
        Assert.Equal(new MfaStatus(false, false, 0), await _mfa.GetStatusAsync("org-1", admin.Id));

        var setup = await _mfa.StartAccountSetupAsync("org-1", admin.Id);
        var codes = (await _mfa.ConfirmAccountSetupAsync("org-1", admin.Id, Authenticator.CodeFor(setup.Setup!.Secret, _clock.UtcNow), Client)).RecoveryCodes!;
        Assert.False((await _auth.LoginAsync("baas@example.nl", Password, Client)).Mfa!.SetupRequired);

        var off = await _mfa.DisableAsync("org-1", admin.Id, codes[0], Client);

        Assert.True(off.Succeeded);
        Assert.Equal(new MfaStatus(false, false, 0), await _mfa.GetStatusAsync("org-1", admin.Id));
        Assert.NotNull((await _auth.LoginAsync("baas@example.nl", Password, "198.51.100.1")).Token);
    }

    [Fact]
    public async Task An_ordinary_user_turns_mfa_on_in_the_settings_and_off_again()
    {
        var user = await SeedAsync("piet@example.nl", UserRole.User);
        Assert.Equal(new MfaStatus(false, false, 0), await _mfa.GetStatusAsync("org-1", user.Id));

        var setup = await _mfa.StartAccountSetupAsync("org-1", user.Id);
        var confirmed = await _mfa.ConfirmAccountSetupAsync("org-1", user.Id, Authenticator.CodeFor(setup.Setup!.Secret, _clock.UtcNow), Client);

        Assert.Equal(RecoveryCodes.Count, confirmed.RecoveryCodes!.Count);
        Assert.Equal(new MfaStatus(true, false, RecoveryCodes.Count), await _mfa.GetStatusAsync("org-1", user.Id));
        Assert.False((await _auth.LoginAsync("piet@example.nl", Password, Client)).Mfa!.SetupRequired);

        NextStep();
        var wrong = await _mfa.DisableAsync("org-1", user.Id, "000000", Client);
        var off = await _mfa.DisableAsync("org-1", user.Id, Authenticator.CodeFor(setup.Setup.Secret, _clock.UtcNow), Client);

        Assert.False(wrong.Succeeded);
        Assert.True(off.Succeeded);
        Assert.NotNull((await _auth.LoginAsync("piet@example.nl", Password, "198.51.100.1")).Token);
    }

    [Fact]
    public async Task Someone_without_the_phone_turns_optional_mfa_off_with_a_recovery_code()
    {
        var user = await SeedAsync("piet@example.nl", UserRole.User);
        var setup = await _mfa.StartAccountSetupAsync("org-1", user.Id);
        var codes = (await _mfa.ConfirmAccountSetupAsync("org-1", user.Id, Authenticator.CodeFor(setup.Setup!.Secret, _clock.UtcNow), Client)).RecoveryCodes!;

        Assert.True((await _mfa.DisableAsync("org-1", user.Id, codes[9], Client)).Succeeded);
    }

    [Fact]
    public async Task Setting_up_twice_is_refused_while_mfa_is_on()
    {
        var user = await SeedAsync("piet@example.nl", UserRole.User);
        var setup = await _mfa.StartAccountSetupAsync("org-1", user.Id);
        await _mfa.ConfirmAccountSetupAsync("org-1", user.Id, Authenticator.CodeFor(setup.Setup!.Secret, _clock.UtcNow), Client);

        var again = await _mfa.StartAccountSetupAsync("org-1", user.Id);

        Assert.False(again.Succeeded);
        Assert.Equal("Tweestapsverificatie staat al aan.", again.Error);
    }

    [Fact]
    public async Task New_recovery_codes_replace_the_old_ones()
    {
        var admin = await SeedPlatformAdminAsync("admin@example.nl");
        var (secret, oldCodes, _) = await SetUpDuringLoginAsync("admin@example.nl");
        NextStep();

        var renewed = await _mfa.RegenerateRecoveryCodesAsync("org-1", admin.Id, Authenticator.CodeFor(secret, _clock.UtcNow), Client);

        Assert.Equal(RecoveryCodes.Count, renewed.RecoveryCodes!.Count);
        Assert.False((await _mfa.VerifyLoginAsync(await ChallengeAsync("admin@example.nl"), null, oldCodes[0], Client)).Succeeded);
        Assert.NotNull((await _mfa.VerifyLoginAsync(await ChallengeAsync("admin@example.nl"), null, renewed.RecoveryCodes[0], Client)).Token);
    }

    [Fact]
    public async Task The_platform_reset_turns_mfa_off_and_a_platform_administrator_sets_it_up_again_at_the_next_login()
    {
        var admin = await SeedPlatformAdminAsync("admin@example.nl");
        await SetUpDuringLoginAsync("admin@example.nl");

        var reset = await _mfa.ResetAsync("Admin@Example.nl");
        var unknown = await _mfa.ResetAsync("nobody@example.nl");

        Assert.True(reset.Succeeded);
        Assert.True(unknown.NotFound);
        Assert.Null(await _store.GetAsync("org-1", admin.Id));
        Assert.True((await _auth.LoginAsync("admin@example.nl", Password, Client)).Mfa!.SetupRequired);
    }

    [Fact]
    public async Task An_invited_admin_is_signed_in_without_an_mfa_step()
    {
        var platform = new PlatformService(_platform, _users, _clock);
        var created = await platform.CreateOrganizationAsync("platform-admin", "Klant Pte Ltd", "SG", "Jan", "jan@klant.sg");
        var session = await _auth.AcceptInvitationAsync(created.InvitationCode!, "Jan Tan", "a-long-enough-password", Client);

        Assert.Null(session.Mfa);
        Assert.NotNull(session.Token);
        Assert.Equal(created.Organization!.OrganizationId, session.User!.OrganizationId);
        Assert.Equal(UserRole.Admin, session.User.Role);
        Assert.Empty(_store.Challenges);
    }

    [Fact]
    public async Task A_session_from_before_mfa_stays_valid_until_it_ends()
    {
        // Enforcement happens at the next login, not by cutting live sessions.
        var admin = await SeedPlatformAdminAsync("admin@example.nl");
        await _users.SaveAsync(new AuthSession(AuthSession.HashOf("existing"), admin.Id, "org-1", _clock.UtcNow, _clock.UtcNow + AuthSession.Lifetime));

        Assert.NotNull(await _auth.ValidateAsync("existing"));
    }

    [Fact]
    public async Task A_deactivated_account_cannot_finish_the_second_step()
    {
        await SeedPlatformAdminAsync("admin@example.nl");
        var (secret, _, _) = await SetUpDuringLoginAsync("admin@example.nl");
        NextStep();
        var challenge = await ChallengeAsync("admin@example.nl");
        _users.Deactivate((await _users.FindByEmailAsync("admin@example.nl"))!.Id);

        var result = await _mfa.VerifyLoginAsync(challenge, Authenticator.CodeFor(secret, _clock.UtcNow), null, Client);

        Assert.Null(result.Token);
        Assert.Equal("Account gedeactiveerd.", result.Error);
    }
}
