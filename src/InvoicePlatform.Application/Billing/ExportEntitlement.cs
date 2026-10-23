using InvoicePlatform.Application.Usage;

namespace InvoicePlatform.Application.Billing;

/// <summary>
/// Whether an organisation may take an e-invoice away - download it now, send
/// it later. Converting and checking are free within the conversions it has;
/// the file is for paying customers. The one place this is decided: the
/// export refuses on it, and the screen shows it.
///
/// An organisation may export when
///   - its subscription runs (<see cref="SubscriptionStatuses.GivesConversions"/>:
///     active, trialing or past_due), or
///   - it has purchased credits left, or
///   - it has no limit and never subscribed (the platform's own organisations
///     and those its administrator created).
///
/// Having paid once is not enough: when the subscription ends and the credits
/// are used up, downloading stops.
/// </summary>
public static class ExportEntitlement
{
    public static bool Allows(BillingAccount account, UsageSummary usage)
        => SubscriptionStatuses.GivesConversions(account.Status)
            || account.Credits > 0
            || usage.Unlimited;

    /// <summary>Reads the organisation's billing state and usage, and decides.</summary>
    public static async Task<bool> AllowsAsync(
        IBillingStore billing,
        IUsageMeter usage,
        string organizationId,
        CancellationToken cancellationToken = default)
    {
        var account = await billing.GetAsync(organizationId, cancellationToken).ConfigureAwait(false);
        var summary = await usage.GetAsync(organizationId, cancellationToken).ConfigureAwait(false);
        return Allows(account, summary);
    }
}
