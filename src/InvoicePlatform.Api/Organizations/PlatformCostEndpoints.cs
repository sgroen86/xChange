using System.Text;
using InvoicePlatform.Api.Identity;
using InvoicePlatform.Application.Usage;
using InvoicePlatform.Contracts.Costs;

namespace InvoicePlatform.Api.Organizations;

/// <summary>
/// What model calls cost, per month, across the platform and per organisation.
/// Platform administrator only: customers see their conversions, never model cost.
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
