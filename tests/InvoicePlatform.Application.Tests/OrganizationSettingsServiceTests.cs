using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Domain.EInvoicing;

namespace InvoicePlatform.Application.Tests;

public class OrganizationSettingsServiceTests
{
    private readonly FakeSettingsStore _store = new();
    private readonly OrganizationSettingsService _service;

    public OrganizationSettingsServiceTests()
        => _service = new OrganizationSettingsService(_store, new InvoiceRulesetRegistry(), new FakePlatformStore());

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
        var result = await _service.UpdateAsync("org-a", " peppol-bis-billing-3.0 ");

        Assert.True(result.Succeeded);
        Assert.Equal("peppol-bis-billing-3.0", (await _service.GetAsync("org-a")).DefaultRulesetId);
    }

    [Fact]
    public async Task An_unknown_ruleset_is_refused()
    {
        var result = await _service.UpdateAsync("org-a", "xrechnung-9");

        Assert.False(result.Succeeded);
        Assert.Null(await _store.GetAsync("org-a"));
    }

    [Fact]
    public async Task An_empty_value_clears_the_default()
    {
        await _service.UpdateAsync("org-a", "peppol-bis-billing-3.0");

        await _service.UpdateAsync("org-a", "  ");

        Assert.Null((await _service.GetAsync("org-a")).DefaultRulesetId);
    }

    [Fact]
    public async Task Saving_the_settings_keeps_a_peppol_id_that_has_not_moved_yet()
    {
        await _store.SaveAsync(new OrganizationSettings { OrganizationId = "org-a", EndpointId = "12345678", EndpointScheme = "0106" });

        await _service.UpdateAsync("org-a", "peppol-bis-billing-3.0");

        var settings = await _service.GetAsync("org-a");
        Assert.Equal(("12345678", "0106"), (settings.EndpointId, settings.EndpointScheme));
    }

    [Fact]
    public async Task One_organisation_never_sees_anothers_settings()
    {
        await _service.UpdateAsync("org-a", "peppol-bis-billing-3.0");

        Assert.Null((await _service.GetAsync("org-b")).DefaultRulesetId);
    }
}
