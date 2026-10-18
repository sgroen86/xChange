using InvoicePlatform.Application.Usage;

namespace InvoicePlatform.Application.Tests;

/// <summary>Conversions in memory: no limit unless one is set, as DynamoDB does it.</summary>
internal sealed class FakeUsageMeter : IUsageMeter
{
    private readonly Dictionary<string, (int Used, int? Limit, bool Trial)> _usage = [];

    public Task<bool> TryConsumeAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        var (used, limit, trial) = _usage.GetValueOrDefault(organizationId);
        if (limit is { } max && used >= max)
        {
            return Task.FromResult(false);
        }

        _usage[organizationId] = (used + 1, limit, trial);
        return Task.FromResult(true);
    }

    public Task RefundAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        var (used, limit, trial) = _usage.GetValueOrDefault(organizationId);
        if (used > 0)
        {
            _usage[organizationId] = (used - 1, limit, trial);
        }

        return Task.CompletedTask;
    }

    public Task<UsageSummary> GetAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        var (used, limit, trial) = _usage.GetValueOrDefault(organizationId);
        return Task.FromResult(new UsageSummary(used, limit, trial));
    }

    public Task SetLimitAsync(string organizationId, int? limit, bool trial = false, CancellationToken cancellationToken = default)
    {
        _usage[organizationId] = (_usage.GetValueOrDefault(organizationId).Used, limit, trial && limit is not null);
        return Task.CompletedTask;
    }
}
