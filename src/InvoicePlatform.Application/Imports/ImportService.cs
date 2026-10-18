using InvoicePlatform.Application.Conversions;
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
    ConversionCompletion conversions,
    IClock clock)
{
    /// <summary>
    /// Stores the PDF and records the import. An upload goes on the queue at
    /// once, and so does a conversion (its sender holds a key an admin
    /// issued); a mailed PDF waits for a user's approval (anyone who knows the
    /// address can send one, and reading spends model credit).
    /// </summary>
    /// <param name="userId">Who sent it: a user id, or "apikey:&lt;keyId&gt;" for a conversion.</param>
    /// <param name="rulesetId">For a conversion: the format asked for, if any.</param>
    public async Task<ImportRecord> SubmitAsync(
        string organizationId,
        ImportSource source,
        string fileName,
        ReadOnlyMemory<byte> pdf,
        string? userId = null,
        string? sender = null,
        string? subject = null,
        string? rulesetId = null,
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
            Status = source == ImportSource.Email ? ImportStatus.AwaitingApproval : ImportStatus.Queued,
            FileName = fileName,
            ByteSize = pdf.Length,
            DocumentKey = key,
            Sender = sender,
            Subject = subject,
            RulesetId = rulesetId,
            CreatedAt = now,
            CreatedByUserId = userId,
            UpdatedAt = now,
        };

        await imports.SaveAsync(record, cancellationToken).ConfigureAwait(false);
        if (record.Status == ImportStatus.Queued)
        {
            await queue.EnqueueAsync(organizationId, id, cancellationToken).ConfigureAwait(false);
        }

        return record;
    }

    /// <summary>A user approves a mailed PDF: now it is read.</summary>
    /// <returns>False when there is no such import waiting for approval in this organisation.</returns>
    public async Task<bool> ApproveAsync(string organizationId, string importId, CancellationToken cancellationToken = default)
    {
        var record = await imports.GetAsync(organizationId, importId, cancellationToken).ConfigureAwait(false);
        if (record is not { Status: ImportStatus.AwaitingApproval })
        {
            return false;
        }

        await imports.SaveAsync(record with { Status = ImportStatus.Queued, UpdatedAt = clock.UtcNow }, cancellationToken).ConfigureAwait(false);
        await queue.EnqueueAsync(organizationId, importId, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Reads one import (the worker calls this per queue message). A failure is
    /// recorded on the import rather than thrown, so the queue does not retry a
    /// document the model cannot read; the reviewer can retry it.
    /// </summary>
    public async Task ProcessAsync(string organizationId, string importId, CancellationToken cancellationToken = default)
    {
        var record = await imports.GetAsync(organizationId, importId, cancellationToken).ConfigureAwait(false);

        // Never read what is already read, nor a mailed PDF nobody approved -
        // whatever put the message on the queue.
        if (record is null || record.Status is ImportStatus.Ready or ImportStatus.AwaitingApproval or ImportStatus.Closed)
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

        // A conversion is finished without a person when it can be: saved as
        // read first, so a failure from here on never makes it read again.
        if (record is { Source: ImportSource.Api, Status: ImportStatus.Ready })
        {
            try
            {
                record = await conversions.CompleteAsync(record, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                // It stays in the review list, where a person can finish it.
                record = record with { Error = $"Automatisch afronden is mislukt: {exception.Message}", UpdatedAt = clock.UtcNow };
            }

            await imports.SaveAsync(record, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>The review list: everything not yet booked or dismissed.</summary>
    public async Task<IReadOnlyList<ImportRecord>> ListAsync(string organizationId, CancellationToken cancellationToken = default)
        => (await imports.ListAsync(organizationId, cancellationToken).ConfigureAwait(false))
            .Where(r => r.Status != ImportStatus.Closed)
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

        // Retrying is not a way around approval, nor a way to read a closed conversion again.
        if (record is null || record.Status is ImportStatus.AwaitingApproval or ImportStatus.Closed)
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
    ///
    /// A conversion is closed rather than deleted: its sender may still ask
    /// for its status and file, and the format it asked for is kept on it.
    /// </summary>
    public async Task<bool> DismissAsync(string organizationId, string importId, CancellationToken cancellationToken = default)
    {
        var record = await imports.GetAsync(organizationId, importId, cancellationToken).ConfigureAwait(false);
        if (record is null || record.Status == ImportStatus.Closed)
        {
            return false;
        }

        if (record.Source == ImportSource.Api)
        {
            await imports.SaveAsync(record with { Status = ImportStatus.Closed, UpdatedAt = clock.UtcNow }, cancellationToken).ConfigureAwait(false);
            return true;
        }

        await imports.DeleteAsync(organizationId, importId, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
