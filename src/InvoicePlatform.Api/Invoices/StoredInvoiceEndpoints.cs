using InvoicePlatform.Api.Identity;
using InvoicePlatform.Application.Invoices;
using InvoicePlatform.Contracts.Invoices;
using InvoicePlatform.Domain.Invoices;

namespace InvoicePlatform.Api.Invoices;

/// <summary>
/// HTTP surface for stored invoices. The organisation comes from the signed-in
/// user and never from the request, so a caller cannot ask for someone else's
/// invoices by changing a parameter.
/// </summary>
public static class StoredInvoiceEndpoints
{
    public static RouteGroupBuilder MapStoredInvoiceEndpoints(this RouteGroupBuilder group)
    {
        // Guards go on each endpoint rather than on the group: these Map*
        // helpers return the group, so a guard chained at group level silently
        // covers every route in it, login included.
        group.MapPost("/invoices", BookAsync)
            .WithName("BookInvoice")
            .RequireAuthenticatedUser()
            .RequireRole(UserRoleName.Admin, UserRoleName.User);

        group.MapPost("/invoices/drafts", SaveDraftAsync)
            .WithName("SaveInvoiceDraft")
            .RequireAuthenticatedUser()
            .RequireRole(UserRoleName.Admin, UserRoleName.User);

        group.MapDelete("/invoices/{invoiceId}", DeleteAsync)
            .WithName("DeleteInvoice")
            .RequireAuthenticatedUser()
            .RequireRole(UserRoleName.Admin, UserRoleName.User);

        group.MapGet("/invoices", ListAsync)
            .WithName("ListInvoices")
            .RequireAuthenticatedUser();

        group.MapGet("/invoices/{invoiceId}", GetAsync)
            .WithName("GetInvoice")
            .RequireAuthenticatedUser();

        group.MapGet("/invoices/{invoiceId}/document", GetDocumentAsync)
            .WithName("GetInvoiceDocument")
            .RequireAuthenticatedUser();

        return group;
    }

    private static async Task<IResult> BookAsync(
        HttpContext context,
        BookInvoiceService service,
        CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;
        var (request, pdf, problem) = await ReadAsync(context, cancellationToken).ConfigureAwait(false);
        if (problem is not null)
        {
            return problem;
        }

        var stored = await service.BookAsync(
            user.OrganizationId,
            user.Id,
            request!.CanonicalInvoice,
            pdf!,
            request.SourceFileName,
            request.ProviderModel ?? "onbekend",
            request.InvoiceId,
            request.ExtractionId,
            cancellationToken).ConfigureAwait(false);

        return Results.Ok(ToResponse(stored));
    }

    private static async Task<IResult> SaveDraftAsync(
        HttpContext context,
        BookInvoiceService service,
        CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;
        var (request, pdf, problem) = await ReadAsync(context, cancellationToken).ConfigureAwait(false);
        if (problem is not null)
        {
            return problem;
        }

        var stored = await service.SaveDraftAsync(
            user.OrganizationId,
            user.Id,
            request!.CanonicalInvoice,
            pdf!,
            request.SourceFileName,
            request.ProviderModel ?? "onbekend",
            request.InvoiceId,
            cancellationToken).ConfigureAwait(false);

        return stored is null
            ? Results.Problem(
                detail: "Deze factuur is al geboekt. Boek hem opnieuw om je wijzigingen te bewaren.",
                title: "Al geboekt",
                statusCode: StatusCodes.Status409Conflict)
            : Results.Ok(ToResponse(stored));
    }

    private static async Task<IResult> DeleteAsync(
        string invoiceId,
        HttpContext context,
        BookInvoiceService service,
        CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;
        return await service.DeleteAsync(user.OrganizationId, invoiceId, cancellationToken).ConfigureAwait(false)
            ? Results.NoContent()
            : Results.NotFound();
    }

    /// <summary>
    /// The body is read here rather than bound as a parameter. Minimal APIs
    /// bind arguments before endpoint filters run, so a bound parameter would
    /// let an anonymous caller get body-validation feedback (400) before the
    /// login check ever rejected them (401).
    /// </summary>
    private static async Task<(BookInvoiceRequest? Request, byte[]? Pdf, IResult? Problem)> ReadAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        BookInvoiceRequest? request;
        try
        {
            request = await context.Request
                .ReadFromJsonAsync<BookInvoiceRequest>(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (System.Text.Json.JsonException)
        {
            request = null;
        }

        if (request is null)
        {
            return (null, null, Results.Problem(
                detail: "De aanvraag mist verplichte velden.",
                title: "Ongeldige aanvraag",
                statusCode: StatusCodes.Status400BadRequest));
        }

        try
        {
            return (request, Convert.FromBase64String(request.DocumentBase64), null);
        }
        catch (FormatException)
        {
            return (null, null, Results.Problem(
                detail: "Het document is geen geldige base64.",
                title: "Ongeldig document",
                statusCode: StatusCodes.Status400BadRequest));
        }
    }

    private static async Task<IResult> ListAsync(
        HttpContext context,
        BookInvoiceService service,
        CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;
        var invoices = await service.ListAsync(user.OrganizationId, cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(invoices.Select(ToResponse));
    }

    private static async Task<IResult> GetAsync(
        string invoiceId,
        HttpContext context,
        BookInvoiceService service,
        CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;
        var invoice = await service.GetAsync(user.OrganizationId, invoiceId, cancellationToken)
            .ConfigureAwait(false);

        return invoice is null ? Results.NotFound() : Results.Ok(ToResponse(invoice));
    }

    private static async Task<IResult> GetDocumentAsync(
        string invoiceId,
        HttpContext context,
        BookInvoiceService service,
        CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;
        var pdf = await service.GetDocumentAsync(user.OrganizationId, invoiceId, cancellationToken)
            .ConfigureAwait(false);

        return pdf is null ? Results.NotFound() : Results.File(pdf, "application/pdf");
    }

    private static StoredInvoiceResponse ToResponse(StoredInvoice invoice) => new()
    {
        InvoiceId = invoice.InvoiceId,
        Status = invoice.Status.ToString(),
        CanonicalInvoice = invoice.Invoice,
        SourceFileName = invoice.SourceFileName,
        ProviderModel = invoice.ProviderModel,
        BookedAt = invoice.BookedAt,
    };
}
