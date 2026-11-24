namespace InvoicePlatform.Application.Organizations;

/// <summary>What an organisation has told xChange about itself.</summary>
public sealed record OrganizationSettings
{
    public required string OrganizationId { get; init; }

    /// <summary>The ruleset the format menu opens on. Null means the system default.</summary>
    public string? DefaultRulesetId { get; init; }

    /// <summary>
    /// Where the organisation's PEPPOL id lived before the company profile.
    /// Still read until the profile is first saved
    /// (<see cref="OrganizationProfileService"/>), which clears it. Nothing
    /// writes a new value here any more.
    /// </summary>
    public string? EndpointId { get; init; }

    public string? EndpointScheme { get; init; }
}

/// <summary>Persistence for organisation settings, keyed by organisation.</summary>
public interface IOrganizationSettingsStore
{
    /// <returns>The stored settings, or null when none were ever saved.</returns>
    Task<OrganizationSettings?> GetAsync(string organizationId, CancellationToken cancellationToken = default);

    Task SaveAsync(OrganizationSettings settings, CancellationToken cancellationToken = default);
}
