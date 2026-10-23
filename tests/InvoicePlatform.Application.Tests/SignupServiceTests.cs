using Microsoft.Extensions.Logging.Abstractions;
using InvoicePlatform.Application.Identity;
using InvoicePlatform.Application.Interpretation;
using InvoicePlatform.Application.Invoices;
using InvoicePlatform.Application.Learning;
using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Application.Relations;
using InvoicePlatform.Application.Usage;
using InvoicePlatform.Domain.EInvoicing;
using InvoicePlatform.Domain.Identity;
using InvoicePlatform.Domain.Learning;
using InvoicePlatform.Domain.Localization;
using InvoicePlatform.Domain.Serialization;
using InvoicePlatform.Domain.Validation;

namespace InvoicePlatform.Application.Tests;

public class SignupServiceTests
{
    private const string Client = "203.0.113.7";

    private readonly AuthServiceTests.FakeStore _identity = new();
    private readonly FakePlatformStore _platform = new();
    private readonly FakeUsageMeter _usage = new();
    private readonly Signups _signups = new();
    private readonly Mailbox _mail = new();
    private readonly TestClock _clock = new(DateTimeOffset.Parse("2026-10-19T09:00:00Z"));
    private readonly AuthServiceTests.CountingHasher _hasher = new();
    private readonly SignupService _signup;

    public SignupServiceTests()
    {
        var auth = new AuthService(_identity, _identity, _identity, new AuthServiceTests.FakeHasher(), _clock, _platform, new FakeMfaStore());
        _signup = new SignupService(_signups, _identity, _identity, _hasher, _clock, _platform,
            _mail, _usage, auth, new SignupSettings("https://app.example.test"));
    }

    private Task Open() => _platform.SaveSettingsAsync(new PlatformSettings { SignupOpen = true });

    private Task<SignupResult> SignUp(string email = "anna@bakkerij.be", string client = Client)
        => _signup.StartAsync("Anna Peeters", email, "lang-genoeg-wachtwoord", "Bakkerij Peeters", "BE", client);

    /// <summary>The code from the last confirmation mail.</summary>
    private string CodeFromMail() => Uri.UnescapeDataString(_mail.Sent[^1].Body.Split("?code=")[1].Split('\n')[0].Trim());

    [Fact]
    public async Task Signup_is_closed_until_the_platform_administrator_opens_it()
    {
        var result = await SignUp();

        Assert.Equal(SignupOutcome.Closed, result.Outcome);
        Assert.Empty(_mail.Sent);
    }

    [Fact]
    public async Task Confirming_creates_an_organisation_on_trial_and_signs_its_admin_in_without_an_mfa_step()
    {
        await Open();

        Assert.Equal(SignupOutcome.MailSent, (await SignUp()).Outcome);
        Assert.Contains("https://app.example.test/signup/confirm/?code=", _mail.Sent[0].Body, StringComparison.Ordinal);
        var result = await _signup.ConfirmAsync(CodeFromMail(), Client);

        // Sign-up runs on to payment without an authenticator app: MFA is optional for a company admin.
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Token);
        Assert.Null(result.Mfa);
        Assert.Equal(UserRole.Admin, (await _identity.FindByEmailAsync("anna@bakkerij.be"))!.Role);
        var organization = Assert.Single(await _platform.ListOrganizationsAsync());
        Assert.Equal(("Bakkerij Peeters", "BE"), (organization.Name, organization.CountryCode));
        Assert.Equal(new UsageSummary(0, 1, Trial: true), await _usage.GetAsync(organization.OrganizationId));
    }

    [Fact]
    public async Task Nothing_exists_before_confirmation_and_a_link_works_once()
    {
        await Open();
        await SignUp();

        Assert.Empty(await _platform.ListOrganizationsAsync());
        var code = CodeFromMail();
        Assert.True((await _signup.ConfirmAsync(code, Client)).Succeeded);
        Assert.False((await _signup.ConfirmAsync(code, Client)).Succeeded);
    }

    [Fact]
    public async Task An_expired_link_creates_nothing()
    {
        await Open();
        await SignUp();
        _clock.UtcNow = _clock.UtcNow + PendingSignup.Lifetime + TimeSpan.FromMinutes(1);

        Assert.False((await _signup.ConfirmAsync(CodeFromMail(), Client)).Succeeded);
        Assert.Empty(await _platform.ListOrganizationsAsync());
    }

    [Fact]
    public async Task A_known_address_gets_the_same_answer_but_a_different_mail()
    {
        await Open();
        await SignUp();
        await _signup.ConfirmAsync(CodeFromMail(), Client);

        var again = await SignUp();

        Assert.Equal(SignupOutcome.MailSent, again.Outcome);
        Assert.Contains("al een xChange-account", _mail.Sent[^1].Subject, StringComparison.Ordinal);
        Assert.DoesNotContain("?code=", _mail.Sent[^1].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_known_address_costs_the_same_password_hash_as_a_new_one()
    {
        // Skipping bcrypt for a known address made that answer measurably faster.
        await Open();
        await SignUp();
        Assert.Equal(1, _hasher.Hashes);
        await _signup.ConfirmAsync(CodeFromMail(), Client);

        await SignUp();

        Assert.Equal(2, _hasher.Hashes);
    }

    [Fact]
    public async Task One_address_gets_at_most_three_signup_mails_an_hour_from_any_number_of_senders()
    {
        await Open();
        for (var i = 0; i < LoginAttempts.MaxSignupMails; i++)
        {
            Assert.Equal(SignupOutcome.MailSent, (await SignUp(client: $"10.0.{i}.1")).Outcome);
        }

        var fourth = await SignUp("Anna@Bakkerij.be ", "10.9.9.9");

        Assert.Equal(SignupOutcome.Locked, fourth.Outcome);
        Assert.Contains("te veel bevestigingsmails", fourth.Error, StringComparison.Ordinal);
        Assert.Equal(LoginAttempts.MaxSignupMails, _mail.Sent.Count);
        // Another address is not affected.
        Assert.Equal(SignupOutcome.MailSent, (await SignUp("piet@bakkerij.be", "10.9.9.9")).Outcome);

        _clock.UtcNow += LoginAttempts.SignupMailWindow;
        Assert.Equal(SignupOutcome.MailSent, (await SignUp(client: "10.9.9.8")).Outcome);
    }

    [Fact]
    public async Task The_mail_is_in_the_language_of_the_signup_request()
    {
        await Open();

        using (Texts.Use(Language.En))
        {
            await SignUp();
        }

        var mail = _mail.Sent[^1];
        Assert.Equal("Confirm your xChange sign-up", mail.Subject);
        Assert.StartsWith("Hello Anna Peeters,", mail.Body, StringComparison.Ordinal);
        Assert.Contains("for Bakkerij Peeters", mail.Body, StringComparison.Ordinal);
        Assert.Contains("You get 1 free conversion to try xChange.", mail.Body, StringComparison.Ordinal);
        Assert.Contains("Downloading the e-invoice requires a plan or purchased credits.", mail.Body, StringComparison.Ordinal);
        Assert.Contains("https://app.example.test/signup/confirm/?code=", mail.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Bevestig", mail.Body, StringComparison.Ordinal);

        // The link in an English mail works like any other.
        Assert.True((await _signup.ConfirmAsync(CodeFromMail(), Client)).Succeeded);
    }

    [Fact]
    public async Task Without_a_language_the_mail_stays_dutch()
    {
        await Open();
        await SignUp();

        Assert.Equal("Bevestig je aanmelding bij xChange", _mail.Sent[^1].Subject);
        Assert.StartsWith("Hallo Anna Peeters,", _mail.Sent[^1].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Repeated_signups_from_one_address_are_locked_out()
    {
        await Open();
        for (var i = 0; i < 5; i++)
        {
            await SignUp($"user{i}@example.test");
        }

        Assert.Equal(SignupOutcome.Locked, (await SignUp("one-more@example.test")).Outcome);
    }

    [Theory]
    [InlineData("", "lang-genoeg-wachtwoord", "Bakkerij", "BE")]
    [InlineData("anna@bakkerij.be", "kort", "Bakkerij", "BE")]
    [InlineData("anna@bakkerij.be", "lang-genoeg-wachtwoord", "", "BE")]
    [InlineData("anna@bakkerij.be", "lang-genoeg-wachtwoord", "Bakkerij", "US")]
    public async Task Incomplete_signups_are_refused_and_send_nothing(string email, string password, string company, string country)
    {
        await Open();

        var result = await _signup.StartAsync("Anna", email, password, company, country, Client);

        Assert.Equal(SignupOutcome.Invalid, result.Outcome);
        Assert.Empty(_mail.Sent);
    }

    private sealed class Signups : ISignupStore
    {
        private readonly Dictionary<string, PendingSignup> _byHash = [];

        public Task SaveAsync(PendingSignup signup, CancellationToken cancellationToken = default)
        {
            _byHash[signup.CodeHash] = signup;
            return Task.CompletedTask;
        }

        public Task<PendingSignup?> TryTakeAsync(string codeHash, CancellationToken cancellationToken = default)
            => Task.FromResult(_byHash.Remove(codeHash, out var signup) ? signup : null);
    }

    private sealed class Mailbox : IEmailSender
    {
        public List<(string To, string Subject, string Body)> Sent { get; } = [];

        public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
        {
            Sent.Add((to, subject, body));
            return Task.CompletedTask;
        }
    }
}

public class ConversionLimitTests
{
    private const string Org = "org-trial";

    private readonly FakeUsageMeter _usage = new();
    private readonly Reader _reader = new();
    private readonly ExtractInvoiceService _extract;

    public ConversionLimitTests()
    {
        var clock = new TestClock(DateTimeOffset.UnixEpoch);
        var relations = new RelationService(new FakeRelationStore(), clock, new InvoiceRulesetRegistry());
        _extract = new ExtractInvoiceService(_reader, new InvoiceEnrichment(new FakeSettingsStore(), relations),
            new InvoiceCalculationValidator(), new CanonicalInvoiceXmlSerializer(),
            new LearningService(new FakeLearningStore(), clock), new FakePlatformStore(), _usage, new FakeUsageLedger(), clock, NullLogger<ExtractInvoiceService>.Instance);
    }

    [Fact]
    public async Task A_trial_stops_at_its_limit_before_the_model_is_asked()
    {
        await _usage.SetLimitAsync(Org, 2);

        await _extract.ExtractAsync(Org, new byte[] { 1 }, "user-1", UsageSources.Upload);
        await _extract.ExtractAsync(Org, new byte[] { 1 }, "user-1", UsageSources.Upload);
        await Assert.ThrowsAsync<ConversionLimitReachedException>(() => _extract.ExtractAsync(Org, new byte[] { 1 }, "user-1", UsageSources.Upload));

        Assert.Equal(2, _reader.Calls);
        Assert.Equal(new UsageSummary(2, 2), await _usage.GetAsync(Org));
    }

    [Fact]
    public async Task A_failed_reading_gives_the_conversion_back()
    {
        await _usage.SetLimitAsync(Org, 1);
        _reader.Fail = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => _extract.ExtractAsync(Org, new byte[] { 1 }, "user-1", UsageSources.Upload));

        Assert.Equal(new UsageSummary(0, 1), await _usage.GetAsync(Org));
    }

    [Fact]
    public async Task An_organisation_without_a_limit_is_never_stopped()
    {
        for (var i = 0; i < 25; i++)
        {
            await _extract.ExtractAsync("org-own", new byte[] { 1 }, "user-1", UsageSources.Upload);
        }

        Assert.Equal(new UsageSummary(25, null), await _usage.GetAsync("org-own"));
    }

    private sealed class Reader : IInvoiceInterpreter
    {
        public int Calls { get; private set; }

        public bool Fail { get; set; }

        public Task<InvoiceInterpretationResult> InterpretAsync(
            ReadOnlyMemory<byte> pdf, IReadOnlyList<ReadingHint>? hints = null, string? model = null, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Fail
                ? throw new InvalidOperationException("unreadable")
                : Task.FromResult(new InvoiceInterpretationResult(CompleteInvoice.Create(), new ProviderExecution("test", "m", 0, TokenUsage.None)));
        }
    }
}
