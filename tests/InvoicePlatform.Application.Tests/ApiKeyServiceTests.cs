using InvoicePlatform.Application.Identity;
using InvoicePlatform.Domain.Identity;

namespace InvoicePlatform.Application.Tests;

public class ApiKeyServiceTests
{
    private const string OrgA = "org-a";
    private const string OrgB = "org-b";

    private readonly FakeApiKeyStore _store = new();
    private readonly TestClock _clock = new(DateTimeOffset.Parse("2026-10-18T09:00:00Z"));
    private readonly ApiKeyService _service;

    public ApiKeyServiceTests() => _service = new ApiKeyService(_store, _clock);

    [Fact]
    public async Task A_new_key_is_shown_once_and_only_its_hash_is_stored()
    {
        var created = (await _service.CreateAsync(OrgA, "admin-a", "  Exact koppeling "))!;

        Assert.StartsWith("xck_", created.Secret, StringComparison.Ordinal);
        // 32 bytes, base64url without padding.
        Assert.Equal(4 + 43, created.Secret.Length);
        Assert.DoesNotContain('+', created.Secret);
        Assert.DoesNotContain('/', created.Secret);

        var stored = Assert.Single(_store.All);
        Assert.Equal("Exact koppeling", stored.Name);
        Assert.Equal(ApiKey.HashOf(created.Secret), stored.Hash);
        Assert.Equal(created.Secret[..12], stored.VisiblePrefix);
        Assert.Equal("admin-a", stored.CreatedByUserId);
        Assert.DoesNotContain(created.Secret, stored.Hash, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Two_keys_are_never_the_same()
    {
        var first = (await _service.CreateAsync(OrgA, "admin-a", "Een"))!;
        var second = (await _service.CreateAsync(OrgA, "admin-a", "Twee"))!;

        Assert.NotEqual(first.Secret, second.Secret);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_key_needs_a_name(string name)
    {
        Assert.Null(await _service.CreateAsync(OrgA, "admin-a", name));
        Assert.Null(await _service.CreateAsync(OrgA, "admin-a", new string('x', ApiKeyService.MaxNameLength + 1)));
        Assert.Empty(_store.All);
    }

    [Fact]
    public async Task A_key_authenticates_as_its_organisation_and_records_its_use()
    {
        var created = (await _service.CreateAsync(OrgA, "admin-a", "ERP"))!;

        var caller = await _service.AuthenticateAsync(created.Secret);

        Assert.Equal(new ApiKeyCaller(OrgA, created.Key.KeyId), caller);
        Assert.Equal($"apikey:{created.Key.KeyId}", caller!.Actor);
        Assert.Equal(_clock.UtcNow, Assert.Single(await _service.ListAsync(OrgA)).LastUsedAt);
    }

    [Fact]
    public async Task Its_use_is_written_at_most_every_few_minutes()
    {
        var created = (await _service.CreateAsync(OrgA, "admin-a", "ERP"))!;

        await _service.AuthenticateAsync(created.Secret);
        _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
        await _service.AuthenticateAsync(created.Secret);
        Assert.Equal(1, _store.Touches);

        _clock.UtcNow = _clock.UtcNow.AddMinutes(5);
        await _service.AuthenticateAsync(created.Secret);
        Assert.Equal(2, _store.Touches);
    }

    [Theory]
    [InlineData("xck_nonsense")]
    [InlineData("not-a-key")]
    [InlineData("")]
    public async Task Anything_else_does_not_authenticate(string token)
    {
        await _service.CreateAsync(OrgA, "admin-a", "ERP");

        Assert.Null(await _service.AuthenticateAsync(token));
    }

    [Fact]
    public async Task A_revoked_key_stops_working_at_once()
    {
        var created = (await _service.CreateAsync(OrgA, "admin-a", "ERP"))!;

        Assert.True(await _service.RevokeAsync(OrgA, created.Key.KeyId));

        Assert.Null(await _service.AuthenticateAsync(created.Secret));
        Assert.Empty(await _service.ListAsync(OrgA));
    }

    [Fact]
    public async Task Another_organisation_can_neither_see_nor_revoke_a_key()
    {
        var created = (await _service.CreateAsync(OrgA, "admin-a", "ERP"))!;

        Assert.Empty(await _service.ListAsync(OrgB));
        Assert.False(await _service.RevokeAsync(OrgB, created.Key.KeyId));
        Assert.NotNull(await _service.AuthenticateAsync(created.Secret));
    }

    private sealed class FakeApiKeyStore : IApiKeyStore
    {
        private readonly Dictionary<string, ApiKey> _byHash = [];

        public IReadOnlyCollection<ApiKey> All => _byHash.Values;

        public int Touches { get; private set; }

        public Task SaveAsync(ApiKey key, CancellationToken cancellationToken = default)
        {
            _byHash[key.Hash] = key;
            return Task.CompletedTask;
        }

        public Task<ApiKey?> FindByHashAsync(string hash, CancellationToken cancellationToken = default)
            => Task.FromResult(_byHash.GetValueOrDefault(hash));

        public Task<IReadOnlyList<ApiKey>> ListAsync(string organizationId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ApiKey>>(_byHash.Values.Where(k => k.OrganizationId == organizationId).ToList());

        public Task TouchAsync(ApiKey key, DateTimeOffset at, CancellationToken cancellationToken = default)
        {
            Touches++;
            if (_byHash.ContainsKey(key.Hash))
            {
                _byHash[key.Hash] = key with { LastUsedAt = at };
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync(ApiKey key, CancellationToken cancellationToken = default)
        {
            _byHash.Remove(key.Hash);
            return Task.CompletedTask;
        }
    }
}
