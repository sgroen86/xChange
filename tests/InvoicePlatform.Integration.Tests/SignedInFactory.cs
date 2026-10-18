using InvoicePlatform.Application.Identity;
using InvoicePlatform.Application.Invoices;
using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.Identity;
using InvoicePlatform.Domain.Invoices;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InvoicePlatform.Integration.Tests;

/// <summary>
/// The real API with in-memory stores and a fixed set of signed-in users, so
/// role and tenant rules can be tested end to end without AWS credentials.
///
/// Tokens: admin-a, user-a and readonly-a belong to org-a; admin-b to org-b.
/// </summary>
public sealed class SignedInFactory : WebApplicationFactory<Program>
{
    public const string OrgA = "org-a";
    public const string OrgB = "org-b";

    public InMemoryInvoices Invoices { get; } = new();

    public InMemorySettings Settings { get; } = new();

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

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IUserStore>();
            services.RemoveAll<ISessionStore>();
            services.RemoveAll<ILoginAttemptStore>();
            services.RemoveAll<IOrganizationSettingsStore>();
            services.RemoveAll<IInvoiceRepository>();
            services.RemoveAll<IDocumentStore>();

            var identity = new SignedInIdentity();
            services.AddSingleton<IUserStore>(identity);
            services.AddSingleton<ISessionStore>(identity);
            services.AddSingleton<ILoginAttemptStore>(identity);
            services.AddSingleton<IOrganizationSettingsStore>(Settings);
            services.AddSingleton<IInvoiceRepository>(Invoices);
            services.AddSingleton<IDocumentStore>(Invoices);
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
