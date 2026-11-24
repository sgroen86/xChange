using InvoicePlatform.Domain.Organizations;

namespace InvoicePlatform.Application.Organizations;

/// <summary>
/// Reading and changing an organisation's company profile. Who may change it
/// is the API's concern (Admin only); this class cleans and checks what is
/// stored, and stores it under the caller's organisation and no other.
/// </summary>
public sealed class OrganizationProfileService(
    IOrganizationProfileStore profiles,
    IOrganizationSettingsStore settings,
    IPlatformStore platform)
{
    /// <summary>The caller's own organisation record: the name it was created with, and its country.</summary>
    public Task<Organization?> GetOrganizationAsync(string organizationId, CancellationToken cancellationToken = default)
        => platform.FindOrganizationAsync(organizationId, cancellationToken);

    /// <summary>
    /// The stored profile. Before it is first saved, an empty one carrying the
    /// PEPPOL id the organisation entered under Settings, where it used to live.
    /// </summary>
    public async Task<OrganizationProfile> GetAsync(string organizationId, CancellationToken cancellationToken = default)
        => await profiles.GetAsync(organizationId, cancellationToken).ConfigureAwait(false)
            ?? await FromSettingsAsync(organizationId, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// What the seller on an invoice is filled from. The country only once the
    /// profile is saved: an organisation that never filled it in keeps today's
    /// behaviour, where only empty fields are filled, and only the PEPPOL id is
    /// available. <see cref="SellerProfile.Saved"/> tells the two apart.
    /// </summary>
    public async Task<SellerProfile> ForInvoicesAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        var stored = await profiles.GetAsync(organizationId, cancellationToken).ConfigureAwait(false);
        if (stored is null)
        {
            return new SellerProfile(await FromSettingsAsync(organizationId, cancellationToken).ConfigureAwait(false), CountryCode: null, Saved: false);
        }

        var organization = await platform.FindOrganizationAsync(organizationId, cancellationToken).ConfigureAwait(false);
        return new SellerProfile(stored, organization?.CountryCode, Saved: true);
    }

    /// <param name="update">What the screen sent. Its OrganizationId is ignored: the caller's is used.</param>
    public async Task<ProfileUpdateResult> UpdateAsync(
        string organizationId,
        OrganizationProfile update,
        CancellationToken cancellationToken = default)
    {
        var profile = OrganizationProfileRules.Normalize(update with { OrganizationId = organizationId });
        var organization = await platform.FindOrganizationAsync(organizationId, cancellationToken).ConfigureAwait(false);

        var errors = OrganizationProfileRules.Check(profile, organization?.CountryCode);
        if (errors.Count > 0)
        {
            return new ProfileUpdateResult(false, errors);
        }

        await profiles.SaveAsync(profile, cancellationToken).ConfigureAwait(false);

        // The PEPPOL id has moved here. The copy under Settings goes, so a
        // cleared id cannot come back from it.
        var legacy = await settings.GetAsync(organizationId, cancellationToken).ConfigureAwait(false);
        if (legacy is { EndpointId: not null } or { EndpointScheme: not null })
        {
            await settings.SaveAsync(legacy with { EndpointId = null, EndpointScheme = null }, cancellationToken).ConfigureAwait(false);
        }

        return new ProfileUpdateResult(true, [], profile);
    }

    private async Task<OrganizationProfile> FromSettingsAsync(string organizationId, CancellationToken cancellationToken)
    {
        var legacy = await settings.GetAsync(organizationId, cancellationToken).ConfigureAwait(false);
        return new OrganizationProfile
        {
            OrganizationId = organizationId,
            Peppol = legacy?.EndpointId is { } endpoint
                ? new PeppolDetails { EndpointId = endpoint, EndpointScheme = legacy.EndpointScheme }
                : new PeppolDetails(),
        };
    }
}

/// <param name="CountryCode">The organisation's country, or null while no profile is saved.</param>
public sealed record SellerProfile(OrganizationProfile Profile, string? CountryCode, bool Saved);

public sealed record ProfileUpdateResult(bool Succeeded, IReadOnlyList<ProfileFieldError> Errors, OrganizationProfile? Profile = null);
