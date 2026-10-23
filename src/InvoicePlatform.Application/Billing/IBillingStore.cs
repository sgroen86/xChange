namespace InvoicePlatform.Application.Billing;

/// <summary>
/// An organisation's billing state, tenant-partitioned, and the two lookups
/// webhooks need: customer to organisation, and which events were applied.
///
/// Every write a webhook causes takes an idempotency key and is applied
/// together with it, atomically: the provider retries, and a retry must not
/// count twice.
/// </summary>
public interface IBillingStore
{
    Task<BillingAccount> GetAsync(string organizationId, CancellationToken cancellationToken = default);

    /// <summary>The organisation a provider customer belongs to, as xChange linked it.</summary>
    Task<string?> FindOrganizationByCustomerAsync(string customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Links the organisation and the customer, both ways. An organisation keeps
    /// its first customer, and a customer never moves to another organisation.
    /// </summary>
    /// <returns>The customer id the organisation has now: this one, or the one linked before.</returns>
    /// <exception cref="InvalidOperationException">The customer already belongs to another organisation.</exception>
    Task<string> LinkCustomerAsync(string organizationId, string customerId, CancellationToken cancellationToken = default);

    /// <summary>Adds purchased credits, and lifts the trial.</summary>
    Task<BillingWrite> AddCreditsAsync(string organizationId, int credits, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a subscription's state. Older than the last applied subscription
    /// event: <see cref="BillingWrite.Stale"/>. An ending state only applies to
    /// the subscription the organisation has, never to an older one.
    /// </summary>
    Task<BillingWrite> ApplySubscriptionAsync(
        string organizationId,
        SubscriptionState state,
        DateTimeOffset eventCreated,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>A billing period started: the period's conversions start again from zero, once per period start.</summary>
    Task<BillingWrite> StartPeriodAsync(
        string organizationId,
        DateTimeOffset periodStart,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>Collecting failed: the status becomes past_due, conversions stay.</summary>
    Task<BillingWrite> MarkPastDueAsync(
        string organizationId,
        string subscriptionId,
        DateTimeOffset eventCreated,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}

public enum BillingWrite
{
    Applied,

    /// <summary>This key was applied before.</summary>
    Duplicate,

    /// <summary>Superseded by something applied later; nothing changed.</summary>
    Stale,
}

/// <param name="PlanId">Null when the subscription gives no conversions (ended, unpaid, not started).</param>
/// <param name="MonthlyConversions">Null removes the period's conversions.</param>
public sealed record SubscriptionState(
    string SubscriptionId,
    string? PlanId,
    string Status,
    int? MonthlyConversions,
    DateTimeOffset? PeriodStart,
    DateTimeOffset? PeriodEnd,
    bool CancelAtPeriodEnd);

/// <param name="PeriodAllowance">The subscription's conversions per period; null without a plan.</param>
/// <param name="Credits">Purchased credits left.</param>
public sealed record BillingAccount(
    string? CustomerId,
    string? SubscriptionId,
    string? PlanId,
    string? Status,
    DateTimeOffset? PeriodStart,
    DateTimeOffset? PeriodEnd,
    bool CancelAtPeriodEnd,
    int? PeriodAllowance,
    int PeriodUsed,
    int Credits)
{
    public static readonly BillingAccount None = new(null, null, null, null, null, null, false, null, 0, 0);
}

/// <summary>The provider statuses in which a subscription gives its conversions.</summary>
public static class SubscriptionStatuses
{
    public const string Active = "active";
    public const string Trialing = "trialing";

    /// <summary>A payment failed; the provider retries. xChange keeps serving until it cancels.</summary>
    public const string PastDue = "past_due";

    public static bool GivesConversions(string? status) => status is Active or Trialing or PastDue;
}
