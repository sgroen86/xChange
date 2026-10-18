using InvoicePlatform.Application.Identity;
using InvoicePlatform.Application.Invoices;
using InvoicePlatform.Domain.Imports;

namespace InvoicePlatform.Application.Imports;

/// <summary>Imports in storage, per organisation.</summary>
public interface IImportRepository
{
    Task SaveAsync(ImportRecord record, CancellationToken cancellationToken = default);

    Task<ImportRecord?> GetAsync(string organizationId, string importId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ImportRecord>> ListAsync(string organizationId, CancellationToken cancellationToken = default);

    Task DeleteAsync(string organizationId, string importId, CancellationToken cancellationToken = default);
}

/// <summary>Where imports wait to be read. Implemented by SQS; the worker consumes it.</summary>
public interface IImportQueue
{
    Task EnqueueAsync(string organizationId, string importId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Batch and email import: a PDF is stored and queued at once, read by the
/// worker in the background, and then waits in the organisation's list until
/// a reviewer opens it. Reading goes through the same extraction as a single
/// upload, learning included.
/// </summary>
public sealed class ImportService(
    IImportRepository imports,
    IImportQueue queue,
    IDocumentStore documents,
    ExtractInvoiceService extraction,
    IClock clock)
{
    /// <summary>Stores the PDF, records the import and puts it on the queue.</summary>
    public async Task<ImportRecord> SubmitAsync(
        string organizationId,
        ImportSource source,
        string fileName,
        ReadOnlyMemory<byte> pdf,
        string? userId = null,
        string? sender = null,
        string? subject = null,
        CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid().ToString("n");
        var now = clock.UtcNow;

        // Stored under the id the invoice will have once booked: the PDF is kept once.
        var key = await documents.PutAsync(organizationId, id, pdf, "application/pdf", cancellationToken).ConfigureAwait(false);

        var record = new ImportRecord
        {
            ImportId = id,
            OrganizationId = organizationId,
            Source = source,
            Status = ImportStatus.Queued,
            FileName = fileName,
            ByteSize = pdf.Length,
            DocumentKey = key,
            Sender = sender,
            Subject = subject,
            CreatedAt = now,
            CreatedByUserId = userId,
            UpdatedAt = now,
        };

        await imports.SaveAsync(record, cancellationToken).ConfigureAwait(false);
        await queue.EnqueueAsync(organizationId, id, cancellationToken).ConfigureAwait(false);
        return record;
    }

    /// <summary>
    /// Reads one import (the worker calls this per queue message). A failure is
    /// recorded on the import rather than thrown, so the queue does not retry a
    /// document the model cannot read; the reviewer can retry it.
    /// </summary>
    public async Task ProcessAsync(string organizationId, string importId, CancellationToken cancellationToken = default)
    {
        var record = await imports.GetAsync(organizationId, importId, cancellationToken).ConfigureAwait(false);
        if (record is null || record.Status is ImportStatus.Ready)
        {
            return;
        }

        record = record with { Status = ImportStatus.Processing, Error = null, UpdatedAt = clock.UtcNow };
        await imports.SaveAsync(record, cancellationToken).ConfigureAwait(false);

        try
        {
            var pdf = await documents.GetAsync(record.DocumentKey, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Het PDF-bestand is niet meer gevonden.");

            var result = await extraction.ExtractAsync(organizationId, pdf, cancellationToken).ConfigureAwait(false);

            record = record with
            {
                Status = ImportStatus.Ready,
                ExtractionId = result.InvoiceId,
                Draft = result.Draft,
                Warnings = result.Warnings,
                ProviderModel = $"{result.Execution.Provider}/{result.Execution.Model}",
                UpdatedAt = clock.UtcNow,
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Whatever went wrong - the model, the network, an unreadable PDF -
            // is shown on the import, where the reviewer can retry it.
            record = record with { Status = ImportStatus.Failed, Error = exception.Message, UpdatedAt = clock.UtcNow };
        }

        await imports.SaveAsync(record, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ImportRecord>> ListAsync(string organizationId, CancellationToken cancellationToken = default)
        => (await imports.ListAsync(organizationId, cancellationToken).ConfigureAwait(false))
            .OrderByDescending(r => r.CreatedAt)
            .ToList();

    public Task<ImportRecord?> GetAsync(string organizationId, string importId, CancellationToken cancellationToken = default)
        => imports.GetAsync(organizationId, importId, cancellationToken);

    public async Task<byte[]?> GetDocumentAsync(string organizationId, string importId, CancellationToken cancellationToken = default)
    {
        // Only through the organisation's own import record (CLAUDE.md: a document is fetched via its record).
        var record = await imports.GetAsync(organizationId, importId, cancellationToken).ConfigureAwait(false);
        return record is null ? null : await documents.GetAsync(record.DocumentKey, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Puts a failed import back on the queue.</summary>
    public async Task<bool> RetryAsync(string organizationId, string importId, CancellationToken cancellationToken = default)
    {
        var record = await imports.GetAsync(organizationId, importId, cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            return false;
        }

        await imports.SaveAsync(record with { Status = ImportStatus.Queued, Error = null, UpdatedAt = clock.UtcNow }, cancellationToken).ConfigureAwait(false);
        await queue.EnqueueAsync(organizationId, importId, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Takes an import off the list: after it was booked, or when it is not
    /// wanted. The PDF stays; a booked invoice uses the same document.
    /// </summary>
    public async Task<bool> DismissAsync(string organizationId, string importId, CancellationToken cancellationToken = default)
    {
        if (await imports.GetAsync(organizationId, importId, cancellationToken).ConfigureAwait(false) is null)
        {
            return false;
        }

        await imports.DeleteAsync(organizationId, importId, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
