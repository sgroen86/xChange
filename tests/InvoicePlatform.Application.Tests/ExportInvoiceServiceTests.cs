using System.Text;
using InvoicePlatform.Application.Billing;
using InvoicePlatform.Application.Invoices;
using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Application.Relations;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.EInvoicing;
using InvoicePlatform.Domain.EInvoicing.Peppol;
using InvoicePlatform.Domain.EInvoicing.Ubl;
using InvoicePlatform.Domain.Invoices;

namespace InvoicePlatform.Application.Tests;

public class ExportInvoiceServiceTests
{
    private const string OrgA = "org-a";
    private const string OrgB = "org-b";

    private readonly FakeInvoiceStore _invoices = new();
    private readonly FakeSettingsStore _settings = new();
    private readonly FakeOfficialValidator _official = new();
    private readonly FakePlatformStore _platform = new();
    private readonly FakeUsageMeter _usage = new();
    private readonly FakeRelationStore _relations = new();
    private readonly ExportInvoiceService _service;

    public ExportInvoiceServiceTests()
    {
        var registry = new InvoiceRulesetRegistry([new PeppolBisBilling30Ruleset(), new RenamedPeppol()]);
        var relations = new RelationService(_relations, new TestClock(DateTimeOffset.UnixEpoch), registry);
        _service = new ExportInvoiceService(_invoices, _settings, registry, new InvoiceEnrichment(_settings, relations), new UblInvoiceWriter(), _official, _platform, _usage, _usage);
    }

    private async Task<string> Book(CanonicalInvoiceDraft draft, string organizationId = OrgA)
    {
        var id = Guid.NewGuid().ToString("n");
        await _invoices.SaveAsync(new StoredInvoice
        {
            InvoiceId = id,
            OrganizationId = organizationId,
            Status = InvoiceStatus.Booked,
            Invoice = draft,
            CreatedAt = DateTimeOffset.UnixEpoch,
            CreatedByUserId = "u",
        });
        return id;
    }

    [Fact]
    public async Task A_trial_checks_everything_but_cannot_download_the_file()
    {
        await _usage.SetLimitAsync(OrgA, 1, trial: true);
        var id = await Book(CompleteInvoice.Create());

        var outcome = await _service.ExportAsync(OrgA, id, null);
        var check = await _service.CheckAsync(OrgA, PeppolBisBilling30Ruleset.RulesetId, CompleteInvoice.Create());

        Assert.Equal(ExportOutcomeKind.PlanRequired, outcome.Kind);
        Assert.Null(outcome.Content);
        Assert.True(check.RulesetFound);
        Assert.Empty(check.Violations);
    }

    [Theory]
    [InlineData(SubscriptionStatuses.Active)]
    [InlineData(SubscriptionStatuses.Trialing)]
    [InlineData(SubscriptionStatuses.PastDue)]
    public async Task A_running_subscription_downloads(string status)
    {
        await _usage.SetLimitAsync(OrgA, 1, trial: true);
        _usage[OrgA].Status = status;
        var id = await Book(CompleteInvoice.Create());

        Assert.Equal(ExportOutcomeKind.Exported, (await _service.ExportAsync(OrgA, id, null)).Kind);
    }

    [Fact]
    public async Task Purchased_credits_download_and_used_up_credits_do_not()
    {
        await _usage.SetLimitAsync(OrgA, 1, trial: true);
        _usage[OrgA].Credits = 1;
        _usage[OrgA].PaidOnce = true;
        var id = await Book(CompleteInvoice.Create());

        Assert.Equal(ExportOutcomeKind.Exported, (await _service.ExportAsync(OrgA, id, null)).Kind);

        _usage[OrgA].Credits = 0;
        Assert.Equal(ExportOutcomeKind.PlanRequired, (await _service.ExportAsync(OrgA, id, null)).Kind);
    }

    [Theory]
    [InlineData("canceled")]
    [InlineData(SubscriptionStatuses.Unpaid)]
    public async Task An_ended_subscription_without_credits_stops_downloading(string status)
    {
        await _usage.SetLimitAsync(OrgA, 1, trial: true);
        _usage[OrgA].Status = status;
        _usage[OrgA].SubscribedOnce = true;
        _usage[OrgA].PaidOnce = true;
        var id = await Book(CompleteInvoice.Create());

        Assert.Equal(ExportOutcomeKind.PlanRequired, (await _service.ExportAsync(OrgA, id, null)).Kind);
    }

    [Fact]
    public async Task A_complete_invoice_is_exported_as_ubl_with_a_safe_file_name()
    {
        var id = await Book(CompleteInvoice.Create("F-2026/041"));

        var outcome = await _service.ExportAsync(OrgA, id, requestedRulesetId: null);

        Assert.Equal(ExportOutcomeKind.Exported, outcome.Kind);
        Assert.Equal("F-2026-041-peppol-bis-billing-3.0.xml", outcome.FileName);
        var xml = Encoding.UTF8.GetString(outcome.Content!);
        Assert.StartsWith("<?xml", xml, StringComparison.Ordinal);
        Assert.Contains("<cbc:ID>F-2026/041</cbc:ID>", xml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_singapore_organisation_exports_pint_sg_by_default_with_a_stable_uuid()
    {
        await _platform.SaveOrganizationAsync(new InvoicePlatform.Domain.Organizations.Organization
        {
            OrganizationId = OrgA,
            Name = "Green IT Solutions Pte. Ltd.",
            CountryCode = "SG",
            CreatedAt = DateTimeOffset.UnixEpoch,
        });
        var service = new ExportInvoiceService(
            _invoices, _settings, new InvoiceRulesetRegistry(), new InvoiceEnrichment(_settings, new RelationService(new FakeRelationStore(), new TestClock(DateTimeOffset.UnixEpoch), new InvoiceRulesetRegistry())),
            new UblInvoiceWriter(), _official, _platform, _usage, _usage);
        var id = await Book(CompleteInvoice.Singapore());

        var first = await service.ExportAsync(OrgA, id, null);
        var again = await service.ExportAsync(OrgA, id, null);

        Assert.Equal(ExportOutcomeKind.Exported, first.Kind);
        Assert.EndsWith("-pint-sg-billing-1.xml", first.FileName, StringComparison.Ordinal);
        var xml = Encoding.UTF8.GetString(first.Content!);
        Assert.Contains("<cbc:CustomizationID>urn:peppol:pint:billing-1@sg-1</cbc:CustomizationID>", xml, StringComparison.Ordinal);
        Assert.Contains($"<cbc:UUID>{DocumentUuids.For(OrgA, id)}</cbc:UUID>", xml, StringComparison.Ordinal);
        Assert.Equal(xml, Encoding.UTF8.GetString(again.Content!));
    }

    [Fact]
    public async Task A_fatal_official_failure_blocks_the_file()
    {
        _official.Verdict = new OfficialValidation(true, "3.0.21",
            [new OfficialFailure("fatal", "BR-CO-16", "Amount due for payment ...", "/Invoice[1]/LegalMonetaryTotal[1]")]);
        var id = await Book(CompleteInvoice.Create());

        var outcome = await _service.ExportAsync(OrgA, id, null);

        Assert.Equal(ExportOutcomeKind.Incomplete, outcome.Kind);
        var violation = Assert.Single(outcome.Violations!);
        Assert.Equal(("BR-CO-16", "/Invoice[1]/LegalMonetaryTotal[1]"), (violation.RuleId, violation.Path));
        Assert.Null(outcome.Content);
    }

    [Fact]
    public async Task Official_warnings_come_with_the_file_without_blocking_it()
    {
        _official.Verdict = new OfficialValidation(true, "3.0.21",
            [new OfficialFailure("warning", "PEPPOL-COMMON-R054", "KVK format", "/Invoice[1]")]);
        var id = await Book(CompleteInvoice.Create());

        var outcome = await _service.ExportAsync(OrgA, id, null);

        Assert.Equal(ExportOutcomeKind.Exported, outcome.Kind);
        Assert.Equal("PEPPOL-COMMON-R054", Assert.Single(outcome.Warnings!).RuleId);
    }

    [Fact]
    public async Task No_verdict_means_no_file()
    {
        _official.Verdict = OfficialValidation.Unavailable("down");
        var id = await Book(CompleteInvoice.Create());

        var outcome = await _service.ExportAsync(OrgA, id, null);

        Assert.Equal(ExportOutcomeKind.ValidationUnavailable, outcome.Kind);
        Assert.Null(outcome.Content);
    }

    [Fact]
    public async Task An_invoice_the_runtime_rules_refuse_is_never_sent_to_the_official_validator()
    {
        var id = await Book(CompleteInvoice.Create() with { InvoiceNumber = null });

        await _service.ExportAsync(OrgA, id, null);

        Assert.Equal(0, _official.Calls);
    }

    [Fact]
    public async Task An_incomplete_invoice_is_refused_with_its_violations()
    {
        var id = await Book(CompleteInvoice.Create() with { BuyerReference = null });

        var outcome = await _service.ExportAsync(OrgA, id, null);

        Assert.Equal(ExportOutcomeKind.Incomplete, outcome.Kind);
        Assert.Contains(outcome.Violations!, v => v.RuleId == "PEPPOL-EN16931-R003");
        Assert.Null(outcome.Content);
    }

    [Fact]
    public async Task Another_organisations_invoice_is_not_found()
    {
        var id = await Book(CompleteInvoice.Create(), OrgA);

        var outcome = await _service.ExportAsync(OrgB, id, null);

        Assert.Equal(ExportOutcomeKind.NotFound, outcome.Kind);
    }

    [Fact]
    public async Task The_organisation_default_is_used_when_nothing_is_requested()
    {
        await _settings.SaveAsync(new OrganizationSettings { OrganizationId = OrgA, DefaultRulesetId = "renamed" });
        var id = await Book(CompleteInvoice.Create());

        var outcome = await _service.ExportAsync(OrgA, id, null);

        Assert.EndsWith("-renamed.xml", outcome.FileName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_requested_ruleset_wins_over_the_organisation_default()
    {
        await _settings.SaveAsync(new OrganizationSettings { OrganizationId = OrgA, DefaultRulesetId = "renamed" });
        var id = await Book(CompleteInvoice.Create());

        var outcome = await _service.ExportAsync(OrgA, id, PeppolBisBilling30Ruleset.RulesetId);

        Assert.EndsWith("-peppol-bis-billing-3.0.xml", outcome.FileName, StringComparison.Ordinal);
    }

    private Task Customer(string? defaultRulesetId)
        => _relations.SaveAsync(new InvoicePlatform.Domain.Relations.Relation
        {
            RelationId = "r1",
            OrganizationId = OrgA,
            VatIdentifier = "NL814912345B01",
            DefaultRulesetId = defaultRulesetId,
            UpdatedAt = DateTimeOffset.UnixEpoch,
        });

    [Fact]
    public async Task The_customers_format_wins_over_the_organisation_default()
    {
        await _settings.SaveAsync(new OrganizationSettings { OrganizationId = OrgA, DefaultRulesetId = PeppolBisBilling30Ruleset.RulesetId });
        await Customer("renamed");
        var id = await Book(CompleteInvoice.Create());

        var outcome = await _service.ExportAsync(OrgA, id, null);

        Assert.EndsWith("-renamed.xml", outcome.FileName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_requested_ruleset_wins_over_the_customers_format()
    {
        await Customer("renamed");
        var id = await Book(CompleteInvoice.Create());

        var outcome = await _service.ExportAsync(OrgA, id, PeppolBisBilling30Ruleset.RulesetId);

        Assert.EndsWith("-peppol-bis-billing-3.0.xml", outcome.FileName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_customers_format_that_no_longer_exists_falls_back_to_the_organisation()
    {
        await _settings.SaveAsync(new OrganizationSettings { OrganizationId = OrgA, DefaultRulesetId = "renamed" });
        await Customer("xrechnung-9");
        var id = await Book(CompleteInvoice.Create());

        var outcome = await _service.ExportAsync(OrgA, id, null);

        Assert.EndsWith("-renamed.xml", outcome.FileName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_requested_ruleset_is_reported_not_replaced()
    {
        var id = await Book(CompleteInvoice.Create());

        var outcome = await _service.ExportAsync(OrgA, id, "xrechnung-9");

        Assert.Equal(ExportOutcomeKind.UnknownRuleset, outcome.Kind);
    }

    [Fact]
    public async Task The_organisations_endpoint_completes_an_invoice_booked_without_one()
    {
        await _settings.SaveAsync(new OrganizationSettings { OrganizationId = OrgA, EndpointId = "12345678", EndpointScheme = "0106" });
        var draft = CompleteInvoice.Create();
        var id = await Book(draft with { Seller = draft.Seller! with { ElectronicAddress = null, ElectronicAddressScheme = null } });

        var outcome = await _service.ExportAsync(OrgA, id, null);

        Assert.Equal(ExportOutcomeKind.Exported, outcome.Kind);
    }

    [Fact]
    public async Task Checking_reports_violations_and_stores_nothing()
    {
        var outcome = await _service.CheckAsync(OrgA, PeppolBisBilling30Ruleset.RulesetId, new CanonicalInvoiceDraft());

        Assert.True(outcome.RulesetFound);
        Assert.Contains(outcome.Violations, v => v.RuleId == "BR-02");
        Assert.Empty(await _invoices.ListAsync(OrgA));
    }

    [Fact]
    public async Task Checking_against_an_unknown_ruleset_says_so()
    {
        var outcome = await _service.CheckAsync(OrgA, "xrechnung-9", CompleteInvoice.Create());

        Assert.False(outcome.RulesetFound);
    }

    /// <summary>The PEPPOL rules under another id, to tell which ruleset was chosen.</summary>
    private sealed class RenamedPeppol : IInvoiceRuleset
    {
        private readonly PeppolBisBilling30Ruleset _inner = new();

        public string Id => "renamed";
        public string DisplayName => "Renamed";
        public string RulesRelease => _inner.RulesRelease;
        public InvoiceSyntax Syntax => _inner.Syntax;
        public string CustomizationId => _inner.CustomizationId;
        public string ProfileId => _inner.ProfileId;
        public string TaxName => _inner.TaxName;
        public IReadOnlyList<TaxCategory> TaxCategories => _inner.TaxCategories;
        public string? TaxCurrency => _inner.TaxCurrency;
        public UblDialect Ubl => _inner.Ubl;
        public CanonicalInvoiceDraft Prepare(CanonicalInvoiceDraft draft) => _inner.Prepare(draft);
        public IReadOnlyList<RuleViolation> Check(CanonicalInvoiceDraft invoice) => _inner.Check(invoice);
    }
}
