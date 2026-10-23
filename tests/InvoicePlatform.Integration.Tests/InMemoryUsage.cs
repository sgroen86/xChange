using InvoicePlatform.Application.Billing;
using InvoicePlatform.Application.Usage;

namespace InvoicePlatform.Integration.Tests;

/// <summary>
/// The organisation's allowance item in memory: conversions and billing state
/// in one record, as the DynamoDB stores share one item. No limit unless one
/// is set; each billing write applied once per idempotency key.
/// </summary>
public sealed class InMemoryUsage : IUsageMeter, IBillingStore
{
    private readonly Dictionary<string, Allowance> _orgs = [];
    private readonly HashSet<string> _applied = [];
    private readonly Dictionary<string, string> _customers = [];

    public Allowance For(string organizationId)
    {
        lock (_orgs)
        {
            return Get(organizationId);
        }
    }

    public Task<ConversionBucket?> TryConsumeAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        lock (_orgs)
        {
            var a = Get(organizationId);
            var bucket = ConversionRules.Choose(new ConversionBalance(a.PeriodAllowance, a.PeriodUsed, a.Credits, a.Used, a.Limit, a.SubscribedOnce));
            if (bucket == ConversionBucket.Subscription) a.PeriodUsed++;
            if (bucket == ConversionBucket.Credits) a.Credits--;
            if (bucket == ConversionBucket.Base) a.Used++;
            return Task.FromResult(bucket);
        }
    }

    public Task RefundAsync(string organizationId, ConversionBucket bucket, CancellationToken cancellationToken = default)
    {
        lock (_orgs)
        {
            var a = Get(organizationId);
            if (bucket == ConversionBucket.Subscription) a.PeriodUsed = Math.Max(0, a.PeriodUsed - 1);
            if (bucket == ConversionBucket.Credits) a.Credits++;
            if (bucket == ConversionBucket.Base) a.Used = Math.Max(0, a.Used - 1);
        }

        return Task.CompletedTask;
    }

    public Task<UsageSummary> GetAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        lock (_orgs)
        {
            var a = Get(organizationId);
            return Task.FromResult(new UsageSummary(a.Used, a.Limit, a.TrialPlan && !a.PaidOnce));
        }
    }

    public Task SetLimitAsync(string organizationId, int? limit, bool trial = false, CancellationToken cancellationToken = default)
    {
        lock (_orgs)
        {
            var a = Get(organizationId);
            a.Limit = limit;
            a.TrialPlan = trial && limit is not null;
        }

        return Task.CompletedTask;
    }

    Task<BillingAccount> IBillingStore.GetAsync(string organizationId, CancellationToken cancellationToken)
    {
        lock (_orgs)
        {
            var a = Get(organizationId);
            return Task.FromResult(new BillingAccount(
                a.CustomerId, a.SubscriptionId, a.PlanId, a.Status, a.PeriodStart, a.PeriodEnd, false,
                a.PeriodAllowance, a.PeriodUsed, a.Credits));
        }
    }

    public Task<string?> FindOrganizationByCustomerAsync(string customerId, CancellationToken cancellationToken = default)
    {
        lock (_orgs)
        {
            return Task.FromResult(_customers.GetValueOrDefault(customerId));
        }
    }

    public Task<string> LinkCustomerAsync(string organizationId, string customerId, CancellationToken cancellationToken = default)
    {
        lock (_orgs)
        {
            var a = Get(organizationId);
            a.CustomerId ??= customerId;
            _customers.TryAdd(a.CustomerId, organizationId);
            return Task.FromResult(a.CustomerId);
        }
    }

    public Task<BillingWrite> AddCreditsAsync(string organizationId, int credits, string idempotencyKey, CancellationToken cancellationToken = default)
        => Once(idempotencyKey, () =>
        {
            var a = Get(organizationId);
            a.Credits += credits;
            a.PaidOnce = true;
        });

    public Task<BillingWrite> ApplySubscriptionAsync(
        string organizationId, SubscriptionState state, DateTimeOffset eventCreated, string idempotencyKey, CancellationToken cancellationToken = default)
        => Once(idempotencyKey, () =>
        {
            var a = Get(organizationId);
            a.SubscriptionId = state.SubscriptionId;
            a.Status = state.Status;
            a.PlanId = state.PlanId;
            a.PeriodAllowance = state.MonthlyConversions;
            a.PeriodStart = state.PeriodStart;
            a.PeriodEnd = state.PeriodEnd;
            a.CancelAtPeriodEnd = state.CancelAtPeriodEnd;
            if (state.MonthlyConversions is not null)
            {
                a.SubscribedOnce = true;
                a.PaidOnce = true;
            }
        });

    public Task<BillingWrite> StartPeriodAsync(string organizationId, DateTimeOffset periodStart, string idempotencyKey, CancellationToken cancellationToken = default)
        => Once(idempotencyKey, () => Get(organizationId).PeriodUsed = 0);

    public Task<BillingWrite> MarkPastDueAsync(
        string organizationId, string subscriptionId, DateTimeOffset eventCreated, string idempotencyKey, CancellationToken cancellationToken = default)
        => Once(idempotencyKey, () => Get(organizationId).Status = SubscriptionStatuses.PastDue);

    private Task<BillingWrite> Once(string key, Action apply)
    {
        lock (_orgs)
        {
            if (!_applied.Add(key))
            {
                return Task.FromResult(BillingWrite.Duplicate);
            }

            apply();
            return Task.FromResult(BillingWrite.Applied);
        }
    }

    public int RemoveOrganization(string organizationId)
    {
        lock (_orgs)
        {
            return _orgs.Remove(organizationId) ? 1 : 0;
        }
    }

    public int RemoveCustomer(string organizationId, string customerId)
    {
        lock (_orgs)
        {
            return _customers.TryGetValue(customerId, out var owner) && owner == organizationId && _customers.Remove(customerId) ? 1 : 0;
        }
    }

    private Allowance Get(string organizationId)
    {
        if (!_orgs.TryGetValue(organizationId, out var allowance))
        {
            _orgs[organizationId] = allowance = new Allowance();
        }

        return allowance;
    }

    public sealed class Allowance
    {
        public int Used { get; set; }

        public int? Limit { get; set; }

        public bool TrialPlan { get; set; }

        public int? PeriodAllowance { get; set; }

        public int PeriodUsed { get; set; }

        public int Credits { get; set; }

        public bool SubscribedOnce { get; set; }

        public bool PaidOnce { get; set; }

        public bool CancelAtPeriodEnd { get; set; }

        public string? CustomerId { get; set; }

        public string? SubscriptionId { get; set; }

        public string? PlanId { get; set; }

        public string? Status { get; set; }

        public DateTimeOffset? PeriodStart { get; set; }

        public DateTimeOffset? PeriodEnd { get; set; }
    }
}
