using InvoicePlatform.Application.Learning;
using InvoicePlatform.Application.Identity;
using InvoicePlatform.Application.Invoices;
using InvoicePlatform.Application.Relations;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.EInvoicing;
using InvoicePlatform.Domain.Invoices;

namespace InvoicePlatform.Application.Tests;

public class BookInvoiceServiceTests
{
    private const string OrgA = "org-a";
    private const string OrgB = "org-b";

    private readonly FakeInvoiceStore _store = new();
    private readonly FakeRelationStore _relations = new();
    private readonly TestClock _clock = new(DateTimeOffset.Parse("2026-09-08T12:00:00Z"));
    private readonly BookInvoiceService _service;

    public BookInvoiceServiceTests()
        => _service = new BookInvoiceService(_store, _store, _clock, new RelationService(_relations, _clock, new InvoiceRulesetRegistry()), new LearningService(new FakeLearningStore(), _clock));

    private static CanonicalInvoiceDraft ADraft(string number = "F-1") => new()
    {
        InvoiceNumber = number,
        CurrencyCode = "EUR",
        Lines = [new CanonicalInvoiceLine { Description = "Werk", NetAmount = 100m }],
    };

    [Fact]
    public async Task Booking_stores_the_invoice_and_the_document()
    {
        var stored = await _service.BookAsync(
            OrgA, "user-1", ADraft(), new byte[] { 1, 2, 3 }, "factuur.pdf", "claude-opus-5");

        Assert.Equal(InvoiceStatus.Booked, stored.Status);
        Assert.Equal(OrgA, stored.OrganizationId);
        Assert.Equal("user-1", stored.BookedByUserId);
        Assert.Equal(_clock.UtcNow, stored.BookedAt);
        Assert.NotNull(stored.DocumentKey);
        Assert.Equal(3, _store.Documents[stored.DocumentKey!].Length);
    }

    [Fact]
    public async Task Booking_stores_the_calculated_totals_so_the_record_matches_the_export()
    {
        var draft = new CanonicalInvoiceDraft
        {
            Lines = [new CanonicalInvoiceLine { Quantity = 2m, UnitPrice = 10.00m, NetAmount = 99m, VatCategoryCode = "S", VatPercentage = 21m }],
            Totals = new CanonicalTotals { PayableAmount = 1m },
        };

        var stored = await _service.BookAsync(OrgA, "u", draft, new byte[] { 1 }, "a.pdf", "m");

        Assert.Equal(20.00m, stored.Invoice.Lines[0].NetAmount);
        Assert.Equal(24.20m, stored.Invoice.Totals!.PayableAmount);
        Assert.Equal(4.20m, Assert.Single(stored.Invoice.VatBreakdown).TaxAmount);
    }

    [Fact]
    public async Task A_booked_invoice_is_outgoing()
    {
        var stored = await _service.BookAsync(OrgA, "u", ADraft(), new byte[] { 1 }, "a.pdf", "m");

        Assert.Equal(InvoiceDirection.Outgoing, stored.Direction);
    }

    [Fact]
    public async Task An_invoice_is_invisible_to_another_organisation()
    {
        // The property that matters most: a tenant cannot reach another
        // tenant's invoice even knowing its id.
        var stored = await _service.BookAsync(
            OrgA, "user-1", ADraft(), new byte[] { 1 }, "a.pdf", "claude-opus-5");

        Assert.NotNull(await _service.GetAsync(OrgA, stored.InvoiceId));
        Assert.Null(await _service.GetAsync(OrgB, stored.InvoiceId));
    }

    [Fact]
    public async Task A_document_is_unreachable_from_another_organisation()
    {
        var stored = await _service.BookAsync(
            OrgA, "user-1", ADraft(), new byte[] { 7, 7 }, "a.pdf", "m");

        Assert.NotNull(await _service.GetDocumentAsync(OrgA, stored.InvoiceId));
        Assert.Null(await _service.GetDocumentAsync(OrgB, stored.InvoiceId));
    }

    [Fact]
    public async Task Listing_returns_only_the_callers_organisation()
    {
        await _service.BookAsync(OrgA, "u", ADraft("A-1"), new byte[] { 1 }, "a.pdf", "m");
        await _service.BookAsync(OrgA, "u", ADraft("A-2"), new byte[] { 1 }, "a.pdf", "m");
        await _service.BookAsync(OrgB, "u", ADraft("B-1"), new byte[] { 1 }, "b.pdf", "m");

        var forA = await _service.ListAsync(OrgA);

        Assert.Equal(2, forA.Count);
        Assert.DoesNotContain(forA, i => i.Invoice.InvoiceNumber == "B-1");
    }

    [Fact]
    public async Task Booking_the_same_invoice_again_replaces_it_rather_than_duplicating()
    {
        var first = await _service.BookAsync(
            OrgA, "u", ADraft("F-1"), new byte[] { 1 }, "a.pdf", "m");

        var again = await _service.BookAsync(
            OrgA, "u", ADraft("F-1") with { Note = "gecorrigeerd" },
            new byte[] { 1 }, "a.pdf", "m", first.InvoiceId);

        Assert.Equal(first.InvoiceId, again.InvoiceId);
        Assert.Single(await _service.ListAsync(OrgA));
        Assert.Equal("gecorrigeerd", (await _service.GetAsync(OrgA, first.InvoiceId))!.Invoice.Note);
    }

    private static CanonicalInvoiceDraft ADraftFor(string buyerVat) => ADraft() with
    {
        Buyer = new CanonicalParty { Name = "Klant B.V.", VatIdentifier = buyerVat },
    };

    [Fact]
    public async Task A_concept_is_kept_with_its_document_but_teaches_nothing()
    {
        var saved = await _service.SaveDraftAsync(OrgA, "u", ADraftFor("NL001234567B01"), new byte[] { 1, 2 }, "a.pdf", "m");

        Assert.NotNull(saved);
        Assert.Equal(InvoiceStatus.Draft, saved.Status);
        Assert.Null(saved.BookedAt);
        Assert.Equal(2, _store.Documents[saved.DocumentKey!].Length);
        Assert.Empty(_relations.All);
    }

    [Fact]
    public async Task Booking_a_concept_remembers_the_buyer_and_keeps_when_it_was_first_saved()
    {
        var saved = await _service.SaveDraftAsync(OrgA, "first", ADraftFor("NL001234567B01"), new byte[] { 1 }, "a.pdf", "m");
        _clock.UtcNow = _clock.UtcNow.AddHours(1);

        var booked = await _service.BookAsync(OrgA, "second", ADraftFor("NL001234567B01"), new byte[] { 1 }, "a.pdf", "m", saved!.InvoiceId);

        Assert.Equal(InvoiceStatus.Booked, booked.Status);
        Assert.Equal(saved.CreatedAt, booked.CreatedAt);
        Assert.Equal("first", booked.CreatedByUserId);
        Assert.Equal("second", booked.BookedByUserId);
        Assert.Single(await _service.ListAsync(OrgA));
        Assert.Single(_relations.All);
    }

    [Fact]
    public async Task A_booked_invoice_is_not_saved_back_to_a_concept()
    {
        var booked = await _service.BookAsync(OrgA, "u", ADraft("F-1"), new byte[] { 1 }, "a.pdf", "m");

        var saved = await _service.SaveDraftAsync(OrgA, "u", ADraft("F-1") with { Note = "later" }, new byte[] { 1 }, "a.pdf", "m", booked.InvoiceId);

        Assert.Null(saved);
        var kept = await _service.GetAsync(OrgA, booked.InvoiceId);
        Assert.Equal(InvoiceStatus.Booked, kept!.Status);
        Assert.Null(kept.Invoice.Note);
    }

    [Fact]
    public async Task Deleting_removes_the_invoice_and_its_document()
    {
        var booked = await _service.BookAsync(OrgA, "u", ADraft(), new byte[] { 1 }, "a.pdf", "m");

        Assert.True(await _service.DeleteAsync(OrgA, booked.InvoiceId));

        Assert.Null(await _service.GetAsync(OrgA, booked.InvoiceId));
        Assert.Empty(_store.Documents);
    }

    [Fact]
    public async Task Another_organisation_cannot_delete_an_invoice()
    {
        var booked = await _service.BookAsync(OrgA, "u", ADraft(), new byte[] { 1 }, "a.pdf", "m");

        Assert.False(await _service.DeleteAsync(OrgB, booked.InvoiceId));

        Assert.NotNull(await _service.GetAsync(OrgA, booked.InvoiceId));
        Assert.Single(_store.Documents);
    }
}
