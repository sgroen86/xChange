using InvoicePlatform.Application.Billing;
using InvoicePlatform.Application.Identity;
using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Domain.Identity;
using InvoicePlatform.Domain.Organizations;
using Microsoft.Extensions.Logging.Abstractions;

namespace InvoicePlatform.Application.Tests;

/// <summary>
/// Deleting an organisation: refused for the caller's own organisation, one
/// with a platform administrator, or one whose subscription still runs; and
/// when it goes, everything of it goes - access first, its record last - and
/// nothing of another organisation.
/// </summary>
public class OrganizationDeletionServiceTests
{
    private const string Doomed = "org-x";
    private const string Other = "org-y";
    private const string Caller = "org-platform";

    private readonly FakePlatformStore _platform = new();
    private readonly DeletionLog _log = new();
    private readonly FakeIdentityDeletion _identity;
    private readonly FakeRecordDeletion _records;
    private readonly FakeDocumentDeletion _documents;
    private readonly RecordingAttempts _attempts = new();
    private readonly FakeUsageMeter _billing = new();
    private readonly OrganizationDeletionService _service;

    public OrganizationDeletionServiceTests()
    {
        _identity = new FakeIdentityDeletion(_log, _platform);
        _records = new FakeRecordDeletion(_log);
        _documents = new FakeDocumentDeletion(_log);
        _service = new OrganizationDeletionService(
            _platform, _identity, _attempts, _billing, _records, _documents, NullLogger<OrganizationDeletionService>.Instance);

        foreach (var (id, name) in new[] { (Doomed, "Acme BV"), (Other, "Other BV"), (Caller, "xChange") })
        {
            Seed(id, name);
        }

        _identity.Users.Add(NewUser("platform", Caller, "platform@xchange.test", platformAdmin: true));
    }

    private void Seed(string organizationId, string name)
    {
        var invitation = new Invitation($"hash-{organizationId}", organizationId, $"new@{organizationId}.test", "New", DateTimeOffset.UnixEpoch, DateTimeOffset.MaxValue);
        _platform.SaveInvitationAsync(invitation).Wait();
        _platform.SaveOrganizationAsync(new Organization
        {
            OrganizationId = organizationId,
            Name = name,
            CountryCode = "NL",
            CreatedAt = DateTimeOffset.UnixEpoch,
            PendingInvitationHash = invitation.CodeHash,
        }).Wait();

        _identity.Users.Add(NewUser($"admin-{organizationId}", organizationId, $"admin@{organizationId}.test"));
        _identity.Users.Add(NewUser($"user-{organizationId}", organizationId, $"user@{organizationId}.test"));
        _identity.Mfa.Add(organizationId);
        _identity.Inbox.Add(organizationId);
        _identity.Settings.Add(organizationId);
        _records.PlatformSums.Add(organizationId);
        _records.Partitions.Add(organizationId);
        _records.CustomerLinks[$"cus_{organizationId}"] = organizationId;
        _billing[organizationId].CustomerId = $"cus_{organizationId}";
        _documents.Keys.Add($"{organizationId}/invoice-1.pdf");
        _documents.Keys.Add($"{organizationId}/import-1.pdf");
    }

    private static User NewUser(string id, string organizationId, string email, bool platformAdmin = false) => new()
    {
        Id = id,
        OrganizationId = organizationId,
        Email = email,
        PasswordHash = "-",
        Name = id,
        Role = UserRole.Admin,
        IsPlatformAdmin = platformAdmin,
    };

    private Task<OrganizationDeletionResult> DeleteAsync(string organizationId, string? confirmName)
        => _service.DeleteAsync("platform", Caller, organizationId, confirmName);

    private async Task AssertNothingDeletedAsync()
    {
        Assert.Empty(_log.Calls);
        Assert.Empty(_attempts.Cleared);
        Assert.Equal(3, (await _platform.ListOrganizationsAsync()).Count);
        Assert.Equal(3, _platform.Invitations.Count);
    }

    [Fact]
    public async Task An_unknown_organisation_is_not_found()
    {
        var result = await DeleteAsync("nope", "nope");

        Assert.Equal(OrganizationDeletionOutcome.NotFound, result.Outcome);
        await AssertNothingDeletedAsync();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("acme bv")]
    [InlineData("Acme")]
    [InlineData("Other BV")]
    public async Task The_confirmation_must_be_the_exact_name(string? confirmName)
    {
        var result = await DeleteAsync(Doomed, confirmName);

        Assert.Equal(OrganizationDeletionOutcome.Invalid, result.Outcome);
        Assert.NotNull(result.Error);
        await AssertNothingDeletedAsync();
    }

    [Fact]
    public async Task The_callers_own_organisation_is_refused()
    {
        var result = await DeleteAsync(Caller, "xChange");

        Assert.Equal(OrganizationDeletionOutcome.Refused, result.Outcome);
        Assert.Contains("eigen organisatie", result.Error);
        await AssertNothingDeletedAsync();
    }

    [Fact]
    public async Task An_organisation_with_a_platform_administrator_is_refused()
    {
        _identity.Users.Add(NewUser("second-platform", Doomed, "second@x.test", platformAdmin: true));

        var result = await DeleteAsync(Doomed, "Acme BV");

        Assert.Equal(OrganizationDeletionOutcome.Refused, result.Outcome);
        Assert.Contains("platformbeheerder", result.Error);
        await AssertNothingDeletedAsync();
    }

    [Theory]
    [InlineData(SubscriptionStatuses.Active)]
    [InlineData(SubscriptionStatuses.Trialing)]
    [InlineData(SubscriptionStatuses.PastDue)]
    [InlineData(SubscriptionStatuses.Unpaid)]
    public async Task A_running_subscription_must_be_cancelled_first(string status)
    {
        _billing[Doomed].Status = status;

        var result = await DeleteAsync(Doomed, "Acme BV");

        Assert.Equal(OrganizationDeletionOutcome.Refused, result.Outcome);
        Assert.Contains("Stripe", result.Error);
        await AssertNothingDeletedAsync();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("canceled")]
    [InlineData("incomplete_expired")]
    public async Task Everything_of_the_organisation_goes_and_nothing_of_another(string? status)
    {
        _billing[Doomed].Status = status;

        var result = await DeleteAsync(Doomed, "  Acme BV ");

        Assert.Equal(OrganizationDeletionOutcome.Deleted, result.Outcome);

        // Gone: every store's share of org-x.
        Assert.Null(await _platform.FindOrganizationAsync(Doomed));
        Assert.Null(await _platform.FindInvitationAsync($"hash-{Doomed}"));
        Assert.DoesNotContain(_identity.Users, u => u.OrganizationId == Doomed);
        Assert.DoesNotContain(Doomed, _identity.Mfa);
        Assert.DoesNotContain(Doomed, _identity.Inbox);
        Assert.DoesNotContain(Doomed, _identity.Settings);
        Assert.DoesNotContain(Doomed, _records.PlatformSums);
        Assert.DoesNotContain(Doomed, _records.Partitions);
        Assert.False(_records.CustomerLinks.ContainsKey($"cus_{Doomed}"));
        Assert.DoesNotContain(_documents.Keys, k => k.StartsWith($"{Doomed}/", StringComparison.Ordinal));
        Assert.Equal(
            [LoginAttempts.AccountKey($"admin@{Doomed}.test"), LoginAttempts.AccountKey($"user@{Doomed}.test")],
            _attempts.Cleared.Order(StringComparer.Ordinal));

        // Every store was asked for org-x, and only for org-x.
        Assert.All(_log.Calls, call => Assert.EndsWith($":{Doomed}", call));

        // Access goes first, the organisation's own record last.
        Assert.Equal(
            ["users", "mfa", "inbox", "settings", "platform-sums", "customer-link", "documents", "records", "organization"],
            _log.Calls.Select(call => call[..call.IndexOf(':')]));

        // Untouched: the other organisations.
        foreach (var kept in new[] { Other, Caller })
        {
            Assert.NotNull(await _platform.FindOrganizationAsync(kept));
            Assert.NotNull(await _platform.FindInvitationAsync($"hash-{kept}"));
            Assert.Contains(_identity.Users, u => u.OrganizationId == kept);
            Assert.Contains(kept, _identity.Mfa);
            Assert.Contains(kept, _identity.Inbox);
            Assert.Contains(kept, _identity.Settings);
            Assert.Contains(kept, _records.PlatformSums);
            Assert.Contains(kept, _records.Partitions);
            Assert.True(_records.CustomerLinks.ContainsKey($"cus_{kept}"));
            Assert.Contains(_documents.Keys, k => k.StartsWith($"{kept}/", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task Without_a_customer_there_is_no_pointer_to_remove()
    {
        _billing[Doomed].CustomerId = null;

        var result = await DeleteAsync(Doomed, "Acme BV");

        Assert.Equal(OrganizationDeletionOutcome.Deleted, result.Outcome);
        Assert.DoesNotContain(_log.Calls, call => call.StartsWith("customer-link", StringComparison.Ordinal));
        Assert.True(_records.CustomerLinks.ContainsKey($"cus_{Doomed}"));
    }

    [Fact]
    public async Task A_deletion_can_be_repeated_after_it_failed_halfway()
    {
        _documents.FailOnce = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => DeleteAsync(Doomed, "Acme BV"));

        // Nobody can get in, but the organisation is still listed ...
        Assert.DoesNotContain(_identity.Users, u => u.OrganizationId == Doomed);
        Assert.NotNull(await _platform.FindOrganizationAsync(Doomed));

        // ... so the platform administrator runs it again.
        var result = await DeleteAsync(Doomed, "Acme BV");

        Assert.Equal(OrganizationDeletionOutcome.Deleted, result.Outcome);
        Assert.Null(await _platform.FindOrganizationAsync(Doomed));
        Assert.DoesNotContain(Doomed, _records.Partitions);
    }

    private sealed class DeletionLog
    {
        public List<string> Calls { get; } = [];

        public void Add(string store, string organizationId) => Calls.Add($"{store}:{organizationId}");
    }

    /// <summary>The organisation ids that still hold each kind of item; deleting removes only the one asked for.</summary>
    private sealed class FakeIdentityDeletion(DeletionLog log, FakePlatformStore platform) : IOrganizationIdentityDeletion
    {
        public List<User> Users { get; } = [];

        public HashSet<string> Mfa { get; } = [];

        public HashSet<string> Inbox { get; } = [];

        public HashSet<string> Settings { get; } = [];

        public Task<IReadOnlyList<User>> ListUsersAsync(string organizationId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<User>>(Users.Where(u => u.OrganizationId == organizationId).ToList());

        public Task<int> DeleteUsersAsync(string organizationId, CancellationToken cancellationToken = default)
        {
            log.Add("users", organizationId);
            return Task.FromResult(Users.RemoveAll(u => u.OrganizationId == organizationId));
        }

        public Task<int> DeleteMfaAsync(string organizationId, CancellationToken cancellationToken = default)
            => Remove("mfa", Mfa, organizationId);

        public Task<int> DeleteInboxAsync(string organizationId, CancellationToken cancellationToken = default)
            => Remove("inbox", Inbox, organizationId);

        public Task<int> DeleteSettingsAsync(string organizationId, CancellationToken cancellationToken = default)
            => Remove("settings", Settings, organizationId);

        /// <summary>The platform store's item: the same one in DynamoDB.</summary>
        public Task<int> DeleteOrganizationRecordAsync(string organizationId, CancellationToken cancellationToken = default)
        {
            log.Add("organization", organizationId);
            return Task.FromResult(platform.RemoveOrganization(organizationId) ? 1 : 0);
        }

        private Task<int> Remove(string store, HashSet<string> held, string organizationId)
        {
            log.Add(store, organizationId);
            return Task.FromResult(held.Remove(organizationId) ? 1 : 0);
        }
    }

    private sealed class FakeRecordDeletion(DeletionLog log) : IOrganizationRecordDeletion
    {
        public HashSet<string> PlatformSums { get; } = [];

        public HashSet<string> Partitions { get; } = [];

        /// <summary>Customer id to organisation.</summary>
        public Dictionary<string, string> CustomerLinks { get; } = [];

        public Task<int> DeletePlatformSumsAsync(string organizationId, CancellationToken cancellationToken = default)
        {
            log.Add("platform-sums", organizationId);
            return Task.FromResult(PlatformSums.Remove(organizationId) ? 1 : 0);
        }

        public Task<int> DeleteCustomerLinkAsync(string organizationId, string customerId, CancellationToken cancellationToken = default)
        {
            log.Add("customer-link", organizationId);
            var ours = CustomerLinks.TryGetValue(customerId, out var owner) && owner == organizationId;
            return Task.FromResult(ours && CustomerLinks.Remove(customerId) ? 1 : 0);
        }

        public Task<int> DeleteAllForOrganizationAsync(string organizationId, CancellationToken cancellationToken = default)
        {
            log.Add("records", organizationId);
            return Task.FromResult(Partitions.Remove(organizationId) ? 1 : 0);
        }
    }

    private sealed class FakeDocumentDeletion(DeletionLog log) : IOrganizationDocumentDeletion
    {
        public List<string> Keys { get; } = [];

        /// <summary>Fails the first call, as S3 might halfway through.</summary>
        public bool FailOnce { get; set; }

        public Task<int> DeleteAllForOrganizationAsync(string organizationId, CancellationToken cancellationToken = default)
        {
            log.Add("documents", organizationId);
            if (FailOnce)
            {
                FailOnce = false;
                throw new InvalidOperationException("S3 failed.");
            }

            return Task.FromResult(Keys.RemoveAll(k => k.StartsWith($"{organizationId}/", StringComparison.Ordinal)));
        }
    }

    private sealed class RecordingAttempts : ILoginAttemptStore
    {
        public List<string> Cleared { get; } = [];

        public Task<LoginAttempts?> GetAsync(string clientKey, CancellationToken cancellationToken = default)
            => Task.FromResult<LoginAttempts?>(null);

        public Task SaveAsync(LoginAttempts attempts, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ClearAsync(string clientKey, CancellationToken cancellationToken = default)
        {
            Cleared.Add(clientKey);
            return Task.CompletedTask;
        }
    }
}
