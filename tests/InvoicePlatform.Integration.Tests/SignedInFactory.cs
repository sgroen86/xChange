using InvoicePlatform.Application.Quality;
using InvoicePlatform.Application.Billing;
using InvoicePlatform.Application.Identity;
using InvoicePlatform.Application.Imports;
using InvoicePlatform.Application.Invoices;
using InvoicePlatform.Application.Learning;
using InvoicePlatform.Application.Networks;
using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Application.Relations;
using InvoicePlatform.Application.Usage;
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

    public InMemorySignups Signups { get; } = new();

    public CapturedMail Mail { get; } = new();

    /// <summary>Conversions and billing state: one record per organisation, as in DynamoDB.</summary>
    public InMemoryUsage Usage { get; } = new();

    public InMemoryUsageLedger Ledger { get; } = new();

    public InMemoryQualityLedger Quality { get; } = new();

    /// <summary>Two-step verification state; the secrets "protected" by <see cref="TestProtector"/>.</summary>
    public InMemoryMfa Mfa { get; } = new();

    /// <summary>The fixed users plus whoever signs up, accepts an invitation or is added with <see cref="AddUserAsync"/>.</summary>
    internal SignedInIdentity Identity { get; } = new();

    /// <summary>Adds an account that logs in with a real password, checked as the API checks it.</summary>
    public async Task<User> AddUserAsync(string email, string password, UserRole role, string organizationId = OrgA)
    {
        var hasher = Services.GetRequiredService<IPasswordHasher>();
        var user = new User
        {
            Id = Guid.NewGuid().ToString("n"),
            OrganizationId = organizationId,
            Email = email,
            PasswordHash = hasher.Hash(password),
            Name = email,
            Role = role,
        };
        await Identity.AddAsync(user);
        return user;
    }

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
            services.RemoveAll<ISignupStore>();
            services.RemoveAll<IEmailSender>();
            services.RemoveAll<IUsageMeter>();
            services.RemoveAll<IBillingStore>();
            services.RemoveAll<IUsageLedger>();
            services.RemoveAll<IQualityLedger>();
            services.RemoveAll<IMfaStore>();
            services.RemoveAll<ISecretProtector>();

            services.AddSingleton<IUserStore>(Identity);
            services.AddSingleton<ISessionStore>(Identity);
            services.AddSingleton<ILoginAttemptStore>(Identity);
            services.AddSingleton<IMfaStore>(Mfa);
            services.AddSingleton<ISecretProtector>(new TestProtector());
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
            services.AddSingleton<ISignupStore>(Signups);
            services.AddSingleton<IEmailSender>(Mail);
            services.AddSingleton<IUsageMeter>(Usage);
            services.AddSingleton<IBillingStore>(Usage);
            services.AddSingleton<IUsageLedger>(Ledger);
            services.AddSingleton<IQualityLedger>(Quality);
        });
    }

    internal sealed class SignedInIdentity : IUserStore, ISessionStore, ILoginAttemptStore
    {
        private readonly Dictionary<string, AuthSession> _sessions = [];

        private readonly List<User> _users =
        [
            new() { Id = "admin-a", OrganizationId = OrgA, Email = "admin@a", PasswordHash = "-", Name = "Admin A", Role = UserRole.Admin },
            new() { Id = "user-a", OrganizationId = OrgA, Email = "user@a", PasswordHash = "-", Name = "User A", Role = UserRole.User },
            new() { Id = "readonly-a", OrganizationId = OrgA, Email = "readonly@a", PasswordHash = "-", Name = "Readonly A", Role = UserRole.ReadOnly },
            new() { Id = "admin-b", OrganizationId = OrgB, Email = "admin@b", PasswordHash = "-", Name = "Admin B", Role = UserRole.Admin },
            new() { Id = "platform-a", OrganizationId = OrgA, Email = "platform@a", PasswordHash = "-", Name = "Platform", Role = UserRole.Admin, IsPlatformAdmin = true },
        ];

        /// <summary>
        /// A session issued by a login during the test, or else a fixed one:
        /// for the fixed users the token is the user id - good enough for a
        /// test, never for anything else. Looked up by hash, as the real store is.
        /// </summary>
        public Task<AuthSession?> FindAsync(string tokenHash, CancellationToken cancellationToken = default)
        {
            lock (_users)
            {
                if (_sessions.TryGetValue(tokenHash, out var issued))
                {
                    return Task.FromResult<AuthSession?>(issued);
                }

                return Task.FromResult(_users.FirstOrDefault(u => AuthSession.HashOf(u.Id) == tokenHash) is { } user
                    ? new AuthSession(tokenHash, user.Id, user.OrganizationId, DateTimeOffset.UnixEpoch, DateTimeOffset.MaxValue)
                    : null);
            }
        }

        public Task<User?> FindByIdAsync(string organizationId, string userId, CancellationToken cancellationToken = default)
        {
            lock (_users)
            {
                return Task.FromResult(_users.FirstOrDefault(u => u.OrganizationId == organizationId && u.Id == userId));
            }
        }

        public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            lock (_users)
            {
                return Task.FromResult(_users.FirstOrDefault(u => u.Email == email));
            }
        }

        public Task<bool> AnyUsersExistAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            lock (_users)
            {
                if (_users.Any(u => u.Email == user.Email))
                {
                    throw new InvalidOperationException("duplicate");
                }

                _users.Add(user);
            }

            return Task.CompletedTask;
        }

        public Task UpdateLastLoginAsync(string organizationId, string userId, DateTimeOffset at, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SaveAsync(AuthSession session, CancellationToken cancellationToken = default)
        {
            lock (_users)
            {
                _sessions[session.TokenHash] = session;
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync(string tokenHash, CancellationToken cancellationToken = default)
        {
            lock (_users)
            {
                _sessions.Remove(tokenHash);
            }

            return Task.CompletedTask;
        }

        public Task<LoginAttempts?> GetAsync(string clientKey, CancellationToken cancellationToken = default)
            => Task.FromResult<LoginAttempts?>(null);

        public Task SaveAsync(LoginAttempts attempts, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ClearAsync(string clientKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

/// <summary>MFA state and challenges in memory, conditional where the DynamoDB store is.</summary>
public sealed class InMemoryMfa : IMfaStore
{
    private readonly Dictionary<(string Org, string User), UserMfa> _users = [];
    private readonly Dictionary<string, MfaChallenge> _challenges = [];

    public UserMfa? Find(string organizationId, string userId)
    {
        lock (_users)
        {
            return _users.GetValueOrDefault((organizationId, userId));
        }
    }

    public Task<UserMfa?> GetAsync(string organizationId, string userId, CancellationToken cancellationToken = default)
        => Task.FromResult(Find(organizationId, userId));

    public Task SaveAsync(UserMfa mfa, CancellationToken cancellationToken = default)
    {
        lock (_users)
        {
            _users[(mfa.OrganizationId, mfa.UserId)] = mfa;
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(string organizationId, string userId, CancellationToken cancellationToken = default)
    {
        lock (_users)
        {
            _users.Remove((organizationId, userId));
        }

        return Task.CompletedTask;
    }

    public Task<bool> TryUseStepAsync(string organizationId, string userId, long step, CancellationToken cancellationToken = default)
    {
        lock (_users)
        {
            if (!_users.TryGetValue((organizationId, userId), out var mfa) || mfa.LastUsedStep >= step)
            {
                return Task.FromResult(false);
            }

            _users[(organizationId, userId)] = mfa with { LastUsedStep = step };
            return Task.FromResult(true);
        }
    }

    public Task<int?> TryConsumeRecoveryCodeAsync(string organizationId, string userId, string codeHash, CancellationToken cancellationToken = default)
    {
        lock (_users)
        {
            if (!_users.TryGetValue((organizationId, userId), out var mfa) || !mfa.RecoveryCodeHashes.Contains(codeHash))
            {
                return Task.FromResult<int?>(null);
            }

            var left = mfa.RecoveryCodeHashes.Where(h => h != codeHash).ToList();
            _users[(organizationId, userId)] = mfa with { RecoveryCodeHashes = left };
            return Task.FromResult<int?>(left.Count);
        }
    }

    public Task SetRecoveryCodesAsync(string organizationId, string userId, IReadOnlyList<string> codeHashes, CancellationToken cancellationToken = default)
    {
        lock (_users)
        {
            _users[(organizationId, userId)] = _users[(organizationId, userId)] with { RecoveryCodeHashes = codeHashes };
        }

        return Task.CompletedTask;
    }

    public Task SaveChallengeAsync(MfaChallenge challenge, CancellationToken cancellationToken = default)
    {
        lock (_challenges)
        {
            _challenges[challenge.TokenHash] = challenge;
        }

        return Task.CompletedTask;
    }

    public Task<MfaChallenge?> FindChallengeAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        lock (_challenges)
        {
            return Task.FromResult(_challenges.GetValueOrDefault(tokenHash));
        }
    }

    public Task<int> RecordChallengeFailureAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        lock (_challenges)
        {
            if (!_challenges.TryGetValue(tokenHash, out var challenge))
            {
                return Task.FromResult(MfaChallenge.MaxFailedAttempts);
            }

            _challenges[tokenHash] = challenge with { FailedAttempts = challenge.FailedAttempts + 1 };
            return Task.FromResult(challenge.FailedAttempts + 1);
        }
    }

    public Task<bool> TryDeleteChallengeAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        lock (_challenges)
        {
            return Task.FromResult(_challenges.Remove(tokenHash));
        }
    }
}

/// <summary>Stands in for KMS: not plain text, and bound to its context as KMS binds encryption context.</summary>
public sealed class TestProtector : ISecretProtector
{
    public Task<string> ProtectAsync(byte[] plaintext, string context, CancellationToken cancellationToken = default)
        => Task.FromResult($"{context}|{Convert.ToBase64String(plaintext.Select(b => (byte)(b ^ 0x5a)).ToArray())}");

    public Task<byte[]> UnprotectAsync(string protectedValue, string context, CancellationToken cancellationToken = default)
    {
        var parts = protectedValue.Split('|');
        return parts[0] == context
            ? Task.FromResult(Convert.FromBase64String(parts[1]).Select(b => (byte)(b ^ 0x5a)).ToArray())
            : throw new System.Security.Cryptography.CryptographicException("Wrong context.");
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

    public Task DeleteAsync(string organizationId, string invoiceId, CancellationToken cancellationToken = default)
    {
        lock (_invoices)
        {
            _invoices.Remove((organizationId, invoiceId));
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(string documentKey, CancellationToken cancellationToken = default)
    {
        lock (_documents)
        {
            _documents.Remove(documentKey);
        }

        return Task.CompletedTask;
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
    private PlatformSettings _settings = new();

    public Task<PlatformSettings> GetSettingsAsync(CancellationToken cancellationToken = default) => Task.FromResult(_settings);

    public Task SaveSettingsAsync(PlatformSettings settings, CancellationToken cancellationToken = default)
    {
        _settings = settings;
        return Task.CompletedTask;
    }

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

/// <summary>Pending sign-ups in memory; taking one removes it, as the conditional delete does.</summary>
public sealed class InMemorySignups : ISignupStore
{
    private readonly Dictionary<string, PendingSignup> _byHash = [];

    public Task SaveAsync(PendingSignup signup, CancellationToken cancellationToken = default)
    {
        lock (_byHash)
        {
            _byHash[signup.CodeHash] = signup;
        }

        return Task.CompletedTask;
    }

    public Task<PendingSignup?> TryTakeAsync(string codeHash, CancellationToken cancellationToken = default)
    {
        lock (_byHash)
        {
            return Task.FromResult(_byHash.Remove(codeHash, out var signup) ? signup : null);
        }
    }
}

/// <summary>Mail that would have gone out through SES.</summary>
public sealed class CapturedMail : IEmailSender
{
    public List<(string To, string Subject, string Body)> Sent { get; } = [];

    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        lock (Sent)
        {
            Sent.Add((to, subject, body));
        }

        return Task.CompletedTask;
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
