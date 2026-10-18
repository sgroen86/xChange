using InvoicePlatform.Application.Usage;

namespace InvoicePlatform.Application.Tests;

/// <summary>Conversions in memory: no limit unless one is set, as DynamoDB does it.</summary>
internal sealed class FakeUsageMeter : IUsageMeter
{
    private readonly Dictionary<string, (int Used, int? Limit)> _usage = [];

    public Task<bool> TryConsumeAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        var (used, limit) = _usage.GetValueOrDefault(organizationId);
        if (limit is { } max && used >= max)
        {
            return Task.FromResult(false);
        }

        _usage[organizationId] = (used + 1, limit);
        return Task.FromResult(true);
    }

    public Task RefundAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        var (used, limit) = _usage.GetValueOrDefault(organizationId);
        if (used > 0)
        {
            _usage[organizationId] = (used - 1, limit);
        }

        return Task.CompletedTask;
    }

    public Task<UsageSummary> GetAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        var (used, limit) = _usage.GetValueOrDefault(organizationId);
        return Task.FromResult(new UsageSummary(used, limit));
    }

    public Task SetLimitAsync(string organizationId, int? limit, CancellationToken cancellationToken = default)
    {
        _usage[organizationId] = (_usage.GetValueOrDefault(organizationId).Used, limit);
        return Task.CompletedTask;
    }
}
