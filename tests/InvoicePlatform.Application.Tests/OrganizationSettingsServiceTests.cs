using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Domain.EInvoicing;

namespace InvoicePlatform.Application.Tests;

public class OrganizationSettingsServiceTests
{
    private readonly FakeSettingsStore _store = new();
    private readonly OrganizationSettingsService _service;

    public OrganizationSettingsServiceTests()
        => _service = new OrganizationSettingsService(_store, new InvoiceRulesetRegistry());

    [Fact]
    public async Task Nothing_saved_yet_means_empty_settings_for_that_organisation()
    {
        var settings = await _service.GetAsync("org-a");

        Assert.Equal("org-a", settings.OrganizationId);
        Assert.Null(settings.DefaultRulesetId);
        Assert.Null(settings.EndpointId);
    }

    [Fact]
    public async Task An_update_round_trips_trimmed()
    {
        var result = await _service.UpdateAsync("org-a", "peppol-bis-billing-3.0", " 12345678 ", "0106");

        Assert.True(result.Succeeded);
        var settings = await _service.GetAsync("org-a");
        Assert.Equal(("peppol-bis-billing-3.0", "12345678", "0106"), (settings.DefaultRulesetId, settings.EndpointId, settings.EndpointScheme));
    }

    [Fact]
    public async Task An_unknown_ruleset_is_refused()
    {
        var result = await _service.UpdateAsync("org-a", "xrechnung-9", null, null);

        Assert.False(result.Succeeded);
        Assert.Null(await _store.GetAsync("org-a"));
    }

    [Theory]
    [InlineData("12345678", null)]
    [InlineData("12345678", "0217")]
    public async Task An_endpoint_needs_a_valid_scheme(string endpoint, string? scheme)
    {
        var result = await _service.UpdateAsync("org-a", null, endpoint, scheme);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Empty_values_clear_the_settings()
    {
        await _service.UpdateAsync("org-a", "peppol-bis-billing-3.0", "12345678", "0106");

        await _service.UpdateAsync("org-a", "", "  ", "0106");

        var settings = await _service.GetAsync("org-a");
        Assert.Null(settings.DefaultRulesetId);
        Assert.Null(settings.EndpointId);
        Assert.Null(settings.EndpointScheme);
    }

    [Fact]
    public async Task One_organisation_never_sees_anothers_settings()
    {
        await _service.UpdateAsync("org-a", null, "12345678", "0106");

        Assert.Null((await _service.GetAsync("org-b")).EndpointId);
    }
}
