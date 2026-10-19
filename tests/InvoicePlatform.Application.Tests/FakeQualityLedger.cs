using InvoicePlatform.Application.Quality;
using InvoicePlatform.Application.Usage;

namespace InvoicePlatform.Application.Tests;

/// <summary>
/// The quality ledger in memory: measurements per organisation, as the
/// table's partition keeps them, with the sums worked out on reading.
/// </summary>
internal sealed class FakeQualityLedger : IQualityLedger
{
    private readonly Dictionary<string, List<QualityMeasurement>> _measurements = [];

    /// <summary>Makes recording throw, as a DynamoDB outage would.</summary>
    public bool Fail { get; set; }

    public IReadOnlyList<QualityMeasurement> All => [.. _measurements.Values.SelectMany(m => m)];

    public IReadOnlyList<QualityMeasurement> Of(string organizationId) => _measurements.GetValueOrDefault(organizationId, []);

    public Task RecordAsync(QualityMeasurement measurement, CancellationToken cancellationToken = default)
    {
        if (Fail)
        {
            throw new InvalidOperationException("The ledger is down.");
        }

        if (!_measurements.TryGetValue(measurement.OrganizationId, out var measurements))
        {
            _measurements[measurement.OrganizationId] = measurements = [];
        }

        measurements.Add(measurement);
        return Task.CompletedTask;
    }

    public Task<PlatformQualityMonth> GetPlatformMonthAsync(UsageMonth month, CancellationToken cancellationToken = default)
        => Task.FromResult(Month(All, month));

    /// <summary>The sums a month of measurements adds up to, as the table's atomic counters would hold them.</summary>
    public static PlatformQualityMonth Month(IEnumerable<QualityMeasurement> measurements, UsageMonth month)
    {
        var inMonth = measurements.Where(m => UsageMonth.Of(m.At) == month).ToList();
        return new PlatformQualityMonth(
            QualitySums.Of(inMonth),
            inMonth.GroupBy(m => m.Model).ToDictionary(g => g.Key, QualitySums.Of),
            inMonth.GroupBy(m => m.OrganizationId).ToDictionary(g => g.Key, QualitySums.Of),
            inMonth.SelectMany(FieldSums.Of)
                .GroupBy(f => f.Key)
                .ToDictionary(g => g.Key, g => g.Aggregate(FieldSums.Zero, (sum, f) => sum.Plus(f.Value))));
    }
}
