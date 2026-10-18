using InvoicePlatform.Domain.EInvoicing;
using InvoicePlatform.Domain.Organizations;

namespace InvoicePlatform.Application.Organizations;

/// <summary>
/// Reading and changing an organisation's settings. Who may change them is the
/// API's concern (Admin only); this class makes sure what is stored is valid,
/// and that it is stored under the caller's organisation and no other.
/// </summary>
public sealed class OrganizationSettingsService(
    IOrganizationSettingsStore store,
    InvoiceRulesetRegistry rulesets,
    IPlatformStore platform)
{
    /// <summary>The caller's own organisation record: name and country. Null for an organisation created before records existed.</summary>
    public Task<Organization?> GetOrganizationAsync(string organizationId, CancellationToken cancellationToken = default)
        => platform.FindOrganizationAsync(organizationId, cancellationToken);

    /// <summary>
    /// The format the export menu opens on: the organisation's own choice, else
    /// its country's default, else the system default.
    /// </summary>
    public IInvoiceRuleset EffectiveDefault(OrganizationSettings settings, Organization? organization)
        => rulesets.Resolve(null, settings.DefaultRulesetId, Countries.Find(organization?.CountryCode)?.DefaultRulesetId)!;

    public IReadOnlyList<IInvoiceRuleset> Rulesets => rulesets.All;

    /// <summary>The stored settings, or empty ones when nothing was saved yet.</summary>
    public async Task<OrganizationSettings> GetAsync(
        string organizationId,
        CancellationToken cancellationToken = default)
        => await store.GetAsync(organizationId, cancellationToken).ConfigureAwait(false)
            ?? new OrganizationSettings { OrganizationId = organizationId };

    public async Task<SettingsUpdateResult> UpdateAsync(
        string organizationId,
        string? defaultRulesetId,
        string? endpointId,
        string? endpointScheme,
        CancellationToken cancellationToken = default)
    {
        var ruleset = Clean(defaultRulesetId);
        var endpoint = Clean(endpointId);
        var scheme = Clean(endpointScheme);

        if (ruleset is not null && rulesets.Find(ruleset) is null)
        {
            return SettingsUpdateResult.Fail("Onbekende regelset.");
        }

        if (endpoint is not null && scheme is null)
        {
            return SettingsUpdateResult.Fail("Kies het schema van het elektronisch adres.");
        }

        if (scheme is not null && !CodeLists.IsElectronicAddressScheme(scheme))
        {
            return SettingsUpdateResult.Fail($"'{scheme}' is geen geldig schema voor een elektronisch adres.");
        }

        var settings = new OrganizationSettings
        {
            OrganizationId = organizationId,
            DefaultRulesetId = ruleset,
            EndpointId = endpoint,
            // A scheme without an address means nothing.
            EndpointScheme = endpoint is null ? null : scheme,
        };

        await store.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
        return new SettingsUpdateResult(true, Settings: settings);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record SettingsUpdateResult(bool Succeeded, string? Error = null, OrganizationSettings? Settings = null)
{
    public static SettingsUpdateResult Fail(string error) => new(false, error);
}
