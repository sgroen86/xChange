using InvoicePlatform.Application.Identity;
using InvoicePlatform.Application.Invoices;
using InvoicePlatform.Domain.EInvoicing;
using InvoicePlatform.Domain.Imports;
using InvoicePlatform.Domain.Validation;

namespace InvoicePlatform.Application.Conversions;

/// <summary>
/// What happens to a conversion once the worker has read it: booked without a
/// person when nothing stands in the way, otherwise left in the review list
/// with what stopped it.
///
/// "Nothing in the way" is strict: the complete check an export does (runtime
/// and official rules, in the chosen format) passes, and reading left no
/// warning - a printed total that does not match the recalculated one, or a
/// buyer that disagrees with the relation, is for a person to look at even
/// when the rules would accept the file.
/// </summary>
public sealed class ConversionCompletion(
    ExportInvoiceService export,
    BookInvoiceService booking,
    IDocumentStore documents,
    IClock clock)
{
    /// <param name="record">An <see cref="ImportSource.Api"/> import that was just read.</param>
    /// <returns>The record as it should be saved: closed when booked, ready (in review) with its violations otherwise.</returns>
    public async Task<ImportRecord> CompleteAsync(ImportRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record is not { Source: ImportSource.Api, Status: ImportStatus.Ready, Draft: { } draft })
        {
            return record;
        }

        var organizationId = record.OrganizationId;
        var check = await export.CheckAsBookedAsync(organizationId, record.ImportId, draft, record.RulesetId, cancellationToken)
            .ConfigureAwait(false);

        var violations = record.Warnings
            .Where(w => w.Severity != ValidationSeverity.Info)
            .Select(w => new RuleViolation(w.Code, null, w.Path, w.Message))
            .Concat(check.Kind switch
            {
                ExportOutcomeKind.Exported => [],
                ExportOutcomeKind.Incomplete => check.Violations ?? [],
                ExportOutcomeKind.UnknownRuleset => [new RuleViolation("XC-FORMAT", null, "",
                    $"Het gevraagde formaat '{record.RulesetId}' bestaat niet meer.")],
                // Without the official verdict nothing is booked unseen.
                _ => [new RuleViolation("XC-OFFICIAL-UNAVAILABLE", null, "",
                    "De officiële controle was niet beschikbaar. Controleer de factuur en boek haar zelf.")],
            })
            .ToList();

        // The format that was checked is the one the file is made in.
        record = record with { RulesetId = record.RulesetId ?? check.RulesetId, Violations = violations, UpdatedAt = clock.UtcNow };
        if (violations.Count > 0)
        {
            return record;
        }

        var pdf = await documents.GetAsync(record.DocumentKey, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Het PDF-bestand is niet meer gevonden.");

        // Booked under the import id, as the app books an import; by the key
        // that sent it. No extraction id: nobody corrected anything, so there
        // is nothing to learn.
        await booking.BookAsync(
            organizationId,
            record.CreatedByUserId ?? "apikey",
            draft,
            pdf,
            record.FileName,
            record.ProviderModel ?? "onbekend",
            invoiceId: record.ImportId,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return record with { Status = ImportStatus.Closed };
    }
}
