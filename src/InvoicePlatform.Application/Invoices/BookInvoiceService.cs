using InvoicePlatform.Application.Identity;
using InvoicePlatform.Application.Relations;
using InvoicePlatform.Domain.Calculation;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.Invoices;

namespace InvoicePlatform.Application.Invoices;

/// <summary>
/// Booking an invoice: keeping it deliberately rather than incidentally.
///
/// The document is stored before the record, so a record never points at a key
/// that does not exist. The reverse order would leave a listed invoice whose
/// PDF cannot be fetched.
/// </summary>
public sealed class BookInvoiceService(
    IInvoiceRepository invoices,
    IDocumentStore documents,
    IClock clock,
    RelationService relations)
{
    /// <param name="invoiceId">
    /// Supply an existing id to re-book a correction; omit it for a new invoice.
    /// </param>
    public async Task<StoredInvoice> BookAsync(
        string organizationId,
        string userId,
        CanonicalInvoiceDraft draft,
        ReadOnlyMemory<byte> pdf,
        string sourceFileName,
        string providerModel,
        string? invoiceId = null,
        CancellationToken cancellationToken = default)
    {
        var id = invoiceId ?? Guid.NewGuid().ToString("n");
        var now = clock.UtcNow;

        var documentKey = await documents
            .PutAsync(organizationId, id, pdf, "application/pdf", cancellationToken)
            .ConfigureAwait(false);

        var stored = new StoredInvoice
        {
            InvoiceId = id,
            OrganizationId = organizationId,
            Status = InvoiceStatus.Booked,
            // The record keeps the calculated figures, the same ones an export
            // writes, so the list, the review screen and the file agree. The
            // printed figures were compared at extraction (CALC-* warnings).
            Invoice = InvoiceTotalsCalculator.Apply(draft),
            DocumentKey = documentKey,
            SourceFileName = sourceFileName,
            SourceByteSize = pdf.Length,
            ProviderModel = providerModel,
            CreatedAt = now,
            CreatedByUserId = userId,
            BookedAt = now,
            BookedByUserId = userId,
        };

        await invoices.SaveAsync(stored, cancellationToken).ConfigureAwait(false);

        // After the invoice is saved: the relation is only worth keeping for an
        // invoice that was kept, and if this step fails the booking still
        // stands - booking again is harmless.
        await relations.RememberBuyerAsync(organizationId, stored.Invoice.Buyer, cancellationToken).ConfigureAwait(false);

        return stored;
    }

    public Task<StoredInvoice?> GetAsync(
        string organizationId,
        string invoiceId,
        CancellationToken cancellationToken = default)
        => invoices.GetAsync(organizationId, invoiceId, cancellationToken);

    public Task<IReadOnlyList<StoredInvoice>> ListAsync(
        string organizationId,
        CancellationToken cancellationToken = default)
        => invoices.ListAsync(organizationId, cancellationToken: cancellationToken);

    public async Task<byte[]?> GetDocumentAsync(
        string organizationId,
        string invoiceId,
        CancellationToken cancellationToken = default)
    {
        // Resolved through the invoice, so a document key belonging to another
        // tenant cannot be fetched by guessing it.
        var invoice = await invoices.GetAsync(organizationId, invoiceId, cancellationToken)
            .ConfigureAwait(false);

        return invoice?.DocumentKey is null
            ? null
            : await documents.GetAsync(invoice.DocumentKey, cancellationToken).ConfigureAwait(false);
    }
}
