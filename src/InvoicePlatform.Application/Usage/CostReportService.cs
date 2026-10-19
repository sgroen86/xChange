using System.Globalization;
using System.Text;
using InvoicePlatform.Application.Identity;
using InvoicePlatform.Application.Organizations;

namespace InvoicePlatform.Application.Usage;

/// <summary>
/// What model calls cost, for the platform administrator: a month across the
/// platform, one organisation's month, and a month as CSV. Who may call this
/// is the API's concern (platform administrators only); customers see their
/// conversions, never model cost.
/// </summary>
public sealed class CostReportService(IUsageLedger ledger, IPlatformStore platform, IUserStore users, IClock clock)
{
    /// <summary>How many of an organisation's most expensive calls the detail shows.</summary>
    public const int TopEventCount = 20;

    public UsageMonth CurrentMonth => UsageMonth.Of(clock.UtcNow);

    public async Task<PlatformCostReport> MonthAsync(UsageMonth month, CancellationToken cancellationToken = default)
    {
        var sums = await ledger.GetPlatformMonthAsync(month, cancellationToken).ConfigureAwait(false);
        var names = await OrganizationNamesAsync(cancellationToken).ConfigureAwait(false);

        return new PlatformCostReport(
            month,
            ModelPrices.Version,
            sums.Total,
            [.. sums.Days.OrderBy(d => d.Key, StringComparer.Ordinal).Select(d => new CostRow(d.Key, null, d.Value))],
            [.. ByCost(sums.Models).Select(m => new CostRow(m.Key, null, m.Value))],
            [.. ByCost(sums.Organizations).Select(o => new CostRow(o.Key, names.GetValueOrDefault(o.Key), o.Value))]);
    }

    public async Task<OrganizationCostReport> OrganizationMonthAsync(
        string organizationId, UsageMonth month, CancellationToken cancellationToken = default)
    {
        var organization = await platform.FindOrganizationAsync(organizationId, cancellationToken).ConfigureAwait(false);
        var sums = await ledger.GetOrganizationMonthAsync(organizationId, month, cancellationToken).ConfigureAwait(false);
        var events = await ledger.ListEventsAsync(organizationId, month, cancellationToken).ConfigureAwait(false);

        var actors = new List<ActorCostRow>();
        foreach (var (actor, totals) in ByCost(sums.Actors))
        {
            var user = actor == UsageActors.Email
                ? null
                : await users.FindByIdAsync(organizationId, actor, cancellationToken).ConfigureAwait(false);
            actors.Add(new ActorCostRow(actor, user?.Name, user?.Email, totals));
        }

        var names = actors.ToDictionary(a => a.Actor, a => a.Name ?? a.Email, StringComparer.Ordinal);
        var top = events
            .OrderByDescending(e => e.CostUsd)
            .ThenByDescending(e => e.At)
            .Take(TopEventCount)
            .Select(e => new CostEventRow(e, names.GetValueOrDefault(e.Actor)))
            .ToList();

        return new OrganizationCostReport(
            month,
            organizationId,
            organization?.Name,
            sums.Total,
            actors,
            [.. ByCost(sums.Models).Select(m => new CostRow(m.Key, null, m.Value))],
            top);
    }

    /// <summary>One row per organisation, most expensive first; amounts with a decimal point, never a comma.</summary>
    public async Task<string> ExportCsvAsync(UsageMonth month, CancellationToken cancellationToken = default)
    {
        var report = await MonthAsync(month, cancellationToken).ConfigureAwait(false);
        var csv = new StringBuilder();
        csv.Append("month,organizationId,organization,calls,failedCalls,inputTokens,outputTokens,cacheWriteTokens,cacheReadTokens,costUsd,averageCostUsd,priceVersion\r\n");

        foreach (var row in report.Organizations)
        {
            var t = row.Totals;
            csv.AppendJoin(',',
                    month.ToString(),
                    Field(row.Key),
                    Field(row.Name ?? ""),
                    Number(t.Calls),
                    Number(t.FailedCalls),
                    Number(t.InputTokens),
                    Number(t.OutputTokens),
                    Number(t.CacheWriteTokens),
                    Number(t.CacheReadTokens),
                    t.CostUsd.ToString(CultureInfo.InvariantCulture),
                    t.AverageCostUsd.ToString(CultureInfo.InvariantCulture),
                    report.PriceVersion)
                .Append("\r\n");
        }

        return csv.ToString();
    }

    private async Task<Dictionary<string, string>> OrganizationNamesAsync(CancellationToken cancellationToken)
        => (await platform.ListOrganizationsAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(o => o.OrganizationId, o => o.Name, StringComparer.Ordinal);

    private static IEnumerable<KeyValuePair<string, CostTotals>> ByCost(IReadOnlyDictionary<string, CostTotals> sums)
        => sums.OrderByDescending(s => s.Value.CostUsd).ThenBy(s => s.Key, StringComparer.Ordinal);

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Quoted when needed, and never read as a formula: an organisation's name
    /// is typed by a customer, and a spreadsheet would run "=..." in a cell.
    /// </summary>
    private static string Field(string value)
    {
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            value = "'" + value;
        }

        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : value;
    }
}

/// <param name="Key">A day (yyyy-MM-dd), a model id or an organisation id.</param>
/// <param name="Name">The organisation's name, when the row is one and it is known.</param>
public sealed record CostRow(string Key, string? Name, CostTotals Totals);

/// <param name="Name">The user's name, when the actor is a user who still exists.</param>
public sealed record ActorCostRow(string Actor, string? Name, string? Email, CostTotals Totals);

/// <param name="ActorName">The user's name (or email) when known.</param>
public sealed record CostEventRow(UsageEvent Event, string? ActorName);

public sealed record PlatformCostReport(
    UsageMonth Month,
    string PriceVersion,
    CostTotals Total,
    IReadOnlyList<CostRow> Days,
    IReadOnlyList<CostRow> Models,
    IReadOnlyList<CostRow> Organizations);

public sealed record OrganizationCostReport(
    UsageMonth Month,
    string OrganizationId,
    string? Name,
    CostTotals Total,
    IReadOnlyList<ActorCostRow> Users,
    IReadOnlyList<CostRow> Models,
    IReadOnlyList<CostEventRow> TopEvents);
