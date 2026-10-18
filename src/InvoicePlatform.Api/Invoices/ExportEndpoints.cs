using InvoicePlatform.Api.Identity;
using InvoicePlatform.Application.Invoices;
using InvoicePlatform.Contracts.Invoices;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.EInvoicing;

namespace InvoicePlatform.Api.Invoices;

/// <summary>
/// Checking a draft against a ruleset, and exporting a booked invoice. HTTP
/// only; the work is in <see cref="ExportInvoiceService"/>.
/// </summary>
public static class ExportEndpoints
{
    /// <summary>Official warnings for a downloaded file: base64 of a JSON array of violations.</summary>
    public const string WarningsHeader = "X-Validation-Warnings";

    public static RouteGroupBuilder MapExportEndpoints(this RouteGroupBuilder group)
    {
        // Checking stores nothing and exporting is reading, so every role may.
        group.MapPost("/rulesets/{rulesetId}/check", CheckAsync)
            .WithName("CheckInvoice")
            .RequireAuthenticatedUser();

        group.MapGet("/invoices/{invoiceId}/export", ExportAsync)
            .WithName("ExportInvoice")
            .RequireAuthenticatedUser();

        return group;
    }

    private static async Task<IResult> CheckAsync(
        string rulesetId,
        HttpContext context,
        ExportInvoiceService service,
        CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;

        var draft = await RequestBody.ReadAsync<CanonicalInvoiceDraft>(context, cancellationToken).ConfigureAwait(false);
        if (draft is null)
        {
            return RequestBody.Invalid();
        }

        var outcome = await service.CheckAsync(user.OrganizationId, rulesetId, draft, cancellationToken)
            .ConfigureAwait(false);

        return outcome.RulesetFound
            ? Results.Ok(ToResponse(outcome.Violations))
            : Results.Problem(detail: $"Regelset '{rulesetId}' bestaat niet.", title: "Onbekende regelset", statusCode: StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> ExportAsync(
        string invoiceId,
        string? ruleset,
        HttpContext context,
        ExportInvoiceService service,
        CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;

        var outcome = await service.ExportAsync(user.OrganizationId, invoiceId, ruleset, cancellationToken)
            .ConfigureAwait(false);

        if (outcome.Kind == ExportOutcomeKind.Exported && outcome.Warnings is { Count: > 0 } warnings)
        {
            var json = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(ToResponse(warnings).Violations);
            context.Response.Headers[WarningsHeader] = Convert.ToBase64String(json);
        }

        return outcome.Kind switch
        {
            ExportOutcomeKind.Exported => Results.File(outcome.Content!, ExportInvoiceService.MediaType, outcome.FileName),
            ExportOutcomeKind.ValidationUnavailable => Results.Problem(
                detail: "De officiële controle is nu niet beschikbaar. Probeer het zo opnieuw.",
                title: "Controle niet beschikbaar",
                statusCode: StatusCodes.Status503ServiceUnavailable),
            ExportOutcomeKind.Incomplete => Results.Json(ToResponse(outcome.Violations!), statusCode: StatusCodes.Status422UnprocessableEntity),
            ExportOutcomeKind.PlanRequired => Results.Problem(
                detail: "In de proefperiode kun je alles controleren, maar het bestand downloaden kan met een abonnement.",
                title: "Abonnement nodig",
                statusCode: StatusCodes.Status402PaymentRequired),
            ExportOutcomeKind.NotBooked => Results.Problem(
                detail: "Dit is een concept. Boek de factuur om het bestand te maken.",
                title: "Nog niet geboekt",
                statusCode: StatusCodes.Status409Conflict),
            ExportOutcomeKind.UnknownRuleset => Results.Problem(
                detail: $"Regelset '{ruleset}' bestaat niet.", title: "Onbekende regelset", statusCode: StatusCodes.Status400BadRequest),
            _ => Results.NotFound(),
        };
    }

    private static RuleViolationsResponse ToResponse(IReadOnlyList<RuleViolation> violations) => new()
    {
        Violations = [.. violations.Select(v => new RuleViolationResponse
        {
            RuleId = v.RuleId,
            BusinessTerm = v.BusinessTerm,
            Path = v.Path,
            Message = v.Message,
        })],
    };
}
