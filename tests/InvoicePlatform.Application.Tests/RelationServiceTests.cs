using InvoicePlatform.Application.Invoices;
using InvoicePlatform.Application.Relations;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.Validation;

namespace InvoicePlatform.Application.Tests;

public class RelationServiceTests
{
    private const string OrgA = "org-a";
    private const string OrgB = "org-b";

    private readonly FakeRelationStore _store = new();
    private readonly TestClock _clock = new(DateTimeOffset.Parse("2026-10-18T09:00:00Z"));
    private readonly RelationService _relations;
    private readonly BookInvoiceService _booking;

    public RelationServiceTests()
    {
        _relations = new RelationService(_store, _clock);
        var invoices = new FakeInvoiceStore();
        _booking = new BookInvoiceService(invoices, invoices, _clock, _relations);
    }

    private static CanonicalParty VanDijk(string? endpoint = "87654321") => new()
    {
        Name = "Van Dijk Techniek B.V.",
        VatIdentifier = "NL 8149.12.345 B01",
        LegalRegistrationId = "8765 4321",
        LegalRegistrationScheme = "0106",
        ElectronicAddress = endpoint,
        ElectronicAddressScheme = endpoint is null ? null : "0106",
        Address = new CanonicalAddress { CountryCode = "NL" },
    };

    private Task Book(CanonicalParty buyer, string organizationId = OrgA)
        => _booking.BookAsync(organizationId, "u", new CanonicalInvoiceDraft { Buyer = buyer }, new byte[] { 1 }, "a.pdf", "m");

    [Fact]
    public async Task Booking_remembers_the_buyer_normalised()
    {
        await Book(VanDijk());

        var relation = Assert.Single(await _relations.ListAsync(OrgA));
        Assert.Equal("NL814912345B01", relation.VatIdentifier);
        Assert.Equal("87654321", relation.LegalRegistrationId);
        Assert.Equal(("87654321", "0106"), (relation.ElectronicAddress, relation.ElectronicAddressScheme));
        Assert.Equal("NL", relation.CountryCode);
    }

    [Fact]
    public async Task Booking_the_same_customer_again_updates_rather_than_duplicates()
    {
        await Book(VanDijk(endpoint: null));
        await Book(VanDijk() with { Name = "Van Dijk Techniek" });

        var relation = Assert.Single(await _relations.ListAsync(OrgA));
        Assert.Equal("87654321", relation.ElectronicAddress);
        Assert.Equal("Van Dijk Techniek", relation.Name);
    }

    [Fact]
    public async Task An_empty_field_on_a_later_invoice_does_not_erase_what_was_learned()
    {
        await Book(VanDijk());
        await Book(VanDijk(endpoint: null));

        Assert.Equal("87654321", Assert.Single(await _relations.ListAsync(OrgA)).ElectronicAddress);
    }

    [Fact]
    public async Task A_buyer_without_vat_or_registration_is_not_stored()
    {
        await Book(new CanonicalParty { Name = "Particulier" });

        Assert.Empty(_store.All);
    }

    [Fact]
    public async Task A_relation_is_invisible_to_another_organisation()
    {
        await Book(VanDijk(), OrgA);

        Assert.Empty(await _relations.ListAsync(OrgB));
        Assert.Null(await _relations.FindForAsync(OrgB, VanDijk()));
    }

    [Fact]
    public async Task Another_organisations_relation_cannot_be_updated()
    {
        await Book(VanDijk(), OrgA);
        var relation = Assert.Single(await _relations.ListAsync(OrgA));

        var result = await _relations.UpdateAsync(OrgB, relation.RelationId,
            new RelationUpdate("Overgenomen", "NL1", null, null, null, null, null));

        Assert.Equal(RelationUpdateOutcome.NotFound, result.Outcome);
        Assert.Equal("Van Dijk Techniek B.V.", Assert.Single(await _relations.ListAsync(OrgA)).Name);
    }

    [Fact]
    public async Task A_correction_is_stored_normalised()
    {
        await Book(VanDijk(), OrgA);
        var relation = Assert.Single(await _relations.ListAsync(OrgA));

        var result = await _relations.UpdateAsync(OrgA, relation.RelationId,
            new RelationUpdate(" Van Dijk ", "nl814912345b01", "", null, "NL814912345B01", "9944", "nl"));

        Assert.Equal(RelationUpdateOutcome.Updated, result.Outcome);
        Assert.Equal(("Van Dijk", "NL814912345B01", null, "9944", "NL"),
            (result.Relation!.Name, result.Relation.VatIdentifier, result.Relation.LegalRegistrationId,
             result.Relation.ElectronicAddressScheme, result.Relation.CountryCode));
    }

    [Theory]
    [InlineData(null, null, "123", "0217")] // no identifier left, and a scheme removed from EAS
    [InlineData("NL1", null, "123", "0217")]
    [InlineData(null, null, null, null)]
    public async Task An_invalid_correction_is_refused(string? vat, string? kvk, string? endpoint, string? scheme)
    {
        await Book(VanDijk(), OrgA);
        var relation = Assert.Single(await _relations.ListAsync(OrgA));

        var result = await _relations.UpdateAsync(OrgA, relation.RelationId, new RelationUpdate("X", vat, kvk, null, endpoint, scheme, null));

        Assert.Equal(RelationUpdateOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public async Task Enrichment_fills_only_the_empty_buyer_fields_and_says_so()
    {
        await Book(VanDijk(), OrgA);
        var enrichment = new InvoiceEnrichment(new FakeSettingsStore(), _relations);

        var result = await enrichment.EnrichAsync(OrgA, new CanonicalInvoiceDraft
        {
            Buyer = new CanonicalParty { Name = "Van Dijk", VatIdentifier = "NL814912345B01" },
        });

        var buyer = result.Draft.Buyer!;
        Assert.Equal(("87654321", "0106"), (buyer.ElectronicAddress, buyer.ElectronicAddressScheme));
        Assert.Equal(("87654321", "0106"), (buyer.LegalRegistrationId, buyer.LegalRegistrationScheme));
        Assert.Equal("NL", buyer.Address!.CountryCode);
        Assert.Equal("Van Dijk", buyer.Name);
        Assert.All(result.Notices, n => Assert.Equal(ValidationSeverity.Info, n.Severity));
        Assert.Contains(result.Notices, n => n.Path == "buyer.electronicAddress");
    }

    [Fact]
    public async Task A_conflict_keeps_the_document_value_and_warns()
    {
        await Book(VanDijk(), OrgA);
        var enrichment = new InvoiceEnrichment(new FakeSettingsStore(), _relations);

        var result = await enrichment.EnrichAsync(OrgA, new CanonicalInvoiceDraft
        {
            Buyer = VanDijk() with { ElectronicAddress = "NL814912345B01", ElectronicAddressScheme = "9944" },
        });

        Assert.Equal("NL814912345B01", result.Draft.Buyer!.ElectronicAddress);
        var warning = Assert.Single(result.Notices);
        Assert.Equal(("RELATION-CONFLICT", "buyer.electronicAddress", ValidationSeverity.Warning),
            (warning.Code, warning.Path, warning.Severity));
    }

    [Fact]
    public async Task A_buyer_the_document_does_not_identify_is_left_alone()
    {
        await Book(VanDijk(), OrgA);
        var enrichment = new InvoiceEnrichment(new FakeSettingsStore(), _relations);

        var result = await enrichment.EnrichAsync(OrgA, new CanonicalInvoiceDraft
        {
            Buyer = new CanonicalParty { Name = "Van Dijk Techniek B.V." },
        });

        Assert.Null(result.Draft.Buyer!.ElectronicAddress);
        Assert.Empty(result.Notices);
    }
}
