using InvoicePlatform.Application.Identity;
using InvoicePlatform.Application.Invoices;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.Invoices;

namespace InvoicePlatform.Application.Tests;

public class BookInvoiceServiceTests
{
    private const string OrgA = "org-a";
    private const string OrgB = "org-b";

    private readonly FakeInvoiceStore _store = new();
    private readonly TestClock _clock = new(DateTimeOffset.Parse("2026-09-08T12:00:00Z"));
    private readonly BookInvoiceService _service;

    public BookInvoiceServiceTests()
        => _service = new BookInvoiceService(_store, _store, _clock);

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

    private sealed class TestClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed class FakeInvoiceStore : IInvoiceRepository, IDocumentStore
    {
        private readonly Dictionary<(string Org, string Id), StoredInvoice> _invoices = [];

        public Dictionary<string, byte[]> Documents { get; } = [];

        public Task SaveAsync(StoredInvoice invoice, CancellationToken cancellationToken = default)
        {
            _invoices[(invoice.OrganizationId, invoice.InvoiceId)] = invoice;
            return Task.CompletedTask;
        }

        public Task<StoredInvoice?> GetAsync(string organizationId, string invoiceId, CancellationToken cancellationToken = default)
            => Task.FromResult(_invoices.GetValueOrDefault((organizationId, invoiceId)));

        public Task<IReadOnlyList<StoredInvoice>> ListAsync(string organizationId, int limit = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StoredInvoice>>(
                _invoices.Where(kv => kv.Key.Org == organizationId).Select(kv => kv.Value).ToList());

        public Task<string> PutAsync(string organizationId, string invoiceId, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken = default)
        {
            var key = $"{organizationId}/{invoiceId}.pdf";
            Documents[key] = content.ToArray();
            return Task.FromResult(key);
        }

        public Task<byte[]?> GetAsync(string documentKey, CancellationToken cancellationToken = default)
            => Task.FromResult(Documents.GetValueOrDefault(documentKey));
    }
}
