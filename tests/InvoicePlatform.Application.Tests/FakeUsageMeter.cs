using InvoicePlatform.Application.Billing;
using InvoicePlatform.Application.Usage;

namespace InvoicePlatform.Application.Tests;

/// <summary>
/// The organisation's allowance item in memory, as DynamoDB keeps it: the
/// usage meter and the billing store share one record per organisation, the
/// meter takes conversions by <see cref="ConversionRules"/>, and every billing
/// write is applied once per idempotency key. No limit unless one is set.
/// </summary>
internal sealed class FakeUsageMeter : IUsageMeter, IBillingStore
{
    private readonly Dictionary<string, Allowance> _orgs = [];
    private readonly HashSet<string> _applied = [];
    private readonly Dictionary<string, string> _customers = [];

    public Allowance this[string organizationId] => Get(organizationId);

    public Task<ConversionBucket?> TryConsumeAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        var a = Get(organizationId);
        var bucket = ConversionRules.Choose(new ConversionBalance(a.PeriodAllowance, a.PeriodUsed, a.Credits, a.Used, a.Limit, a.SubscribedOnce));
        switch (bucket)
        {
            case ConversionBucket.Subscription:
                a.PeriodUsed++;
                break;
            case ConversionBucket.Credits:
                a.Credits--;
                break;
            case ConversionBucket.Base:
                a.Used++;
                break;
        }

        return Task.FromResult(bucket);
    }

    public Task RefundAsync(string organizationId, ConversionBucket bucket, CancellationToken cancellationToken = default)
    {
        var a = Get(organizationId);
        switch (bucket)
        {
            case ConversionBucket.Subscription when a.PeriodUsed > 0:
                a.PeriodUsed--;
                break;
            case ConversionBucket.Credits:
                a.Credits++;
                break;
            case ConversionBucket.Base when a.Used > 0:
                a.Used--;
                break;
        }

        return Task.CompletedTask;
    }

    public Task<UsageSummary> GetAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        var a = Get(organizationId);
        return Task.FromResult(new UsageSummary(a.Used, a.Limit, a.TrialPlan && !a.PaidOnce));
    }

    public Task SetLimitAsync(string organizationId, int? limit, bool trial = false, CancellationToken cancellationToken = default)
    {
        var a = Get(organizationId);
        a.Limit = limit;
        a.TrialPlan = trial && limit is not null;
        return Task.CompletedTask;
    }

    Task<BillingAccount> IBillingStore.GetAsync(string organizationId, CancellationToken cancellationToken)
    {
        var a = Get(organizationId);
        return Task.FromResult(new BillingAccount(
            a.CustomerId, a.SubscriptionId, a.PlanId, a.Status, a.PeriodStart, a.PeriodEnd, a.CancelAtPeriodEnd,
            a.PeriodAllowance, a.PeriodUsed, a.Credits));
    }

    public Task<string?> FindOrganizationByCustomerAsync(string customerId, CancellationToken cancellationToken = default)
        => Task.FromResult(_customers.GetValueOrDefault(customerId));

    public Task<string> LinkCustomerAsync(string organizationId, string customerId, CancellationToken cancellationToken = default)
    {
        if (_customers.TryGetValue(customerId, out var owner) && owner != organizationId)
        {
            throw new InvalidOperationException("The customer belongs to another organisation.");
        }

        var a = Get(organizationId);
        if (a.CustomerId is not null && a.CustomerId != customerId)
        {
            return Task.FromResult(a.CustomerId);
        }

        a.CustomerId = customerId;
        _customers[customerId] = organizationId;
        return Task.FromResult(customerId);
    }

    public Task<BillingWrite> AddCreditsAsync(string organizationId, int credits, string idempotencyKey, CancellationToken cancellationToken = default)
        => Once(idempotencyKey, () =>
        {
            var a = Get(organizationId);
            a.Credits += credits;
            a.PaidOnce = true;
            return true;
        });

    public Task<BillingWrite> ApplySubscriptionAsync(
        string organizationId, SubscriptionState state, DateTimeOffset eventCreated, string idempotencyKey, CancellationToken cancellationToken = default)
        => Once(idempotencyKey, () =>
        {
            var a = Get(organizationId);
            if (a.SubscriptionEventAt is { } last && last > eventCreated)
            {
                return false;
            }

            if (state.MonthlyConversions is { } conversions && state.PlanId is not null)
            {
                a.PlanId = state.PlanId;
                a.PeriodAllowance = conversions;
                a.PeriodStart = state.PeriodStart ?? a.PeriodStart;
                a.PeriodEnd = state.PeriodEnd ?? a.PeriodEnd;
                a.SubscribedOnce = true;
                a.PaidOnce = true;
            }
            else
            {
                if (a.SubscriptionId is not null && a.SubscriptionId != state.SubscriptionId)
                {
                    return false;
                }

                a.PlanId = null;
                a.PeriodAllowance = null;
                a.PeriodStart = null;
                a.PeriodEnd = null;
            }

            a.SubscriptionId = state.SubscriptionId;
            a.Status = state.Status;
            a.CancelAtPeriodEnd = state.CancelAtPeriodEnd;
            a.SubscriptionEventAt = eventCreated;
            return true;
        });

    public Task<BillingWrite> StartPeriodAsync(string organizationId, DateTimeOffset periodStart, string idempotencyKey, CancellationToken cancellationToken = default)
        => Once(idempotencyKey, () =>
        {
            var a = Get(organizationId);
            if (a.PeriodResetFrom is { } from && from >= periodStart)
            {
                return false;
            }

            a.PeriodUsed = 0;
            a.PeriodResetFrom = periodStart;
            return true;
        });

    public Task<BillingWrite> MarkPastDueAsync(
        string organizationId, string subscriptionId, DateTimeOffset eventCreated, string idempotencyKey, CancellationToken cancellationToken = default)
        => Once(idempotencyKey, () =>
        {
            var a = Get(organizationId);
            if (a.SubscriptionId != subscriptionId || (a.SubscriptionEventAt is { } last && last > eventCreated))
            {
                return false;
            }

            a.Status = SubscriptionStatuses.PastDue;
            a.SubscriptionEventAt = eventCreated;
            return true;
        });

    /// <summary>The marker and the change together, as the DynamoDB transaction does.</summary>
    private Task<BillingWrite> Once(string key, Func<bool> apply)
    {
        if (_applied.Contains(key))
        {
            return Task.FromResult(BillingWrite.Duplicate);
        }

        if (!apply())
        {
            return Task.FromResult(BillingWrite.Stale);
        }

        _applied.Add(key);
        return Task.FromResult(BillingWrite.Applied);
    }

    private Allowance Get(string organizationId)
    {
        if (!_orgs.TryGetValue(organizationId, out var allowance))
        {
            _orgs[organizationId] = allowance = new Allowance();
        }

        return allowance;
    }

    internal sealed class Allowance
    {
        public int Used { get; set; }

        public int? Limit { get; set; }

        public bool TrialPlan { get; set; }

        public int? PeriodAllowance { get; set; }

        public int PeriodUsed { get; set; }

        public int Credits { get; set; }

        public bool SubscribedOnce { get; set; }

        public bool PaidOnce { get; set; }

        public string? CustomerId { get; set; }

        public string? SubscriptionId { get; set; }

        public string? PlanId { get; set; }

        public string? Status { get; set; }

        public DateTimeOffset? PeriodStart { get; set; }

        public DateTimeOffset? PeriodEnd { get; set; }

        public DateTimeOffset? PeriodResetFrom { get; set; }

        public bool CancelAtPeriodEnd { get; set; }

        public DateTimeOffset? SubscriptionEventAt { get; set; }
    }
}
