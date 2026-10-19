using InvoicePlatform.Application.Identity;
using InvoicePlatform.Application.Learning;
using InvoicePlatform.Application.Quality;
using InvoicePlatform.Application.Relations;
using InvoicePlatform.Domain.Calculation;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.Invoices;

namespace InvoicePlatform.Application.Invoices;

/// <summary>
/// Booking an invoice: keeping it deliberately rather than incidentally. A
/// reviewer can also save it as a concept first, or delete it altogether.
///
/// The document is stored before the record, so a record never points at a key
/// that does not exist. The reverse order would leave a listed invoice whose
/// PDF cannot be fetched.
/// </summary>
public sealed class BookInvoiceService(
    IInvoiceRepository invoices,
    IDocumentStore documents,
    IClock clock,
    RelationService relations,
    LearningService learning,
    ReadingQualityService quality)
{
    /// <param name="invoiceId">
    /// Supply an existing id to re-book a correction, or to book a saved
    /// concept; omit it for a new invoice.
    /// </param>
    /// <param name="extractionId">The upload this invoice came from, so xChange learns from what the reviewer corrected.</param>
    /// <param name="reviewed">
    /// True when a person reviewed the invoice in the app and booked it. Only
    /// then is the reading measured: a booking without a person (the
    /// conversion API) confirms nothing about what was read.
    /// </param>
    public async Task<StoredInvoice> BookAsync(
        string organizationId,
        string userId,
        CanonicalInvoiceDraft draft,
        ReadOnlyMemory<byte> pdf,
        string sourceFileName,
        string providerModel,
        string? invoiceId = null,
        string? extractionId = null,
        bool reviewed = false,
        CancellationToken cancellationToken = default)
    {
        var id = invoiceId ?? Guid.NewGuid().ToString("n");
        var existing = invoiceId is null
            ? null
            : await invoices.GetAsync(organizationId, id, cancellationToken).ConfigureAwait(false);

        var stored = await StoreAsync(
            organizationId, userId, id, existing, InvoiceStatus.Booked, draft, pdf, sourceFileName, providerModel, cancellationToken)
            .ConfigureAwait(false);

        // After the invoice is saved: the relation is only worth keeping for an
        // invoice that was kept, and if this step fails the booking still
        // stands - booking again is harmless.
        await relations.RememberBuyerAsync(organizationId, stored.Invoice.Buyer, cancellationToken).ConfigureAwait(false);

        // Learning compares the booking with what the model read for this
        // upload - the draft as the reviewer left it, not the recalculated
        // one: the calculator's figures are not corrections.
        if (!string.IsNullOrWhiteSpace(extractionId))
        {
            var snapshot = await learning.LearnAsync(organizationId, extractionId, draft, cancellationToken).ConfigureAwait(false);

            // Measured from the same snapshot, at the same moment: learning
            // has just taken it, so only the first booking of an upload counts.
            if (snapshot is not null && reviewed)
            {
                await quality.MeasureAsync(organizationId, stored.InvoiceId, userId, snapshot, draft).ConfigureAwait(false);
            }
        }

        return stored;
    }

    /// <summary>
    /// Keeps a reviewed invoice as a concept, so the work is not lost and can
    /// be picked up in another browser. A concept teaches nothing: no relation
    /// is remembered and no correction learned until it is booked, and it
    /// cannot be exported.
    /// </summary>
    /// <returns>
    /// Null when the invoice is already booked: saving it as a concept would
    /// quietly undo the booking. A booked invoice is corrected by booking again.
    /// </returns>
    public async Task<StoredInvoice?> SaveDraftAsync(
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
        var existing = invoiceId is null
            ? null
            : await invoices.GetAsync(organizationId, id, cancellationToken).ConfigureAwait(false);

        if (existing?.Status == InvoiceStatus.Booked)
        {
            return null;
        }

        return await StoreAsync(
            organizationId, userId, id, existing, InvoiceStatus.Draft, draft, pdf, sourceFileName, providerModel, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Removes the invoice and its PDF. The record goes first, the reverse of
    /// storing: a failure halfway leaves an unreachable PDF, never a listed
    /// invoice without one.
    /// </summary>
    /// <returns>False when there is no such invoice in this organisation.</returns>
    public async Task<bool> DeleteAsync(
        string organizationId,
        string invoiceId,
        CancellationToken cancellationToken = default)
    {
        var invoice = await invoices.GetAsync(organizationId, invoiceId, cancellationToken).ConfigureAwait(false);
        if (invoice is null)
        {
            return false;
        }

        await invoices.DeleteAsync(organizationId, invoiceId, cancellationToken).ConfigureAwait(false);
        if (invoice.DocumentKey is not null)
        {
            await documents.DeleteAsync(invoice.DocumentKey, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    private async Task<StoredInvoice> StoreAsync(
        string organizationId,
        string userId,
        string id,
        StoredInvoice? existing,
        InvoiceStatus status,
        CanonicalInvoiceDraft draft,
        ReadOnlyMemory<byte> pdf,
        string sourceFileName,
        string providerModel,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        var documentKey = await documents
            .PutAsync(organizationId, id, pdf, "application/pdf", cancellationToken)
            .ConfigureAwait(false);

        var booked = status == InvoiceStatus.Booked;
        var stored = new StoredInvoice
        {
            InvoiceId = id,
            OrganizationId = organizationId,
            Status = status,
            // The record keeps the calculated figures, the same ones an export
            // writes, so the list, the review screen and the file agree. The
            // printed figures were compared at extraction (CALC-* warnings).
            Invoice = InvoiceTotalsCalculator.Apply(draft),
            DocumentKey = documentKey,
            SourceFileName = sourceFileName,
            SourceByteSize = pdf.Length,
            ProviderModel = providerModel,
            // A concept that is booked, or an invoice booked again, keeps when
            // and by whom it was first kept.
            CreatedAt = existing?.CreatedAt ?? now,
            CreatedByUserId = existing?.CreatedByUserId ?? userId,
            BookedAt = booked ? now : null,
            BookedByUserId = booked ? userId : null,
        };

        await invoices.SaveAsync(stored, cancellationToken).ConfigureAwait(false);
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
