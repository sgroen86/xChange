using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using InvoicePlatform.Application.Interpretation;

namespace InvoicePlatform.Application.Usage;

/// <summary>
/// Every model call and what it cost, per organisation, with running sums per
/// month for the organisation, its users and models, and for the platform.
/// Tenant-owned: an event and an organisation's sums are kept and read per
/// organisation. Only the platform administrator reads across organisations.
/// </summary>
public interface IUsageLedger
{
    /// <summary>Stores the event and adds it to every sum it belongs to.</summary>
    Task RecordAsync(UsageEvent usageEvent, CancellationToken cancellationToken = default);

    /// <summary>The platform's sums for a month: in total, per day, per model and per organisation.</summary>
    Task<PlatformCostMonth> GetPlatformMonthAsync(UsageMonth month, CancellationToken cancellationToken = default);

    /// <summary>One organisation's sums for a month: in total, per actor and per model.</summary>
    Task<OrganizationCostMonth> GetOrganizationMonthAsync(string organizationId, UsageMonth month, CancellationToken cancellationToken = default);

    /// <summary>One organisation's events in a month.</summary>
    Task<IReadOnlyList<UsageEvent>> ListEventsAsync(string organizationId, UsageMonth month, CancellationToken cancellationToken = default);
}

/// <summary>Where a model call came from.</summary>
public static class UsageSources
{
    /// <summary>A single PDF uploaded and read while the user waits.</summary>
    public const string Upload = "upload";

    /// <summary>A PDF from a batch upload, read by the worker.</summary>
    public const string Batch = "batch";

    /// <summary>A mailed PDF, read by the worker after approval.</summary>
    public const string Email = "email";
}

/// <summary>Who a model call is booked to, when it is not a signed-in user.</summary>
public static class UsageActors
{
    /// <summary>A PDF that arrived by email: nobody uploaded it.</summary>
    public const string Email = "email";
}

/// <summary>One model call. Its cost is fixed when it is recorded.</summary>
public sealed record UsageEvent
{
    public required string EventId { get; init; }

    public required string OrganizationId { get; init; }

    /// <summary>A user id, <see cref="UsageActors.Email"/>, or (later) "apikey:&lt;id&gt;".</summary>
    public required string Actor { get; init; }

    /// <summary>One of <see cref="UsageSources"/>.</summary>
    public required string Source { get; init; }

    /// <summary>The extraction this call was for; not an invoice id.</summary>
    public required string ExtractionId { get; init; }

    public required string Model { get; init; }

    public required TokenUsage Tokens { get; init; }

    public required decimal CostUsd { get; init; }

    public required string PriceVersion { get; init; }

    public required bool PriceKnown { get; init; }

    /// <summary>False when the provider answered (and was paid) but the answer could not be used.</summary>
    public required bool Succeeded { get; init; }

    public required DateTimeOffset At { get; init; }
}

/// <summary>Running sums: calls, tokens per kind and cost.</summary>
public sealed record CostTotals(
    long Calls,
    long FailedCalls,
    long InputTokens,
    long OutputTokens,
    long CacheWriteTokens,
    long CacheReadTokens,
    decimal CostUsd)
{
    public static readonly CostTotals Zero = new(0, 0, 0, 0, 0, 0, 0m);

    /// <summary>Cost per call, failed calls included, to the micro-dollar.</summary>
    public decimal AverageCostUsd => Calls == 0
        ? 0m
        : Math.Round(CostUsd / Calls, ModelPrices.Decimals, MidpointRounding.AwayFromZero);

    public CostTotals Plus(UsageEvent usageEvent) => new(
        Calls + 1,
        FailedCalls + (usageEvent.Succeeded ? 0 : 1),
        InputTokens + usageEvent.Tokens.InputTokens,
        OutputTokens + usageEvent.Tokens.OutputTokens,
        CacheWriteTokens + usageEvent.Tokens.CacheWriteTokens,
        CacheReadTokens + usageEvent.Tokens.CacheReadTokens,
        CostUsd + usageEvent.CostUsd);

    public static CostTotals Of(IEnumerable<UsageEvent> events) => events.Aggregate(Zero, (totals, e) => totals.Plus(e));
}

/// <param name="Days">Keyed by yyyy-MM-dd (UTC); only days with calls.</param>
/// <param name="Models">Keyed by model id.</param>
/// <param name="Organizations">Keyed by organisation id.</param>
public sealed record PlatformCostMonth(
    CostTotals Total,
    IReadOnlyDictionary<string, CostTotals> Days,
    IReadOnlyDictionary<string, CostTotals> Models,
    IReadOnlyDictionary<string, CostTotals> Organizations);

/// <param name="Actors">Keyed by actor (see <see cref="UsageEvent.Actor"/>).</param>
/// <param name="Models">Keyed by model id.</param>
public sealed record OrganizationCostMonth(
    CostTotals Total,
    IReadOnlyDictionary<string, CostTotals> Actors,
    IReadOnlyDictionary<string, CostTotals> Models);

/// <summary>A calendar month in UTC, written yyyy-MM.</summary>
public readonly record struct UsageMonth
{
    public UsageMonth(int year, int month)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, 2000);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, 9999);
        ArgumentOutOfRangeException.ThrowIfLessThan(month, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(month, 12);
        Year = year;
        Month = month;
    }

    public int Year { get; }

    public int Month { get; }

    public static UsageMonth Of(DateTimeOffset moment)
    {
        var utc = moment.UtcDateTime;
        return new UsageMonth(utc.Year, utc.Month);
    }

    /// <summary>Exactly yyyy-MM, nothing else.</summary>
    public static bool TryParse([NotNullWhen(true)] string? text, out UsageMonth month)
    {
        month = default;
        if (text is not { Length: 7 } || text[4] != '-'
            || !int.TryParse(text.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year)
            || !int.TryParse(text.AsSpan(5, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            || year < 2000 || number is < 1 or > 12)
        {
            return false;
        }

        month = new UsageMonth(year, number);
        return true;
    }

    public override string ToString() => $"{Year:D4}-{Month:D2}";
}
