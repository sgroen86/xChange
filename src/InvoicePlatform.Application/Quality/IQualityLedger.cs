using InvoicePlatform.Application.Usage;
using InvoicePlatform.Domain.Quality;

namespace InvoicePlatform.Application.Quality;

/// <summary>
/// How well the model read the invoices reviewers booked: one measurement per
/// first booking of an upload, with running sums per month for the
/// organisation and its models, and for the platform per model, organisation
/// and field. Tenant-owned: a measurement and an organisation's sums are kept
/// per organisation. Only the platform administrator reads across organisations.
/// </summary>
public interface IQualityLedger
{
    /// <summary>Stores the measurement and adds it to every sum it belongs to.</summary>
    Task RecordAsync(QualityMeasurement measurement, CancellationToken cancellationToken = default);

    /// <summary>The platform's sums for a month: in total, per model, per organisation and per field.</summary>
    Task<PlatformQualityMonth> GetPlatformMonthAsync(UsageMonth month, CancellationToken cancellationToken = default);
}

/// <summary>
/// One booked invoice compared with what was read for it, twice: the model's
/// own reading (learning does not count), and what the reviewer was shown
/// (learned corrections applied, empty fields filled from settings and relations).
/// </summary>
public sealed record QualityMeasurement
{
    public required string MeasurementId { get; init; }

    public required string OrganizationId { get; init; }

    public required string ExtractionId { get; init; }

    public required string InvoiceId { get; init; }

    /// <summary>The model that read the PDF.</summary>
    public required string Model { get; init; }

    /// <summary>The user who booked the invoice.</summary>
    public required string Actor { get; init; }

    public required DateTimeOffset At { get; init; }

    /// <summary>The model's own reading against the booking.</summary>
    public required ReadingComparison Read { get; init; }

    /// <summary>What the reviewer was shown against the booking.</summary>
    public required ReadingComparison Shown { get; init; }
}

/// <summary>Fields counted, and how they came out.</summary>
public sealed record OutcomeCounts(long Counted, long Correct, long Corrected, long Missed, long Extra)
{
    public static readonly OutcomeCounts Zero = new(0, 0, 0, 0, 0);

    public long NotCorrect => Corrected + Missed + Extra;

    public OutcomeCounts Plus(OutcomeCounts other) => new(
        Counted + other.Counted,
        Correct + other.Correct,
        Corrected + other.Corrected,
        Missed + other.Missed,
        Extra + other.Extra);

    /// <summary>The outcomes of some of a reading's fields (all of them, or one field's occurrences).</summary>
    public static OutcomeCounts Of(IEnumerable<FieldComparison> fields)
    {
        long correct = 0, corrected = 0, missed = 0, extra = 0;
        foreach (var field in fields)
        {
            switch (field.Outcome)
            {
                case FieldOutcome.Correct: correct++; break;
                case FieldOutcome.Corrected: corrected++; break;
                case FieldOutcome.Missed: missed++; break;
                case FieldOutcome.Extra: extra++; break;
            }
        }

        return new(correct + corrected + missed + extra, correct, corrected, missed, extra);
    }
}

/// <summary>Running sums of one measure: invoices measured, perfect ones, and their fields.</summary>
public sealed record QualityCounts(long Invoices, long PerfectInvoices, OutcomeCounts Fields)
{
    public static readonly QualityCounts Zero = new(0, 0, OutcomeCounts.Zero);

    /// <summary>Correct fields per counted field, in percent to one decimal; null when nothing was counted.</summary>
    public decimal? FieldAccuracyPercent => QualityMath.Percent(Fields.Correct, Fields.Counted);

    /// <summary>Invoices without a single field wrong, in percent to one decimal; null when none was measured.</summary>
    public decimal? PerfectPercent => QualityMath.Percent(PerfectInvoices, Invoices);

    public QualityCounts Plus(ReadingComparison reading) => new(
        Invoices + 1,
        PerfectInvoices + (reading.Perfect ? 1 : 0),
        Fields.Plus(OutcomeCounts.Of(reading.Fields)));
}

/// <summary>Both measures of the same invoices.</summary>
public sealed record QualitySums(QualityCounts Read, QualityCounts Shown)
{
    public static readonly QualitySums Zero = new(QualityCounts.Zero, QualityCounts.Zero);

    public QualitySums Plus(QualityMeasurement measurement) => new(Read.Plus(measurement.Read), Shown.Plus(measurement.Shown));

    public static QualitySums Of(IEnumerable<QualityMeasurement> measurements) => measurements.Aggregate(Zero, (sums, m) => sums.Plus(m));
}

/// <summary>One field's outcomes in both measures, over every invoice (and line) it was counted in.</summary>
public sealed record FieldSums(OutcomeCounts Read, OutcomeCounts Shown)
{
    public static readonly FieldSums Zero = new(OutcomeCounts.Zero, OutcomeCounts.Zero);

    /// <summary>Per field name (<see cref="FieldComparison.Field"/>): what one measurement adds to each field it counted.</summary>
    public static IReadOnlyDictionary<string, FieldSums> Of(QualityMeasurement measurement)
    {
        var read = measurement.Read.Fields.GroupBy(f => f.Field, StringComparer.Ordinal).ToDictionary(g => g.Key, OutcomeCounts.Of, StringComparer.Ordinal);
        var shown = measurement.Shown.Fields.GroupBy(f => f.Field, StringComparer.Ordinal).ToDictionary(g => g.Key, OutcomeCounts.Of, StringComparer.Ordinal);

        return read.Keys.Union(shown.Keys, StringComparer.Ordinal).ToDictionary(
            field => field,
            field => new FieldSums(read.GetValueOrDefault(field, OutcomeCounts.Zero), shown.GetValueOrDefault(field, OutcomeCounts.Zero)),
            StringComparer.Ordinal);
    }

    public FieldSums Plus(FieldSums other) => new(Read.Plus(other.Read), Shown.Plus(other.Shown));
}

/// <param name="Models">Keyed by model id.</param>
/// <param name="Organizations">Keyed by organisation id.</param>
/// <param name="Fields">Keyed by field name, e.g. <c>lines[].netAmount</c>.</param>
public sealed record PlatformQualityMonth(
    QualitySums Total,
    IReadOnlyDictionary<string, QualitySums> Models,
    IReadOnlyDictionary<string, QualitySums> Organizations,
    IReadOnlyDictionary<string, FieldSums> Fields);

internal static class QualityMath
{
    /// <summary>part / whole x 100, to one decimal (80.0, not 80), half away from zero. Decimal throughout.</summary>
    public static decimal? Percent(long part, long whole)
        => whole == 0 ? null : Math.Round(part * 100m / whole, 1, MidpointRounding.AwayFromZero) + 0.0m;
}
