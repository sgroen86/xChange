using InvoicePlatform.Application.Identity;
using InvoicePlatform.Application.Invoices;
using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Application.Relations;
using InvoicePlatform.Domain.Identity;
using InvoicePlatform.Domain.Invoices;
using InvoicePlatform.Domain.Organizations;
using InvoicePlatform.Domain.Relations;

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

    public Task DeleteAsync(string organizationId, string invoiceId, CancellationToken cancellationToken = default)
    {
        _invoices.Remove((organizationId, invoiceId));
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string documentKey, CancellationToken cancellationToken = default)
    {
        Documents.Remove(documentKey);
        return Task.CompletedTask;
    }
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

internal sealed class FakeRelationStore : IRelationRepository
{
    private readonly Dictionary<(string Org, string Id), Relation> _relations = [];

    public IReadOnlyCollection<Relation> All => _relations.Values;

    public Task<IReadOnlyList<Relation>> ListAsync(string organizationId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Relation>>(
            _relations.Where(kv => kv.Key.Org == organizationId).Select(kv => kv.Value).ToList());

    public Task<Relation?> GetAsync(string organizationId, string relationId, CancellationToken cancellationToken = default)
        => Task.FromResult(_relations.GetValueOrDefault((organizationId, relationId)));

    public Task SaveAsync(Relation relation, CancellationToken cancellationToken = default)
    {
        _relations[(relation.OrganizationId, relation.RelationId)] = relation;
        return Task.CompletedTask;
    }
}

internal sealed class FakePlatformStore : IPlatformStore
{
    private PlatformSettings _settings = new();

    public Task<PlatformSettings> GetSettingsAsync(CancellationToken cancellationToken = default) => Task.FromResult(_settings);

    public Task SaveSettingsAsync(PlatformSettings settings, CancellationToken cancellationToken = default)
    {
        _settings = settings;
        return Task.CompletedTask;
    }

    private readonly Dictionary<string, Organization> _organizations = [];
    private readonly Dictionary<string, Invitation> _invitations = [];

    public IReadOnlyDictionary<string, Invitation> Invitations => _invitations;

    public Func<string, int> UserCount { get; set; } = _ => 0;

    public Task<Organization?> FindOrganizationAsync(string organizationId, CancellationToken cancellationToken = default)
        => Task.FromResult(_organizations.GetValueOrDefault(organizationId));

    public Task<IReadOnlyList<Organization>> ListOrganizationsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Organization>>(_organizations.Values.ToList());

    public Task SaveOrganizationAsync(Organization organization, CancellationToken cancellationToken = default)
    {
        _organizations[organization.OrganizationId] = organization;
        return Task.CompletedTask;
    }

    public Task<int> CountUsersAsync(string organizationId, CancellationToken cancellationToken = default)
        => Task.FromResult(UserCount(organizationId));

    /// <summary>What deleting an organisation's record does to this item (the same item in DynamoDB).</summary>
    public bool RemoveOrganization(string organizationId) => _organizations.Remove(organizationId);

    public Task SaveInvitationAsync(Invitation invitation, CancellationToken cancellationToken = default)
    {
        _invitations[invitation.CodeHash] = invitation;
        return Task.CompletedTask;
    }

    public Task<Invitation?> FindInvitationAsync(string codeHash, CancellationToken cancellationToken = default)
        => Task.FromResult(_invitations.GetValueOrDefault(codeHash));

    public Task<bool> TryDeleteInvitationAsync(string codeHash, CancellationToken cancellationToken = default)
        => Task.FromResult(_invitations.Remove(codeHash));
}

/// <summary>Answers with whatever verdict the test sets; by default "no failures".</summary>
internal sealed class FakeOfficialValidator : IOfficialValidator
{
    public OfficialValidation Verdict { get; set; } = new(true, "3.0.21", []);

    public int Calls { get; private set; }

    public Task<OfficialValidation> ValidateAsync(string rulesetId, string xml, CancellationToken cancellationToken = default)
    {
        Calls++;
        return Task.FromResult(Verdict);
    }
}
