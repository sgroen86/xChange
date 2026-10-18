using InvoicePlatform.Application.Identity;
using InvoicePlatform.Application.Imports;
using InvoicePlatform.Application.Invoices;
using InvoicePlatform.Application.Usage;
using InvoicePlatform.Domain.EInvoicing;
using InvoicePlatform.Domain.Imports;
using InvoicePlatform.Domain.Invoices;

namespace InvoicePlatform.Application.Conversions;

public enum ConversionStatus
{
    Queued,
    Processing,

    /// <summary>Booked: the file can be fetched.</summary>
    Ready,

    /// <summary>Read, but a person has to look at it first; it is in their review list.</summary>
    NeedsReview,

    Failed,
}

/// <summary>A conversion as its sender sees it.</summary>
/// <param name="Format">The format the file is made in; null until one is chosen.</param>
public sealed record Conversion(
    string ConversionId,
    ConversionStatus Status,
    string? Format,
    string? InvoiceNumber,
    IReadOnlyList<RuleViolation> Violations,
    string? Error);

/// <param name="FormatFound">False when the requested format does not exist; nothing was stored then.</param>
public sealed record ConversionSubmission(bool FormatFound, Conversion? Conversion);

public enum ConversionFileKind
{
    Ready,
    NotFound,

    /// <summary>Not booked (yet): still being read, waiting for a person, or failed.</summary>
    NotReady,

    /// <summary>The export refused it; see the outcome.</summary>
    Refused,
}

public sealed record ConversionFile(ConversionFileKind Kind, ExportOutcome? Export = null);

/// <summary>
/// The conversion API: another system sends a PDF with an API key and fetches
/// the e-invoice when it is ready. A conversion is an import with source Api -
/// stored, queued and read like any other - and its file is the export of the
/// booked invoice, so the file, the trial rule and the official check are
/// exactly the app's.
///
/// The organisation always comes from the key, never from the request, and a
/// conversion is looked up in that organisation's partition only.
/// </summary>
public sealed class ConversionService(
    ImportService imports,
    IInvoiceRepository invoices,
    InvoiceRulesetRegistry rulesets,
    ExportInvoiceService export,
    IUsageMeter usage)
{
    public async Task<ConversionSubmission> SubmitAsync(
        ApiKeyCaller caller,
        string? format,
        string fileName,
        ReadOnlyMemory<byte> pdf,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        format = string.IsNullOrWhiteSpace(format) ? null : format.Trim();
        if (format is not null && rulesets.Find(format) is null)
        {
            return new ConversionSubmission(false, null);
        }

        var record = await imports.SubmitAsync(
            caller.OrganizationId, ImportSource.Api, fileName, pdf, caller.Actor, rulesetId: format, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return new ConversionSubmission(true, ToConversion(record, invoice: null));
    }

    /// <returns>Null when the organisation has no such conversion.</returns>
    public async Task<Conversion?> GetAsync(string organizationId, string conversionId, CancellationToken cancellationToken = default)
    {
        var record = await imports.GetAsync(organizationId, conversionId, cancellationToken).ConfigureAwait(false);
        if (record is not { Source: ImportSource.Api })
        {
            return null;
        }

        // Booked in the app or by the worker, the invoice has the import's id.
        var invoice = await invoices.GetAsync(organizationId, conversionId, cancellationToken).ConfigureAwait(false);
        return ToConversion(record, invoice);
    }

    public async Task<ConversionFile> GetFileAsync(string organizationId, string conversionId, CancellationToken cancellationToken = default)
    {
        var conversion = await GetAsync(organizationId, conversionId, cancellationToken).ConfigureAwait(false);
        if (conversion is null)
        {
            return new ConversionFile(ConversionFileKind.NotFound);
        }

        // A trial is told so at once, rather than to wait for a file it will never get.
        if ((await usage.GetAsync(organizationId, cancellationToken).ConfigureAwait(false)).Trial)
        {
            return new ConversionFile(ConversionFileKind.Refused, ExportOutcome.PlanRequired);
        }

        if (conversion.Status != ConversionStatus.Ready)
        {
            return new ConversionFile(ConversionFileKind.NotReady);
        }

        var outcome = await export.ExportAsync(organizationId, conversionId, conversion.Format, cancellationToken).ConfigureAwait(false);
        return outcome.Kind == ExportOutcomeKind.Exported
            ? new ConversionFile(ConversionFileKind.Ready, outcome)
            : new ConversionFile(ConversionFileKind.Refused, outcome);
    }

    private static Conversion ToConversion(ImportRecord record, StoredInvoice? invoice)
    {
        var status = invoice?.Status == InvoiceStatus.Booked
            ? ConversionStatus.Ready
            : record.Status switch
            {
                ImportStatus.Queued or ImportStatus.AwaitingApproval => ConversionStatus.Queued,
                ImportStatus.Processing => ConversionStatus.Processing,
                ImportStatus.Failed => ConversionStatus.Failed,
                // Taken off the list without a booked invoice: saved as a
                // concept is still a person's work; dismissed, or the invoice
                // deleted, means no file will come.
                ImportStatus.Closed when invoice is null => ConversionStatus.Failed,
                _ => ConversionStatus.NeedsReview,
            };

        var error = status switch
        {
            ConversionStatus.Failed when record.Status == ImportStatus.Closed => "De factuur is in xChange verwijderd zonder te boeken.",
            ConversionStatus.Ready => null,
            _ => record.Error,
        };

        return new Conversion(
            record.ImportId,
            status,
            record.RulesetId,
            invoice?.Invoice.InvoiceNumber ?? record.Draft?.InvoiceNumber,
            status == ConversionStatus.NeedsReview ? record.Violations : [],
            error);
    }
}
