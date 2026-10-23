using InvoicePlatform.Application.Billing;
using InvoicePlatform.Application.Interpretation;
using InvoicePlatform.Application.Invoices;
using InvoicePlatform.Application.Learning;
using InvoicePlatform.Application.Relations;
using InvoicePlatform.Application.Usage;
using InvoicePlatform.Domain.EInvoicing;
using InvoicePlatform.Domain.Learning;
using InvoicePlatform.Domain.Organizations;
using InvoicePlatform.Domain.Serialization;
using InvoicePlatform.Domain.Validation;
using Microsoft.Extensions.Logging.Abstractions;

namespace InvoicePlatform.Application.Tests;

public class ConversionRulesTests
{
    [Fact]
    public void The_month_goes_first_then_credits_then_the_trial()
    {
        Assert.Equal(ConversionBucket.Subscription, ConversionRules.Choose(new(PeriodAllowance: 50, PeriodUsed: 49, Credits: 5, Used: 0, Limit: 10, SubscribedOnce: true)));
        Assert.Equal(ConversionBucket.Credits, ConversionRules.Choose(new(PeriodAllowance: 50, PeriodUsed: 50, Credits: 5, Used: 0, Limit: 10, SubscribedOnce: true)));
        Assert.Equal(ConversionBucket.Base, ConversionRules.Choose(new(PeriodAllowance: 50, PeriodUsed: 50, Credits: 0, Used: 9, Limit: 10, SubscribedOnce: true)));
        Assert.Null(ConversionRules.Choose(new(PeriodAllowance: 50, PeriodUsed: 50, Credits: 0, Used: 10, Limit: 10, SubscribedOnce: true)));
    }

    [Fact]
    public void Without_a_limit_an_organisation_is_unlimited_until_it_subscribes()
    {
        Assert.Equal(ConversionBucket.Base, ConversionRules.Choose(new(null, 0, 0, Used: 5000, Limit: null, SubscribedOnce: false)));
        Assert.Null(ConversionRules.Choose(new(null, 0, 0, Used: 5000, Limit: null, SubscribedOnce: true)));
    }
}

public class BillingServiceTests
{
    private const string OrgA = "org-a";
    private const string OrgB = "org-b";

    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly FakeUsageMeter _store = new();
    private readonly FakeBillingProvider _provider = new();
    private readonly FakePlatformStore _platform = new();
    private readonly Reader _reader = new();
    private readonly BillingService _billing;
    private readonly ExtractInvoiceService _extract;

    public BillingServiceTests()
    {
        _billing = new BillingService(_store, _provider, _store, _platform, new BillingSettings("https://app.example.test"), NullLogger<BillingService>.Instance);

        var clock = new TestClock(T0);
        var relations = new RelationService(new FakeRelationStore(), clock, new InvoiceRulesetRegistry());
        _extract = new ExtractInvoiceService(_reader, new InvoiceEnrichment(new FakeSettingsStore(), relations),
            new InvoiceCalculationValidator(), new CanonicalInvoiceXmlSerializer(),
            new LearningService(new FakeLearningStore(), clock), new FakePlatformStore(), _store, new FakeUsageLedger(), clock, NullLogger<ExtractInvoiceService>.Instance);
    }

    // ---- consumption --------------------------------------------------------

    [Fact]
    public async Task Conversions_come_from_the_month_then_credits_and_are_then_refused()
    {
        await _store.SetLimitAsync(OrgA, 10, trial: true);
        await _store.LinkCustomerAsync(OrgA, "cus_a");
        await Webhook(Subscription("evt_1", "active", "xchange_starter_monthly"));
        _store[OrgA].PeriodAllowance = 2;
        _store[OrgA].Used = 10; // the trial is used up
        await Webhook(Checkout("evt_2", "cs_1", paid: true));

        for (var i = 0; i < 22; i++)
        {
            await Extract(OrgA);
        }

        Assert.Equal(2, _store[OrgA].PeriodUsed);
        Assert.Equal(0, _store[OrgA].Credits);
        await Assert.ThrowsAsync<ConversionLimitReachedException>(() => Extract(OrgA));
    }

    [Fact]
    public async Task A_failed_reading_gives_a_credit_back_as_a_credit()
    {
        await _store.SetLimitAsync(OrgA, 0, trial: true);
        await _store.LinkCustomerAsync(OrgA, "cus_a");
        await Webhook(Checkout("evt_1", "cs_1", paid: true));
        await Extract(OrgA);
        Assert.Equal(19, _store[OrgA].Credits);

        _reader.Fail = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Extract(OrgA));

        Assert.Equal(19, _store[OrgA].Credits);
        Assert.Equal(0, _store[OrgA].Used);
    }

    [Fact]
    public async Task A_failed_reading_gives_a_monthly_conversion_back_to_the_month()
    {
        await _store.LinkCustomerAsync(OrgA, "cus_a");
        await Webhook(Subscription("evt_1", "active", "xchange_pro_monthly"));
        await Webhook(Checkout("evt_2", "cs_1", paid: true));
        await Extract(OrgA);

        _reader.Fail = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Extract(OrgA));

        Assert.Equal(1, _store[OrgA].PeriodUsed);
        Assert.Equal(20, _store[OrgA].Credits);
    }

    [Fact]
    public async Task An_unlimited_organisation_stays_unlimited_until_it_subscribes()
    {
        for (var i = 0; i < 30; i++)
        {
            await Extract(OrgA);
        }

        await _store.LinkCustomerAsync(OrgA, "cus_a");
        await Webhook(Subscription("evt_1", "active", "xchange_starter_monthly"));
        _store[OrgA].PeriodUsed = 50;

        await Assert.ThrowsAsync<ConversionLimitReachedException>(() => Extract(OrgA));
    }

    // ---- trial --------------------------------------------------------------

    [Fact]
    public async Task Buying_credits_ends_the_trial()
    {
        await _store.SetLimitAsync(OrgA, 10, trial: true);
        await _store.LinkCustomerAsync(OrgA, "cus_a");
        Assert.True((await _store.GetAsync(OrgA)).Trial);

        await Webhook(Checkout("evt_1", "cs_1", paid: true));

        Assert.False((await _store.GetAsync(OrgA)).Trial);
    }

    [Fact]
    public async Task A_plan_ends_the_trial_and_it_stays_ended_after_cancelling()
    {
        await _store.SetLimitAsync(OrgA, 10, trial: true);
        await _store.LinkCustomerAsync(OrgA, "cus_a");

        await Webhook(Subscription("evt_1", "active", "xchange_pro_monthly"));
        Assert.False((await _store.GetAsync(OrgA)).Trial);

        await Webhook(Subscription("evt_2", "canceled", "xchange_pro_monthly", created: T0.AddDays(40)));
        Assert.False((await _store.GetAsync(OrgA)).Trial);
    }

    [Fact]
    public async Task An_unpaid_one_off_checkout_gives_no_credits_until_its_payment_succeeds()
    {
        await _store.SetLimitAsync(OrgA, 10, trial: true);
        await _store.LinkCustomerAsync(OrgA, "cus_a");

        await Webhook(Checkout("evt_1", "cs_1", paid: false));
        Assert.Equal(0, _store[OrgA].Credits);
        Assert.True((await _store.GetAsync(OrgA)).Trial);

        await Webhook(Checkout("evt_2", "cs_1", paid: true));
        Assert.Equal(20, _store[OrgA].Credits);
    }

    // ---- webhooks -----------------------------------------------------------

    [Fact]
    public async Task Subscription_events_set_change_and_end_the_plan_and_credits_survive()
    {
        await _store.LinkCustomerAsync(OrgA, "cus_a");
        await Webhook(Checkout("evt_0", "cs_1", paid: true));

        await Webhook(Subscription("evt_1", "active", "xchange_starter_monthly"));
        var account = (await _billing.GetStatusAsync(OrgA)).Account;
        Assert.Equal(("starter", 50, "active"), (account.PlanId, account.PeriodAllowance, account.Status));
        Assert.Equal(T0, account.PeriodStart);
        Assert.Equal(T0.AddMonths(1), account.PeriodEnd);

        await Webhook(Subscription("evt_2", "active", "xchange_business_monthly", created: T0.AddDays(3)));
        account = (await _billing.GetStatusAsync(OrgA)).Account;
        Assert.Equal(("business", 1000), (account.PlanId, account.PeriodAllowance));

        await Webhook(Subscription("evt_3", "canceled", "xchange_business_monthly", created: T0.AddDays(5)));
        account = (await _billing.GetStatusAsync(OrgA)).Account;
        Assert.Null(account.PlanId);
        Assert.Null(account.PeriodAllowance);
        Assert.Equal("canceled", account.Status);
        Assert.Equal(20, account.Credits);
    }

    [Fact]
    public async Task An_unpaid_subscription_gives_no_conversions()
    {
        await _store.LinkCustomerAsync(OrgA, "cus_a");
        await Webhook(Subscription("evt_1", "active", "xchange_pro_monthly"));

        await Webhook(Subscription("evt_2", "unpaid", "xchange_pro_monthly", created: T0.AddDays(20)));

        Assert.Null(_store[OrgA].PeriodAllowance);
    }

    [Fact]
    public async Task A_failed_payment_marks_past_due_and_keeps_serving()
    {
        await _store.LinkCustomerAsync(OrgA, "cus_a");
        await Webhook(Subscription("evt_1", "active", "xchange_pro_monthly"));

        await Webhook(new InvoicePaymentFailed("evt_2", T0.AddDays(31), "cus_a", "sub_1", null));

        Assert.Equal(SubscriptionStatuses.PastDue, _store[OrgA].Status);
        Assert.Equal(250, _store[OrgA].PeriodAllowance);
        await Extract(OrgA);
        Assert.Equal(1, _store[OrgA].PeriodUsed);
    }

    [Fact]
    public async Task An_older_subscription_event_arriving_late_changes_nothing()
    {
        await _store.LinkCustomerAsync(OrgA, "cus_a");
        await Webhook(Subscription("evt_2", "active", "xchange_business_monthly", created: T0.AddDays(3)));

        var outcome = await Webhook(Subscription("evt_1", "active", "xchange_starter_monthly", created: T0));

        Assert.Equal(WebhookOutcome.Ignored, outcome);
        Assert.Equal("business", _store[OrgA].PlanId);
    }

    [Fact]
    public async Task A_renewal_starts_the_month_again_once()
    {
        await _store.LinkCustomerAsync(OrgA, "cus_a");
        await Webhook(Subscription("evt_1", "active", "xchange_starter_monthly"));
        _store[OrgA].PeriodUsed = 37;

        await Webhook(new InvoicePaid("evt_2", T0.AddMonths(1), "cus_a", "sub_1", "subscription_cycle", T0.AddMonths(1), null));
        Assert.Equal(0, _store[OrgA].PeriodUsed);

        // A plan change's proration is no new period; neither is the same period again.
        _store[OrgA].PeriodUsed = 4;
        await Webhook(new InvoicePaid("evt_3", T0.AddMonths(1).AddDays(2), "cus_a", "sub_1", "subscription_update", T0.AddMonths(1).AddDays(2), null));
        await Webhook(new InvoicePaid("evt_4", T0.AddMonths(1).AddDays(2), "cus_a", "sub_1", "subscription_cycle", T0.AddMonths(1), null));
        Assert.Equal(4, _store[OrgA].PeriodUsed);
    }

    [Fact]
    public async Task A_retried_event_counts_once()
    {
        await _store.LinkCustomerAsync(OrgA, "cus_a");

        Assert.Equal(WebhookOutcome.Applied, await Webhook(Checkout("evt_1", "cs_1", paid: true)));
        Assert.Equal(WebhookOutcome.Duplicate, await Webhook(Checkout("evt_1", "cs_1", paid: true)));

        // "completed" and "async payment succeeded" of one purchase: credited once.
        Assert.Equal(WebhookOutcome.Duplicate, await Webhook(Checkout("evt_9", "cs_1", paid: true)));

        Assert.Equal(20, _store[OrgA].Credits);
    }

    [Fact]
    public async Task An_event_goes_to_the_organisation_of_its_customer_not_its_metadata()
    {
        await _store.LinkCustomerAsync(OrgA, "cus_a");

        await Webhook(Checkout("evt_1", "cs_1", paid: true) with { OrganizationId = OrgB });

        Assert.Equal(20, _store[OrgA].Credits);
        Assert.Equal(0, _store[OrgB].Credits);
    }

    [Fact]
    public async Task Without_a_linked_customer_the_metadata_names_the_organisation_and_links_it()
    {
        await Webhook(Checkout("evt_1", "cs_1", paid: true) with { CustomerId = "cus_new", OrganizationId = OrgB });

        Assert.Equal(20, _store[OrgB].Credits);
        Assert.Equal(OrgB, await _store.FindOrganizationByCustomerAsync("cus_new"));
    }

    [Fact]
    public async Task An_event_naming_no_known_organisation_is_ignored()
    {
        var outcome = await Webhook(Checkout("evt_1", "cs_1", paid: true) with { CustomerId = "cus_stranger", OrganizationId = null });

        Assert.Equal(WebhookOutcome.Ignored, outcome);
        Assert.Equal(0, _store[OrgA].Credits);
    }

    [Fact]
    public async Task Unknown_events_are_ignored()
    {
        Assert.Equal(WebhookOutcome.Ignored, await Webhook(new OtherBillingEvent("evt_1", T0, "customer.created")));
    }

    [Fact]
    public async Task Without_a_webhook_secret_or_with_a_bad_signature_nothing_is_applied()
    {
        _provider.Parse = WebhookParseResult.NotConfigured;
        Assert.Equal(WebhookOutcome.NotConfigured, await _billing.HandleWebhookAsync("{}", "t=1,v1=x"));

        _provider.Parse = WebhookParseResult.InvalidSignature;
        Assert.Equal(WebhookOutcome.InvalidSignature, await _billing.HandleWebhookAsync("{}", "t=1,v1=x"));
    }

    // ---- checkout and portal ------------------------------------------------

    [Fact]
    public async Task Checkout_for_a_plan_creates_one_customer_and_reuses_it()
    {
        await _platform.SaveOrganizationAsync(new Organization { OrganizationId = OrgA, Name = "Acme", CountryCode = "NL", CreatedAt = T0 });

        var first = await _billing.StartCheckoutAsync(OrgA, "admin@a", "pro", credits: false);
        var second = await _billing.StartCheckoutAsync(OrgA, "admin@a", null, credits: true);

        Assert.Equal(CheckoutOutcome.Started, first.Outcome);
        Assert.Equal(CheckoutOutcome.Started, second.Outcome);
        Assert.Single(_provider.Customers);
        Assert.Equal(("org-a", "Acme", "admin@a"), _provider.Customers[0]);

        var (plan, credits) = (_provider.Checkouts[0], _provider.Checkouts[1]);
        Assert.Equal((OrgA, "cus_1", "xchange_pro_monthly", CheckoutMode.Subscription), (plan.OrganizationId, plan.CustomerId, plan.LookupKey, plan.Mode));
        Assert.Equal(("xchange_credits_20", CheckoutMode.Payment), (credits.LookupKey, credits.Mode));
        Assert.Equal("https://app.example.test/settings/?billing=success", plan.SuccessUrl);
        Assert.Equal(OrgA, await _store.FindOrganizationByCustomerAsync("cus_1"));
    }

    [Fact]
    public async Task A_second_plan_is_refused_while_one_runs_but_credits_are_not()
    {
        await _store.LinkCustomerAsync(OrgA, "cus_a");
        await Webhook(Subscription("evt_1", "active", "xchange_pro_monthly"));

        Assert.Equal(CheckoutOutcome.AlreadySubscribed, (await _billing.StartCheckoutAsync(OrgA, "admin@a", "business", credits: false)).Outcome);
        Assert.Equal(CheckoutOutcome.Started, (await _billing.StartCheckoutAsync(OrgA, "admin@a", null, credits: true)).Outcome);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("pro", true)]
    [InlineData("platinum", false)]
    public async Task Checkout_needs_exactly_one_known_thing_to_buy(string? plan, bool credits)
    {
        Assert.Equal(CheckoutOutcome.Invalid, (await _billing.StartCheckoutAsync(OrgA, "admin@a", plan, credits)).Outcome);
        Assert.Empty(_provider.Checkouts);
    }

    [Fact]
    public async Task Without_payments_set_up_checkout_says_so()
    {
        _provider.Unavailable = true;

        Assert.Equal(CheckoutOutcome.Unavailable, (await _billing.StartCheckoutAsync(OrgA, "admin@a", "pro", credits: false)).Outcome);
        Assert.Null(await _billing.GetPlansAsync());
    }

    [Fact]
    public async Task The_portal_needs_a_customer()
    {
        Assert.Equal(CheckoutOutcome.NoCustomer, (await _billing.StartPortalAsync(OrgA)).Outcome);

        await _store.LinkCustomerAsync(OrgA, "cus_a");
        var portal = await _billing.StartPortalAsync(OrgA);

        Assert.Equal(CheckoutOutcome.Started, portal.Outcome);
        Assert.Equal(("cus_a", "https://app.example.test/settings/"), _provider.Portals.Single());
    }

    [Fact]
    public async Task Plans_carry_the_providers_prices_by_lookup_key()
    {
        var offer = (await _billing.GetPlansAsync())!;

        Assert.Equal(new[] { "starter", "pro", "business" }, offer.Plans.Select(p => p.Plan.Id));
        Assert.Equal(49.00m, offer.Plans[1].Price!.Amount);
        Assert.Equal(20, offer.Credits.Credits);
        Assert.Equal(30.00m, offer.CreditsPrice!.Amount);
    }

    // ---- helpers ------------------------------------------------------------

    private Task<WebhookOutcome> Webhook(BillingEvent e)
    {
        _provider.Parse = new WebhookParseResult(WebhookParseStatus.Parsed, e);
        return _billing.HandleWebhookAsync("{}", "t=1,v1=signed");
    }

    private Task<ExtractInvoiceResult> Extract(string organizationId)
        => _extract.ExtractAsync(organizationId, new byte[] { 1 }, "user-1", UsageSources.Upload);

    private static SubscriptionChanged Subscription(string eventId, string status, string lookupKey, DateTimeOffset? created = null)
        => new(eventId, created ?? T0, "cus_a", "sub_1", status, lookupKey, T0, T0.AddMonths(1), false, null);

    private static CheckoutCompleted Checkout(string eventId, string sessionId, bool paid)
        => new(eventId, T0, sessionId, "cus_a", OrgA, OneOffPayment: true, Paid: paid, LookupKey: BillingCatalogue.Credits.LookupKey);

    private sealed class Reader : IInvoiceInterpreter
    {
        public bool Fail { get; set; }

        public Task<InvoiceInterpretationResult> InterpretAsync(
            ReadOnlyMemory<byte> pdf, IReadOnlyList<ReadingHint>? hints = null, string? model = null, CancellationToken cancellationToken = default)
            => Fail
                ? throw new InvalidOperationException("unreadable")
                : Task.FromResult(new InvoiceInterpretationResult(CompleteInvoice.Create(), new ProviderExecution("test", "m", 0, TokenUsage.None)));
    }
}

/// <summary>Stripe stand-in: records what was asked, hands out fixed prices and whatever event is queued.</summary>
internal sealed class FakeBillingProvider : IBillingProvider
{
    public List<(string OrganizationId, string Name, string Email)> Customers { get; } = [];

    public List<CheckoutSessionRequest> Checkouts { get; } = [];

    public List<(string CustomerId, string ReturnUrl)> Portals { get; } = [];

    public WebhookParseResult Parse { get; set; } = WebhookParseResult.NotConfigured;

    public bool Unavailable { get; set; }

    public Task<string> CreateCustomerAsync(string organizationId, string organizationName, string email, CancellationToken cancellationToken = default)
    {
        Guard();
        Customers.Add((organizationId, organizationName, email));
        return Task.FromResult($"cus_{Customers.Count}");
    }

    public Task<string> CreateCheckoutSessionAsync(CheckoutSessionRequest request, CancellationToken cancellationToken = default)
    {
        Guard();
        Checkouts.Add(request);
        return Task.FromResult($"https://checkout.example.test/{Checkouts.Count}");
    }

    public Task<string> CreatePortalSessionAsync(string customerId, string returnUrl, CancellationToken cancellationToken = default)
    {
        Guard();
        Portals.Add((customerId, returnUrl));
        return Task.FromResult("https://portal.example.test/");
    }

    public Task<IReadOnlyList<BillingPrice>> GetPricesAsync(CancellationToken cancellationToken = default)
    {
        Guard();
        return Task.FromResult<IReadOnlyList<BillingPrice>>(
        [
            new("xchange_starter_monthly", 19.00m, "EUR"),
            new("xchange_pro_monthly", 49.00m, "EUR"),
            new("xchange_business_monthly", 129.00m, "EUR"),
            new("xchange_credits_20", 30.00m, "EUR"),
        ]);
    }

    public Task<WebhookParseResult> ParseWebhookAsync(string payload, string? signatureHeader, CancellationToken cancellationToken = default)
        => Task.FromResult(Parse);

    private void Guard()
    {
        if (Unavailable)
        {
            throw new BillingUnavailableException("not configured");
        }
    }
}
