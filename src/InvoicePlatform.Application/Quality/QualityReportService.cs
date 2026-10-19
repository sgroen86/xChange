using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Application.Usage;
using InvoicePlatform.Domain.Quality;

namespace InvoicePlatform.Application.Quality;

/// <summary>
/// How well the model read the invoices booked in a month, for the platform
/// administrator: in total, per model (with what a perfect invoice cost), per
/// organisation, and the fields that go wrong most. Who may call this is the
/// API's concern (platform administrators only).
///
/// An upper bound: a field the reviewer left as it was counts as correct,
/// whether or not anybody checked it.
/// </summary>
public sealed class QualityReportService(IQualityLedger quality, IUsageLedger costs, IPlatformStore platform)
{
    public async Task<PlatformQualityReport> MonthAsync(UsageMonth month, CancellationToken cancellationToken = default)
    {
        var sums = await quality.GetPlatformMonthAsync(month, cancellationToken).ConfigureAwait(false);
        var cost = await costs.GetPlatformMonthAsync(month, cancellationToken).ConfigureAwait(false);
        var names = (await platform.ListOrganizationsAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(o => o.OrganizationId, o => o.Name, StringComparer.Ordinal);

        return new PlatformQualityReport(
            month,
            sums.Total,
            cost.Total.CostUsd,
            CostPerPerfectInvoice(cost.Total.CostUsd, sums.Total),
            [.. ByMeasured(sums.Models).Select(m =>
            {
                var modelCost = cost.Models.GetValueOrDefault(m.Key)?.CostUsd ?? 0m;
                return new QualityRow(m.Key, null, m.Value, modelCost, CostPerPerfectInvoice(modelCost, m.Value));
            })],
            [.. ByMeasured(sums.Organizations).Select(o => new QualityRow(o.Key, names.GetValueOrDefault(o.Key), o.Value, null, null))],
            [.. sums.Fields
                .Where(f => f.Value.Read.NotCorrect > 0 || f.Value.Shown.NotCorrect > 0)
                .OrderByDescending(f => f.Value.Read.NotCorrect)
                .ThenByDescending(f => f.Value.Shown.NotCorrect)
                .ThenBy(f => f.Key, StringComparer.Ordinal)
                .Select(f => new FieldQualityRow(f.Key, f.Value))]);
    }

    /// <summary>
    /// All the model cost of the month (every call, booked or not) per
    /// invoice the model read perfectly; null when none was perfect.
    /// </summary>
    private static decimal? CostPerPerfectInvoice(decimal costUsd, QualitySums sums)
        => sums.Read.PerfectInvoices == 0
            ? null
            : Math.Round(costUsd / sums.Read.PerfectInvoices, ModelPrices.Decimals, MidpointRounding.AwayFromZero);

    private static IEnumerable<KeyValuePair<string, QualitySums>> ByMeasured(IReadOnlyDictionary<string, QualitySums> sums)
        => sums.OrderByDescending(s => s.Value.Read.Invoices).ThenBy(s => s.Key, StringComparer.Ordinal);
}

/// <param name="Key">A model id or an organisation id.</param>
/// <param name="Name">The organisation's name, when the row is one and it is known.</param>
/// <param name="CostUsd">The model's cost that month, for a model row.</param>
/// <param name="CostPerPerfectInvoiceUsd">For a model row; null when it read no invoice perfectly.</param>
public sealed record QualityRow(string Key, string? Name, QualitySums Sums, decimal? CostUsd, decimal? CostPerPerfectInvoiceUsd);

/// <param name="Field">A field name as <see cref="FieldComparison.Field"/> gives it.</param>
public sealed record FieldQualityRow(string Field, FieldSums Sums);

/// <param name="Fields">Only fields that went wrong at least once, most often wrong (in the model's reading) first.</param>
public sealed record PlatformQualityReport(
    UsageMonth Month,
    QualitySums Total,
    decimal CostUsd,
    decimal? CostPerPerfectInvoiceUsd,
    IReadOnlyList<QualityRow> Models,
    IReadOnlyList<QualityRow> Organizations,
    IReadOnlyList<FieldQualityRow> Fields);
