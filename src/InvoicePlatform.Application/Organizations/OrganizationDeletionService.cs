using InvoicePlatform.Application.Billing;
using InvoicePlatform.Application.Identity;
using InvoicePlatform.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace InvoicePlatform.Application.Organizations;

/// <summary>
/// Deletes an organisation and everything it has, for good. Only a platform
/// administrator may (the API's concern), never their own organisation or one
/// with a platform administrator in it, and never while a subscription still
/// runs at the payment provider: that is cancelled there first.
///
/// The order is chosen so that the account is never half usable: first
/// nobody can get in any more (the open invitation, then the accounts, which
/// ends every session; then the inbox address, so no mail is accepted), then
/// the data goes, and the organisation's own record goes last - so a deletion
/// that fails halfway still shows the organisation and can be run again.
///
/// Deliberately left: the customer at the payment provider (its billing history
/// may be needed for the accounts; only xChange's pointer to it goes), received
/// mail (not kept per organisation, and it expires after 30 days by itself),
/// session and MFA-challenge items (unusable once the account is gone, and
/// removed by their TTL), and the platform-wide totals (aggregates).
/// </summary>
public sealed class OrganizationDeletionService(
    IPlatformStore platform,
    IOrganizationIdentityDeletion identity,
    ILoginAttemptStore attempts,
    IBillingStore billing,
    IOrganizationRecordDeletion records,
    IOrganizationDocumentDeletion documents,
    ILogger<OrganizationDeletionService> logger)
{
    /// <param name="deletedByUserId">The platform administrator asking; recorded in the log.</param>
    /// <param name="deletedByOrganizationId">Their own organisation, which they cannot delete.</param>
    /// <param name="confirmName">The organisation's name as typed to confirm; must match it exactly.</param>
    public async Task<OrganizationDeletionResult> DeleteAsync(
        string deletedByUserId,
        string deletedByOrganizationId,
        string organizationId,
        string? confirmName,
        CancellationToken cancellationToken = default)
    {
        var organization = await platform.FindOrganizationAsync(organizationId, cancellationToken).ConfigureAwait(false);
        if (organization is null)
        {
            return OrganizationDeletionResult.NotFound;
        }

        if (!string.Equals(confirmName?.Trim(), organization.Name.Trim(), StringComparison.Ordinal))
        {
            return OrganizationDeletionResult.Fail(OrganizationDeletionOutcome.Invalid, OrganizationText.Current.ConfirmNameMismatch);
        }

        if (string.Equals(organizationId, deletedByOrganizationId, StringComparison.Ordinal))
        {
            return OrganizationDeletionResult.Fail(OrganizationDeletionOutcome.Refused, OrganizationText.Current.CannotDeleteOwnOrganization);
        }

        var users = await identity.ListUsersAsync(organizationId, cancellationToken).ConfigureAwait(false);
        if (users.Any(user => user.IsPlatformAdmin))
        {
            return OrganizationDeletionResult.Fail(OrganizationDeletionOutcome.Refused, OrganizationText.Current.CannotDeletePlatformAdminOrganization);
        }

        var account = await billing.GetAsync(organizationId, cancellationToken).ConfigureAwait(false);
        if (SubscriptionStatuses.IsOpen(account.Status))
        {
            return OrganizationDeletionResult.Fail(OrganizationDeletionOutcome.Refused, OrganizationText.Current.CancelSubscriptionFirst);
        }

        // 1. Nobody gets in any more.
        var invitations = organization.PendingInvitationHash is { } invitation
            && await platform.TryDeleteInvitationAsync(invitation, cancellationToken).ConfigureAwait(false) ? 1 : 0;
        var accounts = await identity.DeleteUsersAsync(organizationId, cancellationToken).ConfigureAwait(false);
        foreach (var user in users)
        {
            await attempts.ClearAsync(LoginAttempts.AccountKey(user.Email), cancellationToken).ConfigureAwait(false);
        }

        var mfa = await identity.DeleteMfaAsync(organizationId, cancellationToken).ConfigureAwait(false);
        var inbox = await identity.DeleteInboxAsync(organizationId, cancellationToken).ConfigureAwait(false);

        // 2. The data. The customer pointer before the partition: the partition holds its id.
        var settings = await identity.DeleteSettingsAsync(organizationId, cancellationToken).ConfigureAwait(false);
        var platformSums = await records.DeletePlatformSumsAsync(organizationId, cancellationToken).ConfigureAwait(false);
        var customerLinks = account.CustomerId is { } customerId
            ? await records.DeleteCustomerLinkAsync(organizationId, customerId, cancellationToken).ConfigureAwait(false)
            : 0;
        var documentCount = await documents.DeleteAllForOrganizationAsync(organizationId, cancellationToken).ConfigureAwait(false);
        var recordCount = await records.DeleteAllForOrganizationAsync(organizationId, cancellationToken).ConfigureAwait(false);

        // 3. The organisation itself, last.
        var organizations = await identity.DeleteOrganizationRecordAsync(organizationId, cancellationToken).ConfigureAwait(false);

        // Who deleted which organisation, and how much went - no personal data beyond the organisation's name.
        logger.LogInformation(
            "Organisation {OrganizationId} ({OrganizationName}) deleted by platform administrator {DeletedByUserId}: "
            + "{Accounts} accounts, {Invitations} invitations, {Mfa} MFA states, {Inbox} inbox items, {Settings} settings, "
            + "{PlatformSums} platform sums, {CustomerLinks} customer links, {Documents} documents, {Records} invoice-table records, "
            + "{OrganizationRecords} organisation records",
            organizationId,
            organization.Name,
            deletedByUserId,
            accounts,
            invitations,
            mfa,
            inbox,
            settings,
            platformSums,
            customerLinks,
            documentCount,
            recordCount,
            organizations);

        return new OrganizationDeletionResult(OrganizationDeletionOutcome.Deleted);
    }
}

public enum OrganizationDeletionOutcome
{
    Deleted,

    /// <summary>No such organisation.</summary>
    NotFound,

    /// <summary>The request itself is wrong: the confirmation does not match.</summary>
    Invalid,

    /// <summary>The organisation may not be deleted (now): see the error.</summary>
    Refused,
}

public sealed record OrganizationDeletionResult(OrganizationDeletionOutcome Outcome, string? Error = null)
{
    public static readonly OrganizationDeletionResult NotFound = new(OrganizationDeletionOutcome.NotFound);

    public static OrganizationDeletionResult Fail(OrganizationDeletionOutcome outcome, string error) => new(outcome, error);
}
