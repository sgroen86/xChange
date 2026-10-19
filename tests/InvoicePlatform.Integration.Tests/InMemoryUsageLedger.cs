using InvoicePlatform.Application.Usage;

namespace InvoicePlatform.Integration.Tests;

/// <summary>
/// The ledger in memory: events per organisation, as the table's partition
/// keeps them, with the sums worked out on reading.
/// </summary>
public sealed class InMemoryUsageLedger : IUsageLedger
{
    private readonly Dictionary<string, List<UsageEvent>> _events = [];

    public Task RecordAsync(UsageEvent usageEvent, CancellationToken cancellationToken = default)
    {
        lock (_events)
        {
            if (!_events.TryGetValue(usageEvent.OrganizationId, out var events))
            {
                _events[usageEvent.OrganizationId] = events = [];
            }

            events.Add(usageEvent);
        }

        return Task.CompletedTask;
    }

    public Task<PlatformCostMonth> GetPlatformMonthAsync(UsageMonth month, CancellationToken cancellationToken = default)
    {
        List<UsageEvent> events;
        lock (_events)
        {
            events = [.. _events.Values.SelectMany(e => e).Where(e => UsageMonth.Of(e.At) == month)];
        }

        return Task.FromResult(new PlatformCostMonth(
            CostTotals.Of(events),
            Sums(events, e => e.At.UtcDateTime.ToString("yyyy-MM-dd")),
            Sums(events, e => e.Model),
            Sums(events, e => e.OrganizationId)));
    }

    public Task<OrganizationCostMonth> GetOrganizationMonthAsync(string organizationId, UsageMonth month, CancellationToken cancellationToken = default)
    {
        var events = InMonth(organizationId, month);
        return Task.FromResult(new OrganizationCostMonth(CostTotals.Of(events), Sums(events, e => e.Actor), Sums(events, e => e.Model)));
    }

    public Task<IReadOnlyList<UsageEvent>> ListEventsAsync(string organizationId, UsageMonth month, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<UsageEvent>>(InMonth(organizationId, month));

    private List<UsageEvent> InMonth(string organizationId, UsageMonth month)
    {
        lock (_events)
        {
            return [.. _events.GetValueOrDefault(organizationId, []).Where(e => UsageMonth.Of(e.At) == month)];
        }
    }

    private static Dictionary<string, CostTotals> Sums(IEnumerable<UsageEvent> events, Func<UsageEvent, string> key)
        => events.GroupBy(key).ToDictionary(g => g.Key, CostTotals.Of);
}
