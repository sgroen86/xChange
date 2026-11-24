using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Domain.Organizations;

namespace InvoicePlatform.Application.Tests;

public class OrganizationProfileServiceTests
{
    private readonly FakeProfileStore _profiles = new();
    private readonly FakeSettingsStore _settings = new();
    private readonly FakePlatformStore _platform = new();
    private readonly OrganizationProfileService _service;

    public OrganizationProfileServiceTests()
    {
        _service = new OrganizationProfileService(_profiles, _settings, _platform);
        _platform.SaveOrganizationAsync(new Organization
        {
            OrganizationId = "org-a",
            Name = "Groen",
            CountryCode = "NL",
            CreatedAt = DateTimeOffset.UnixEpoch,
        }).GetAwaiter().GetResult();
    }

    private static OrganizationProfile Profile(string vat = "NL123456789B01") => new()
    {
        OrganizationId = "not-the-callers",
        General = new CompanyDetails { LegalName = "Groen B.V.", VatIdentifier = vat },
        Peppol = new PeppolDetails { EndpointId = "12345678", EndpointScheme = "0106" },
    };

    private Task OldPeppolIdInSettings(string id = "87654321")
        => _settings.SaveAsync(new OrganizationSettings
        {
            OrganizationId = "org-a",
            DefaultRulesetId = "peppol-bis-billing-3.0",
            EndpointId = id,
            EndpointScheme = "0106",
        });

    [Fact]
    public async Task Nothing_saved_yet_means_an_empty_profile()
    {
        var profile = await _service.GetAsync("org-a");

        Assert.Equal("org-a", profile.OrganizationId);
        Assert.Null(profile.General.LegalName);
        Assert.Null(profile.Peppol.EndpointId);
    }

    [Fact]
    public async Task Before_the_first_save_the_peppol_id_comes_from_the_settings()
    {
        await OldPeppolIdInSettings();

        var profile = await _service.GetAsync("org-a");

        Assert.Equal(("87654321", "0106"), (profile.Peppol.EndpointId, profile.Peppol.EndpointScheme));
    }

    [Fact]
    public async Task Saving_moves_the_peppol_id_out_of_the_settings()
    {
        await OldPeppolIdInSettings();

        var result = await _service.UpdateAsync("org-a", Profile());

        Assert.True(result.Succeeded);
        var settings = await _settings.GetAsync("org-a");
        Assert.Null(settings!.EndpointId);
        Assert.Null(settings.EndpointScheme);
        Assert.Equal("peppol-bis-billing-3.0", settings.DefaultRulesetId);
        Assert.Equal("12345678", (await _service.GetAsync("org-a")).Peppol.EndpointId);
    }

    [Fact]
    public async Task Clearing_the_peppol_id_does_not_bring_back_the_old_one()
    {
        await OldPeppolIdInSettings();

        await _service.UpdateAsync("org-a", Profile() with { Peppol = new PeppolDetails() });

        Assert.Null((await _service.GetAsync("org-a")).Peppol.EndpointId);
    }

    [Fact]
    public async Task An_invalid_profile_is_refused_and_nothing_is_stored()
    {
        var result = await _service.UpdateAsync("org-a", Profile(vat: "BE0123456749"));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Field == "general.vatIdentifier");
        Assert.Null(await _profiles.GetAsync("org-a"));
    }

    [Fact]
    public async Task The_profile_is_stored_under_the_callers_organisation_only()
    {
        await _service.UpdateAsync("org-a", Profile());

        Assert.Equal("Groen B.V.", (await _profiles.GetAsync("org-a"))!.General.LegalName);
        Assert.Null(await _profiles.GetAsync("not-the-callers"));
        Assert.Null((await _service.GetAsync("org-b")).General.LegalName);
    }

    [Fact]
    public async Task For_invoices_the_country_comes_with_a_saved_profile_only()
    {
        var before = await _service.ForInvoicesAsync("org-a");
        Assert.Null(before.CountryCode);
        Assert.False(before.Saved);

        await _service.UpdateAsync("org-a", Profile());

        var seller = await _service.ForInvoicesAsync("org-a");
        Assert.Equal("NL", seller.CountryCode);
        Assert.True(seller.Saved);
        Assert.Equal("Groen B.V.", seller.Profile.General.LegalName);
    }

    [Fact]
    public async Task Saving_clears_a_scheme_left_in_the_settings_without_an_id()
    {
        await _settings.SaveAsync(new OrganizationSettings { OrganizationId = "org-a", EndpointScheme = "0106" });

        await _service.UpdateAsync("org-a", Profile());

        Assert.Null((await _settings.GetAsync("org-a"))!.EndpointScheme);
    }

    [Fact]
    public async Task For_invoices_without_a_saved_profile_the_peppol_id_comes_from_the_settings()
    {
        await OldPeppolIdInSettings();

        var seller = await _service.ForInvoicesAsync("org-a");

        Assert.Equal("87654321", seller.Profile.Peppol.EndpointId);
        Assert.Null(seller.CountryCode);
    }
}
