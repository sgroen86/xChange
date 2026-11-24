using InvoicePlatform.Domain.Organizations;

namespace InvoicePlatform.Application.Organizations;

/// <summary>Persistence for the company profile, keyed by organisation.</summary>
public interface IOrganizationProfileStore
{
    /// <returns>The stored profile, or null when it was never saved.</returns>
    Task<OrganizationProfile?> GetAsync(string organizationId, CancellationToken cancellationToken = default);

    Task SaveAsync(OrganizationProfile profile, CancellationToken cancellationToken = default);
}
