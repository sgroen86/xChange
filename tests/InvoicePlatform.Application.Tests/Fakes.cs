using InvoicePlatform.Application.Identity;
using InvoicePlatform.Application.Invoices;
using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Domain.Invoices;

namespace InvoicePlatform.Application.Tests;

internal sealed class TestClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;
}

/// <summary>Keyed by organisation and id, like the real table's partition and sort key.</summary>
internal sealed class FakeInvoiceStore : IInvoiceRepository, IDocumentStore
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

internal sealed class FakeSettingsStore : IOrganizationSettingsStore
{
    private readonly Dictionary<string, OrganizationSettings> _settings = [];

    public Task<OrganizationSettings?> GetAsync(string organizationId, CancellationToken cancellationToken = default)
        => Task.FromResult(_settings.GetValueOrDefault(organizationId));

    public Task SaveAsync(OrganizationSettings settings, CancellationToken cancellationToken = default)
    {
        _settings[settings.OrganizationId] = settings;
        return Task.CompletedTask;
    }
}
