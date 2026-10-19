using System.Text;
using InvoicePlatform.Api.Identity;
using InvoicePlatform.Application.Quality;
using InvoicePlatform.Application.Usage;
using InvoicePlatform.Contracts.Costs;

namespace InvoicePlatform.Api.Organizations;

/// <summary>
/// What model calls cost, per month, across the platform and per organisation,
/// and how well the model read what reviewers booked. Platform administrator
/// only: customers see their conversions, never model cost.
/// </summary>
public static class PlatformCostEndpoints
{
    public static RouteGroupBuilder MapPlatformCostEndpoints(this RouteGroupBuilder group)
    {
        // Guards per endpoint, never on the group (see Program.cs).
        group.MapGet("/platform/costs", MonthAsync)
            .WithName("GetPlatformCosts")
            .RequirePlatformAdmin();

        group.MapGet("/platform/costs/organizations/{organizationId}", OrganizationAsync)
            .WithName("GetOrganizationCosts")
            .RequirePlatformAdmin();

        group.MapGet("/platform/costs/export", ExportAsync)
            .WithName("ExportPlatformCosts")
            .RequirePlatformAdmin();

        group.MapGet("/platform/costs/quality", QualityAsync)
            .WithName("GetPlatformQuality")
            .RequirePlatformAdmin();

        return group;
    }

    private static async Task<IResult> MonthAsync(string? month, CostReportService costs, CancellationToken cancellationToken)
    {
        if (!TryMonth(month, costs, out var chosen))
        {
            return InvalidMonth();
        }

        var report = await costs.MonthAsync(chosen, cancellationToken).ConfigureAwait(false);
        return Results.Ok(new PlatformCostsResponse
        {
            Month = report.Month.ToString(),
            PriceVersion = report.PriceVersion,
            Total = ToResponse(report.Total),
            Days = [.. report.Days.Select(ToResponse)],
            Models = [.. report.Models.Select(ToResponse)],
            Organizations = [.. report.Organizations.Select(ToResponse)],
        });
    }

    private static async Task<IResult> OrganizationAsync(
        string organizationId, string? month, CostReportService costs, CancellationToken cancellationToken)
    {
        if (!TryMonth(month, costs, out var chosen))
        {
            return InvalidMonth();
        }

        var report = await costs.OrganizationMonthAsync(organizationId, chosen, cancellationToken).ConfigureAwait(false);
        return Results.Ok(new OrganizationCostsResponse
        {
            Month = report.Month.ToString(),
            OrganizationId = report.OrganizationId,
            Name = report.Name,
            Total = ToResponse(report.Total),
            Users = [.. report.Users.Select(u => new CostActorResponse { Actor = u.Actor, Name = u.Name, Email = u.Email, Totals = ToResponse(u.Totals) })],
            Models = [.. report.Models.Select(ToResponse)],
            TopEvents = [.. report.TopEvents.Select(ToResponse)],
        });
    }

    private static async Task<IResult> ExportAsync(string? month, CostReportService costs, CancellationToken cancellationToken)
    {
        if (!TryMonth(month, costs, out var chosen))
        {
            return InvalidMonth();
        }

        var csv = await costs.ExportCsvAsync(chosen, cancellationToken).ConfigureAwait(false);
        return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", $"xchange-costs-{chosen}.csv");
    }

    private static async Task<IResult> QualityAsync(
        string? month, CostReportService costs, QualityReportService quality, CancellationToken cancellationToken)
    {
        if (!TryMonth(month, costs, out var chosen))
        {
            return InvalidMonth();
        }

        var report = await quality.MonthAsync(chosen, cancellationToken).ConfigureAwait(false);
        return Results.Ok(new PlatformQualityResponse
        {
            Month = report.Month.ToString(),
            Total = ToResponse(report.Total),
            CostUsd = report.CostUsd,
            CostPerPerfectInvoiceUsd = report.CostPerPerfectInvoiceUsd,
            Models = [.. report.Models.Select(ToResponse)],
            Organizations = [.. report.Organizations.Select(ToResponse)],
            Fields = [.. report.Fields.Select(f => new FieldQualityResponse { Field = f.Field, Read = ToResponse(f.Sums.Read), Shown = ToResponse(f.Sums.Shown) })],
        });
    }

    /// <summary>No month means this one; anything but yyyy-MM is refused.</summary>
    private static bool TryMonth(string? text, CostReportService costs, out UsageMonth month)
    {
        if (text is null)
        {
            month = costs.CurrentMonth;
            return true;
        }

        return UsageMonth.TryParse(text, out month);
    }

    private static IResult InvalidMonth() => Results.Problem(
        detail: OrganizationApiText.Current.InvalidMonthDetail,
        title: OrganizationApiText.Current.InvalidMonthTitle,
        statusCode: StatusCodes.Status400BadRequest);

    private static CostTotalsResponse ToResponse(CostTotals totals) => new()
    {
        Calls = totals.Calls,
        FailedCalls = totals.FailedCalls,
        InputTokens = totals.InputTokens,
        OutputTokens = totals.OutputTokens,
        CacheWriteTokens = totals.CacheWriteTokens,
        CacheReadTokens = totals.CacheReadTokens,
        CostUsd = totals.CostUsd,
        AverageCostUsd = totals.AverageCostUsd,
    };

    private static QualityRowResponse ToResponse(QualityRow row) => new()
    {
        Key = row.Key,
        Name = row.Name,
        Sums = ToResponse(row.Sums),
        CostUsd = row.CostUsd,
        CostPerPerfectInvoiceUsd = row.CostPerPerfectInvoiceUsd,
    };

    private static QualitySumsResponse ToResponse(QualitySums sums) => new() { Read = ToResponse(sums.Read), Shown = ToResponse(sums.Shown) };

    private static QualityCountsResponse ToResponse(QualityCounts counts) => new()
    {
        Invoices = counts.Invoices,
        PerfectInvoices = counts.PerfectInvoices,
        Fields = ToResponse(counts.Fields),
        FieldAccuracyPercent = counts.FieldAccuracyPercent,
        PerfectPercent = counts.PerfectPercent,
    };

    private static OutcomeCountsResponse ToResponse(OutcomeCounts counts) => new()
    {
        Counted = counts.Counted,
        Correct = counts.Correct,
        Corrected = counts.Corrected,
        Missed = counts.Missed,
        Extra = counts.Extra,
    };

    private static CostRowResponse ToResponse(CostRow row) => new() { Key = row.Key, Name = row.Name, Totals = ToResponse(row.Totals) };

    private static CostEventResponse ToResponse(CostEventRow row) => new()
    {
        EventId = row.Event.EventId,
        At = row.Event.At,
        Actor = row.Event.Actor,
        ActorName = row.ActorName,
        Source = row.Event.Source,
        ExtractionId = row.Event.ExtractionId,
        Model = row.Event.Model,
        InputTokens = row.Event.Tokens.InputTokens,
        OutputTokens = row.Event.Tokens.OutputTokens,
        CacheWriteTokens = row.Event.Tokens.CacheWriteTokens,
        CacheReadTokens = row.Event.Tokens.CacheReadTokens,
        CostUsd = row.Event.CostUsd,
        PriceVersion = row.Event.PriceVersion,
        PriceKnown = row.Event.PriceKnown,
        Succeeded = row.Event.Succeeded,
    };
}
