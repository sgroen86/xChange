namespace InvoicePlatform.Application.Organizations;

/// <summary>What an organisation has told xChange about itself.</summary>
public sealed record OrganizationSettings
{
    public required string OrganizationId { get; init; }

    /// <summary>The ruleset the format menu opens on. Null means the system default.</summary>
    public string? DefaultRulesetId { get; init; }

    /// <summary>
    /// The organisation's own electronic address (BT-34, it is the seller) and
    /// its EAS scheme. Filled into every invoice whose seller address is empty.
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
