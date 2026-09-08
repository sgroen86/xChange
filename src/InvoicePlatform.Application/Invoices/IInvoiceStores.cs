using InvoicePlatform.Domain.Invoices;

namespace InvoicePlatform.Application.Invoices;

/// <summary>
/// Persistence for invoices. Every method takes the organisation explicitly:
/// there is no ambient tenant, so a caller cannot forget to scope a query
/// (CLAUDE.md, hard rule 4).
/// </summary>
public interface IInvoiceRepository
{
    Task SaveAsync(StoredInvoice invoice, CancellationToken cancellationToken = default);

    Task<StoredInvoice?> GetAsync(
        string organizationId,
        string invoiceId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StoredInvoice>> ListAsync(
        string organizationId,
        int limit = 100,
        CancellationToken cancellationToken = default);
}

/// <summary>Object storage for the source documents.</summary>
public interface IDocumentStore
{
    /// <returns>The key under which the document was stored.</returns>
    Task<string> PutAsync(
        string organizationId,
        string invoiceId,
        ReadOnlyMemory<byte> content,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<byte[]?> GetAsync(string documentKey, CancellationToken cancellationToken = default);
}
