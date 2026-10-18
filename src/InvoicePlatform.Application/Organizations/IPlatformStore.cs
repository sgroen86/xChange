using InvoicePlatform.Domain.Identity;
using InvoicePlatform.Domain.Organizations;

namespace InvoicePlatform.Application.Organizations;

/// <summary>
/// Platform-level persistence: the organisations themselves and the
/// invitations into them. Nothing here holds an organisation's invoices,
/// relations or settings.
/// </summary>
public interface IPlatformStore
{
    Task<Organization?> FindOrganizationAsync(string organizationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Organization>> ListOrganizationsAsync(CancellationToken cancellationToken = default);

    Task SaveOrganizationAsync(Organization organization, CancellationToken cancellationToken = default);

    Task<int> CountUsersAsync(string organizationId, CancellationToken cancellationToken = default);

    Task SaveInvitationAsync(Invitation invitation, CancellationToken cancellationToken = default);

    Task<Invitation?> FindInvitationAsync(string codeHash, CancellationToken cancellationToken = default);

    /// <returns>
    /// False when the invitation was already gone: used by someone else, or
    /// never there. The delete is conditional, so of two simultaneous uses only
    /// one gets true.
    /// </returns>
    Task<bool> TryDeleteInvitationAsync(string codeHash, CancellationToken cancellationToken = default);

    Task<PlatformSettings> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task SaveSettingsAsync(PlatformSettings settings, CancellationToken cancellationToken = default);
}
