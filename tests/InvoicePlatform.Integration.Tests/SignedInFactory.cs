using InvoicePlatform.Application.Identity;
using InvoicePlatform.Application.Imports;
using InvoicePlatform.Application.Invoices;
using InvoicePlatform.Application.Learning;
using InvoicePlatform.Application.Networks;
using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Application.Relations;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.Identity;
using InvoicePlatform.Domain.Imports;
using InvoicePlatform.Domain.Invoices;
using InvoicePlatform.Domain.Learning;
using InvoicePlatform.Domain.Networks;
using InvoicePlatform.Domain.Organizations;
using InvoicePlatform.Domain.Relations;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InvoicePlatform.Integration.Tests;

/// <summary>
/// The real API with in-memory stores and a fixed set of signed-in users, so
/// role and tenant rules can be tested end to end without AWS credentials.
///
/// Tokens: admin-a, user-a and readonly-a belong to org-a; admin-b to org-b;
/// platform-a is a platform administrator, and Admin of org-a.
/// </summary>
public sealed class SignedInFactory : WebApplicationFactory<Program>
{
    public const string OrgA = "org-a";
    public const string OrgB = "org-b";

    public InMemoryInvoices Invoices { get; } = new();

    public InMemorySettings Settings { get; } = new();

    public InMemoryRelations Relations { get; } = new();

    public InMemoryPlatform Platform { get; } = new();

    /// <summary>The official verdict every export gets; by default "no failures".</summary>
    public StubOfficialValidator Official { get; } = new();

    public StubReceiverNetwork Network { get; } = new();

    public InMemoryLearningStore Learning { get; } = new();

    public InMemoryImports Imports { get; } = new();

    public InMemoryInboxDirectory Inboxes { get; } = new();

    public HttpClient As(string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        return client;
    }

    /// <summary>Stores a booked invoice directly, as if it had been booked through the API.</summary>
    public async Task<string> SeedInvoiceAsync(string organizationId, CanonicalInvoiceDraft draft)
    {
        var id = Guid.NewGuid().ToString("n");
        await Invoices.SaveAsync(new StoredInvoice
        {
            InvoiceId = id,
            OrganizationId = organizationId,
            Status = InvoiceStatus.Booked,
            Invoice = draft,
            CreatedAt = DateTimeOffset.UnixEpoch,
            CreatedByUserId = "seed",
        });
        return id;
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("Anthropic:ApiKey", "test-key-never-used");
        // The production origin, so CORS behaves as it does for the real site.
        builder.UseSetting("Cors:AllowedOrigins:0", "https://greenitsolutions.net");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IUserStore>();
            services.RemoveAll<ISessionStore>();
            services.RemoveAll<ILoginAttemptStore>();
            services.RemoveAll<IOrganizationSettingsStore>();
            services.RemoveAll<IInvoiceRepository>();
            services.RemoveAll<IDocumentStore>();
            services.RemoveAll<IRelationRepository>();
            services.RemoveAll<IPlatformStore>();
            services.RemoveAll<IOfficialValidator>();
            services.RemoveAll<IReceiverNetwork>();
            services.RemoveAll<ILearningStore>();
            services.RemoveAll<IImportRepository>();
            services.RemoveAll<IImportQueue>();
            services.RemoveAll<IInboxDirectory>();

            var identity = new SignedInIdentity();
            services.AddSingleton<IUserStore>(identity);
            services.AddSingleton<ISessionStore>(identity);
            services.AddSingleton<ILoginAttemptStore>(identity);
            services.AddSingleton<IOrganizationSettingsStore>(Settings);
            services.AddSingleton<IInvoiceRepository>(Invoices);
            services.AddSingleton<IDocumentStore>(Invoices);
            services.AddSingleton<IRelationRepository>(Relations);
            services.AddSingleton<IPlatformStore>(Platform);
            services.AddSingleton<IOfficialValidator>(Official);
            services.AddSingleton<IReceiverNetwork>(Network);
            services.AddSingleton<ILearningStore>(Learning);
            services.AddSingleton<IImportRepository>(Imports);
            services.AddSingleton<IImportQueue>(Imports);
            services.AddSingleton<IInboxDirectory>(Inboxes);
        });
    }

    private sealed class SignedInIdentity : IUserStore, ISessionStore, ILoginAttemptStore
    {
        private static readonly User[] Users =
        [
            new() { Id = "admin-a", OrganizationId = OrgA, Email = "admin@a", PasswordHash = "-", Name = "Admin A", Role = UserRole.Admin },
            new() { Id = "user-a", OrganizationId = OrgA, Email = "user@a", PasswordHash = "-", Name = "User A", Role = UserRole.User },
            new() { Id = "readonly-a", OrganizationId = OrgA, Email = "readonly@a", PasswordHash = "-", Name = "Readonly A", Role = UserRole.ReadOnly },
            new() { Id = "admin-b", OrganizationId = OrgB, Email = "admin@b", PasswordHash = "-", Name = "Admin B", Role = UserRole.Admin },
            new() { Id = "platform-a", OrganizationId = OrgA, Email = "platform@a", PasswordHash = "-", Name = "Platform", Role = UserRole.Admin, IsPlatformAdmin = true },
        ];

        // The token is the user id: good enough for a test, never for anything else.
        public Task<AuthSession?> FindAsync(string token, CancellationToken cancellationToken = default)
            => Task.FromResult(Users.FirstOrDefault(u => u.Id == token) is { } user
                ? new AuthSession(token, user.Id, user.OrganizationId, DateTimeOffset.UnixEpoch, DateTimeOffset.MaxValue)
                : null);

        public Task<User?> FindByIdAsync(string organizationId, string userId, CancellationToken cancellationToken = default)
            => Task.FromResult(Users.FirstOrDefault(u => u.OrganizationId == organizationId && u.Id == userId));

        public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
            => Task.FromResult(Users.FirstOrDefault(u => u.Email == email));

        public Task<bool> AnyUsersExistAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task AddAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateLastLoginAsync(string organizationId, string userId, DateTimeOffset at, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SaveAsync(AuthSession session, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(string token, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<LoginAttempts?> GetAsync(string clientKey, CancellationToken cancellationToken = default)
            => Task.FromResult<LoginAttempts?>(null);

        public Task SaveAsync(LoginAttempts attempts, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ClearAsync(string clientKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

public sealed class InMemoryInvoices : IInvoiceRepository, IDocumentStore
{
    private readonly Dictionary<(string Org, string Id), StoredInvoice> _invoices = [];
    private readonly Dictionary<string, byte[]> _documents = [];

    public Task SaveAsync(StoredInvoice invoice, CancellationToken cancellationToken = default)
    {
        lock (_invoices)
        {
            _invoices[(invoice.OrganizationId, invoice.InvoiceId)] = invoice;
        }

        return Task.CompletedTask;
    }

    public Task<StoredInvoice?> GetAsync(string organizationId, string invoiceId, CancellationToken cancellationToken = default)
    {
        lock (_invoices)
        {
            return Task.FromResult(_invoices.GetValueOrDefault((organizationId, invoiceId)));
        }
    }

    public Task<IReadOnlyList<StoredInvoice>> ListAsync(string organizationId, int limit = 100, CancellationToken cancellationToken = default)
    {
        lock (_invoices)
        {
            return Task.FromResult<IReadOnlyList<StoredInvoice>>(
                _invoices.Where(kv => kv.Key.Org == organizationId).Select(kv => kv.Value).ToList());
        }
    }

    public Task<string> PutAsync(string organizationId, string invoiceId, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken = default)
    {
        var key = $"{organizationId}/{invoiceId}.pdf";
        lock (_documents)
        {
            _documents[key] = content.ToArray();
        }

        return Task.FromResult(key);
    }

    public Task<byte[]?> GetAsync(string documentKey, CancellationToken cancellationToken = default)
    {
        lock (_documents)
        {
            return Task.FromResult(_documents.GetValueOrDefault(documentKey));
        }
    }
}

public sealed class InMemorySettings : IOrganizationSettingsStore
{
    private readonly Dictionary<string, OrganizationSettings> _settings = [];

    public Task<OrganizationSettings?> GetAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        lock (_settings)
        {
            return Task.FromResult(_settings.GetValueOrDefault(organizationId));
        }
    }

    public Task SaveAsync(OrganizationSettings settings, CancellationToken cancellationToken = default)
    {
        lock (_settings)
        {
            _settings[settings.OrganizationId] = settings;
        }

        return Task.CompletedTask;
    }
}

public sealed class InMemoryRelations : IRelationRepository
{
    private readonly Dictionary<(string Org, string Id), Relation> _relations = [];

    public Task<IReadOnlyList<Relation>> ListAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        lock (_relations)
        {
            return Task.FromResult<IReadOnlyList<Relation>>(
                _relations.Where(kv => kv.Key.Org == organizationId).Select(kv => kv.Value).ToList());
        }
    }

    public Task<Relation?> GetAsync(string organizationId, string relationId, CancellationToken cancellationToken = default)
    {
        lock (_relations)
        {
            return Task.FromResult(_relations.GetValueOrDefault((organizationId, relationId)));
        }
    }

    public Task SaveAsync(Relation relation, CancellationToken cancellationToken = default)
    {
        lock (_relations)
        {
            _relations[(relation.OrganizationId, relation.RelationId)] = relation;
        }

        return Task.CompletedTask;
    }
}

public sealed class InMemoryPlatform : IPlatformStore
{
    private readonly Dictionary<string, Organization> _organizations = [];
    private readonly Dictionary<string, Invitation> _invitations = [];

    public Task<Organization?> FindOrganizationAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        lock (_organizations)
        {
            return Task.FromResult(_organizations.GetValueOrDefault(organizationId));
        }
    }

    public Task<IReadOnlyList<Organization>> ListOrganizationsAsync(CancellationToken cancellationToken = default)
    {
        lock (_organizations)
        {
            return Task.FromResult<IReadOnlyList<Organization>>(_organizations.Values.ToList());
        }
    }

    public Task SaveOrganizationAsync(Organization organization, CancellationToken cancellationToken = default)
    {
        lock (_organizations)
        {
            _organizations[organization.OrganizationId] = organization;
        }

        return Task.CompletedTask;
    }

    public Task<int> CountUsersAsync(string organizationId, CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public Task SaveInvitationAsync(Invitation invitation, CancellationToken cancellationToken = default)
    {
        lock (_invitations)
        {
            _invitations[invitation.CodeHash] = invitation;
        }

        return Task.CompletedTask;
    }

    public Task<Invitation?> FindInvitationAsync(string codeHash, CancellationToken cancellationToken = default)
    {
        lock (_invitations)
        {
            return Task.FromResult(_invitations.GetValueOrDefault(codeHash));
        }
    }

    public Task<bool> TryDeleteInvitationAsync(string codeHash, CancellationToken cancellationToken = default)
    {
        lock (_invitations)
        {
            return Task.FromResult(_invitations.Remove(codeHash));
        }
    }
}

/// <summary>Inbox addresses in memory, both ways, as the identity table keeps them.</summary>
public sealed class InMemoryInboxDirectory : IInboxDirectory
{
    private readonly Dictionary<string, string> _byLocal = [];
    private readonly Dictionary<string, string> _byOrg = [];

    public Task<string?> FindOrganizationAsync(string localPart, CancellationToken cancellationToken = default)
    {
        lock (_byLocal)
        {
            return Task.FromResult(_byLocal.GetValueOrDefault(localPart));
        }
    }

    public Task<string?> GetLocalPartAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        lock (_byLocal)
        {
            return Task.FromResult(_byOrg.GetValueOrDefault(organizationId));
        }
    }

    public Task AssignAsync(string organizationId, string localPart, string? previousLocalPart, CancellationToken cancellationToken = default)
    {
        lock (_byLocal)
        {
            _byLocal[localPart] = organizationId;
            _byOrg[organizationId] = localPart;
            if (previousLocalPart is not null)
            {
                _byLocal.Remove(previousLocalPart);
            }
        }

        return Task.CompletedTask;
    }
}

/// <summary>Imports and their queue in memory; the queue only records what was put on it.</summary>
public sealed class InMemoryImports : IImportRepository, IImportQueue
{
    private readonly Dictionary<(string Org, string Id), ImportRecord> _records = [];

    public List<(string Org, string Id)> Queued { get; } = [];

    public Task SaveAsync(ImportRecord record, CancellationToken cancellationToken = default)
    {
        lock (_records)
        {
            _records[(record.OrganizationId, record.ImportId)] = record;
        }

        return Task.CompletedTask;
    }

    public Task<ImportRecord?> GetAsync(string organizationId, string importId, CancellationToken cancellationToken = default)
    {
        lock (_records)
        {
            return Task.FromResult(_records.GetValueOrDefault((organizationId, importId)));
        }
    }

    public Task<IReadOnlyList<ImportRecord>> ListAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        lock (_records)
        {
            return Task.FromResult<IReadOnlyList<ImportRecord>>(_records.Where(kv => kv.Key.Org == organizationId).Select(kv => kv.Value).ToList());
        }
    }

    public Task DeleteAsync(string organizationId, string importId, CancellationToken cancellationToken = default)
    {
        lock (_records)
        {
            _records.Remove((organizationId, importId));
        }

        return Task.CompletedTask;
    }

    public Task EnqueueAsync(string organizationId, string importId, CancellationToken cancellationToken = default)
    {
        lock (Queued)
        {
            Queued.Add((organizationId, importId));
        }

        return Task.CompletedTask;
    }
}

/// <summary>Learning data in memory, keyed by organisation as DynamoDB keys it.</summary>
public sealed class InMemoryLearningStore : ILearningStore
{
    private readonly Dictionary<(string Org, string Id), ExtractionSnapshot> _extractions = [];
    private readonly Dictionary<string, IReadOnlyList<LearnedCorrection>> _corrections = [];

    public Task SaveExtractionAsync(string organizationId, ExtractionSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        lock (_extractions)
        {
            _extractions[(organizationId, snapshot.ExtractionId)] = snapshot;
        }

        return Task.CompletedTask;
    }

    public Task<ExtractionSnapshot?> TakeExtractionAsync(string organizationId, string extractionId, CancellationToken cancellationToken = default)
    {
        lock (_extractions)
        {
            return Task.FromResult(_extractions.Remove((organizationId, extractionId), out var snapshot) ? snapshot : null);
        }
    }

    public Task<IReadOnlyList<LearnedCorrection>> GetCorrectionsAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        lock (_corrections)
        {
            return Task.FromResult(_corrections.GetValueOrDefault(organizationId) ?? []);
        }
    }

    public Task SaveCorrectionsAsync(string organizationId, IReadOnlyList<LearnedCorrection> corrections, CancellationToken cancellationToken = default)
    {
        lock (_corrections)
        {
            _corrections[organizationId] = corrections;
        }

        return Task.CompletedTask;
    }
}

/// <summary>A PEPPOL network without the network: registered participants are listed by hand.</summary>
public sealed class StubReceiverNetwork : IReceiverNetwork
{
    public Dictionary<string, NetworkAnswer> Answers { get; } = [];

    public string Name => "peppol";

    public Task<NetworkAnswer> FindAsync(ParticipantIdentifier participant, CancellationToken cancellationToken = default)
        => Task.FromResult(Answers.TryGetValue(participant.ToString(), out var answer) ? answer : NetworkAnswer.NotRegistered);
}

public sealed class StubOfficialValidator : IOfficialValidator
{
    public OfficialValidation Verdict { get; set; } = new(true, "3.0.21", []);

    public Task<OfficialValidation> ValidateAsync(string rulesetId, string xml, CancellationToken cancellationToken = default)
        => Task.FromResult(Verdict);
}
