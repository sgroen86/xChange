using InvoicePlatform.Domain.Identity;

namespace InvoicePlatform.Application.Organizations;

// Deleting an organisation, per store. Kept apart from the stores' everyday
// interfaces on purpose: only OrganizationDeletionService depends on these, so
// no other use case can reach a delete that empties an organisation. Every
// method takes the organisation explicitly and touches nothing of another one.
// They are idempotent - deleting what is already gone counts 0 - so a deletion
// that failed halfway can simply be run again.

/// <summary>The identity table's share of an organisation: its accounts and what hangs off them.</summary>
public interface IOrganizationIdentityDeletion
{
    /// <summary>The organisation's accounts, read before they are deleted (who is a platform administrator, which login counters to clear).</summary>
    Task<IReadOnlyList<User>> ListUsersAsync(string organizationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every account of the organisation, both the item found by id and the
    /// one found by email at login. A session of a deleted account stops
    /// working at once (sessions are re-checked against the account on every
    /// request); the session items themselves expire by TTL.
    /// </summary>
    /// <returns>How many accounts were deleted.</returns>
    Task<int> DeleteUsersAsync(string organizationId, CancellationToken cancellationToken = default);

    /// <summary>The two-step verification state of the organisation's users.</summary>
    Task<int> DeleteMfaAsync(string organizationId, CancellationToken cancellationToken = default);

    /// <summary>The organisation's inbox address, both ways (address to organisation and back), so no more mail is accepted for it.</summary>
    Task<int> DeleteInboxAsync(string organizationId, CancellationToken cancellationToken = default);

    /// <summary>The organisation's settings (default ruleset, electronic address).</summary>
    Task<int> DeleteSettingsAsync(string organizationId, CancellationToken cancellationToken = default);

    /// <summary>The organisation itself, as the platform lists it. Deleted last, so a deletion that failed halfway still shows it and can be repeated.</summary>
    Task<int> DeleteOrganizationRecordAsync(string organizationId, CancellationToken cancellationToken = default);
}

/// <summary>The invoice table's share of an organisation.</summary>
public interface IOrganizationRecordDeletion
{
    /// <summary>
    /// The platform-wide sums kept per organisation (cost and reading quality
    /// per month). The platform totals stay: they are aggregates, not the
    /// organisation's data.
    /// </summary>
    Task<int> DeletePlatformSumsAsync(string organizationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The pointer from the payment provider's customer to the organisation,
    /// only when it points at this organisation. The customer at the provider
    /// is left alone: its billing history may be needed for the accounts.
    /// </summary>
    Task<int> DeleteCustomerLinkAsync(string organizationId, string customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Everything in the organisation's own partition: invoices, imports,
    /// relations, extractions, learning, billing and conversions, cost and
    /// quality records and sums.
    /// </summary>
    Task<int> DeleteAllForOrganizationAsync(string organizationId, CancellationToken cancellationToken = default);
}

/// <summary>The organisation's documents: every source PDF stored under its prefix.</summary>
public interface IOrganizationDocumentDeletion
{
    Task<int> DeleteAllForOrganizationAsync(string organizationId, CancellationToken cancellationToken = default);
}
