using InvoicePlatform.Application.Quality;
using InvoicePlatform.Application.Usage;

namespace InvoicePlatform.Integration.Tests;

/// <summary>
/// The quality ledger in memory: measurements per organisation, as the
/// table's partition keeps them, with the sums worked out on reading.
/// </summary>
public sealed class InMemoryQualityLedger : IQualityLedger
{
    private readonly Dictionary<string, List<QualityMeasurement>> _measurements = [];

    public IReadOnlyList<QualityMeasurement> Of(string organizationId)
    {
        lock (_measurements)
        {
            return [.. _measurements.GetValueOrDefault(organizationId, [])];
        }
    }

    public Task RecordAsync(QualityMeasurement measurement, CancellationToken cancellationToken = default)
    {
        lock (_measurements)
        {
            if (!_measurements.TryGetValue(measurement.OrganizationId, out var measurements))
            {
                _measurements[measurement.OrganizationId] = measurements = [];
            }

            measurements.Add(measurement);
        }

        return Task.CompletedTask;
    }

    public Task<PlatformQualityMonth> GetPlatformMonthAsync(UsageMonth month, CancellationToken cancellationToken = default)
    {
        List<QualityMeasurement> inMonth;
        lock (_measurements)
        {
            inMonth = [.. _measurements.Values.SelectMany(m => m).Where(m => UsageMonth.Of(m.At) == month)];
        }

        return Task.FromResult(new PlatformQualityMonth(
            QualitySums.Of(inMonth),
            inMonth.GroupBy(m => m.Model).ToDictionary(g => g.Key, QualitySums.Of),
            inMonth.GroupBy(m => m.OrganizationId).ToDictionary(g => g.Key, QualitySums.Of),
            inMonth.SelectMany(FieldSums.Of)
                .GroupBy(f => f.Key)
                .ToDictionary(g => g.Key, g => g.Aggregate(FieldSums.Zero, (sum, f) => sum.Plus(f.Value)))));
    }
}
