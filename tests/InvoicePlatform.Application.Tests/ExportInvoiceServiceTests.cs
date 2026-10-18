using System.Text;
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
    private readonly ExportInvoiceService _service;

    public ExportInvoiceServiceTests()
    {
        var registry = new InvoiceRulesetRegistry([new PeppolBisBilling30Ruleset(), new RenamedPeppol()]);
        _service = new ExportInvoiceService(_invoices, _settings, registry, new InvoiceEnrichment(_settings, new RelationService(new FakeRelationStore(), new TestClock(DateTimeOffset.UnixEpoch))), new UblInvoiceWriter());
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
        public IReadOnlyList<RuleViolation> Check(CanonicalInvoiceDraft invoice) => _inner.Check(invoice);
    }
}
