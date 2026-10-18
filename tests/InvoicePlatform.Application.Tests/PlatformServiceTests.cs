using InvoicePlatform.Application.Identity;
using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Domain.Identity;

namespace InvoicePlatform.Application.Tests;

public class PlatformServiceTests
{
    private readonly FakePlatformStore _store = new();
    private readonly KnownUsers _users = new();
    private readonly TestClock _clock = new(DateTimeOffset.Parse("2026-10-18T09:00:00Z"));
    private readonly PlatformService _platform;

    public PlatformServiceTests() => _platform = new PlatformService(_store, _users, _clock);

    [Fact]
    public async Task Creating_an_organisation_stores_it_with_its_country_and_an_open_invitation()
    {
        var result = await _platform.CreateOrganizationAsync("admin", " Klant Pte Ltd ", "sg", "Jan", "jan@klant.sg");

        Assert.Equal(PlatformOutcome.Succeeded, result.Outcome);
        Assert.NotNull(result.InvitationCode);
        var stored = await _store.FindOrganizationAsync(result.Organization!.OrganizationId);
        Assert.Equal(("Klant Pte Ltd", "SG", "jan@klant.sg"), (stored!.Name, stored.CountryCode, stored.PendingInvitationEmail));
        Assert.Equal(_clock.UtcNow + Invitation.Lifetime, stored.PendingInvitationExpiresAt);
    }

    [Theory]
    [InlineData("", "SG", "Jan", "jan@klant.sg")]
    [InlineData("Klant", "UK", "Jan", "jan@klant.sg")]
    [InlineData("Klant", "SG", "", "jan@klant.sg")]
    [InlineData("Klant", "SG", "Jan", "geen-adres")]
    public async Task Invalid_input_is_refused(string name, string country, string userName, string email)
    {
        var result = await _platform.CreateOrganizationAsync("admin", name, country, userName, email);

        Assert.Equal(PlatformOutcome.Invalid, result.Outcome);
        Assert.Empty(await _store.ListOrganizationsAsync());
    }

    [Fact]
    public async Task An_existing_account_cannot_be_invited_again()
    {
        _users.Emails.Add("jan@klant.sg");

        var result = await _platform.CreateOrganizationAsync("admin", "Klant", "SG", "Jan", "Jan@Klant.SG");

        Assert.Equal(PlatformOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public async Task The_list_shows_user_counts_and_whether_an_invitation_is_open()
    {
        var created = await _platform.CreateOrganizationAsync("admin", "Klant", "GB", "Jan", "jan@klant.co.uk");
        _store.UserCount = id => id == created.Organization!.OrganizationId ? 2 : 0;

        var summary = Assert.Single(await _platform.ListAsync());
        Assert.Equal((2, true), (summary.UserCount, summary.InvitationPending));

        _clock.UtcNow += Invitation.Lifetime;
        Assert.False(Assert.Single(await _platform.ListAsync()).InvitationPending);
    }

    [Fact]
    public async Task Name_and_country_can_be_changed()
    {
        var created = await _platform.CreateOrganizationAsync("admin", "Klant", "NL", "Jan", "jan@klant.nl");

        var result = await _platform.UpdateOrganizationAsync(created.Organization!.OrganizationId, "Klant BV", "BE");

        Assert.Equal(("Klant BV", "BE"), (result.Organization!.Name, result.Organization.CountryCode));
    }

    [Fact]
    public async Task An_unknown_organisation_is_not_found()
    {
        Assert.Equal(PlatformOutcome.NotFound, (await _platform.UpdateOrganizationAsync("nope", "X", "NL")).Outcome);
        Assert.Equal(PlatformOutcome.NotFound, (await _platform.ReissueInvitationAsync("nope", "X", "x@y.nl")).Outcome);
    }

    private sealed class KnownUsers : IUserStore
    {
        public HashSet<string> Emails { get; } = [];

        public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
            => Task.FromResult(Emails.Contains(email)
                ? new User { Id = "u", OrganizationId = "o", Email = email, PasswordHash = "-", Name = "-" }
                : null);

        public Task<User?> FindByIdAsync(string organizationId, string userId, CancellationToken cancellationToken = default)
            => Task.FromResult<User?>(null);

        public Task<bool> AnyUsersExistAsync(CancellationToken cancellationToken = default) => Task.FromResult(Emails.Count > 0);

        public Task AddAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateLastLoginAsync(string organizationId, string userId, DateTimeOffset at, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
