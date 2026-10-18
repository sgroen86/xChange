using InvoicePlatform.Api.Identity;
using InvoicePlatform.Api.Invoices;
using InvoicePlatform.Application.Imports;
using InvoicePlatform.Contracts.Imports;
using InvoicePlatform.Domain.Imports;

namespace InvoicePlatform.Api.Imports;

/// <summary>
/// Batch import: PDFs are accepted at once and read in the background; the
/// list shows where each one is. HTTP only - the organisation comes from the
/// session, and an import of another organisation is simply not found.
/// </summary>
public static class ImportEndpoints
{
    public static RouteGroupBuilder MapImportEndpoints(this RouteGroupBuilder group)
    {
        // Guards per endpoint, never on the group (see Program.cs).
        // Submitting spends model credit, so read-only accounts cannot.
        group.MapPost("/imports", SubmitAsync)
            .WithName("SubmitImport")
            .DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data")
            .RequireAuthenticatedUser()
            .RequireRole(UserRoleName.Admin, UserRoleName.User);

        group.MapGet("/imports", ListAsync)
            .WithName("ListImports")
            .RequireAuthenticatedUser();

        group.MapGet("/imports/{importId}", GetAsync)
            .WithName("GetImport")
            .RequireAuthenticatedUser();

        group.MapGet("/imports/{importId}/document", DocumentAsync)
            .WithName("GetImportDocument")
            .RequireAuthenticatedUser();

        group.MapPost("/imports/{importId}/approve", ApproveAsync)
            .WithName("ApproveImport")
            .RequireAuthenticatedUser()
            .RequireRole(UserRoleName.Admin, UserRoleName.User);

        group.MapPost("/imports/{importId}/retry", RetryAsync)
            .WithName("RetryImport")
            .RequireAuthenticatedUser()
            .RequireRole(UserRoleName.Admin, UserRoleName.User);

        group.MapDelete("/imports/{importId}", DismissAsync)
            .WithName("DismissImport")
            .RequireAuthenticatedUser()
            .RequireRole(UserRoleName.Admin, UserRoleName.User);

        return group;
    }

    private static async Task<IResult> SubmitAsync(HttpRequest request, ImportService imports, CancellationToken cancellationToken)
    {
        var upload = await ExtractInvoiceEndpoint.ReadPdfAsync(request, cancellationToken).ConfigureAwait(false);
        if (upload.Problem is not null)
        {
            return upload.Problem;
        }

        var user = request.HttpContext.GetAuthenticatedUser()!;
        var record = await imports.SubmitAsync(
            user.OrganizationId, ImportSource.Upload, upload.FileName, upload.Pdf!, user.Id, cancellationToken: cancellationToken).ConfigureAwait(false);

        return Results.Accepted($"/api/v1/imports/{record.ImportId}", ToResponse(record, detail: false));
    }

    private static async Task<IResult> ListAsync(HttpContext context, ImportService imports, CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;
        var records = await imports.ListAsync(user.OrganizationId, cancellationToken).ConfigureAwait(false);
        return Results.Ok(records.Select(r => ToResponse(r, detail: false)));
    }

    private static async Task<IResult> GetAsync(string importId, HttpContext context, ImportService imports, CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;
        var record = await imports.GetAsync(user.OrganizationId, importId, cancellationToken).ConfigureAwait(false);
        return record is null ? Results.NotFound() : Results.Ok(ToResponse(record, detail: true));
    }

    private static async Task<IResult> DocumentAsync(string importId, HttpContext context, ImportService imports, CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;
        var pdf = await imports.GetDocumentAsync(user.OrganizationId, importId, cancellationToken).ConfigureAwait(false);
        return pdf is null ? Results.NotFound() : Results.File(pdf, "application/pdf");
    }

    private static async Task<IResult> ApproveAsync(string importId, HttpContext context, ImportService imports, CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;
        return await imports.ApproveAsync(user.OrganizationId, importId, cancellationToken).ConfigureAwait(false)
            ? Results.Accepted()
            : Results.NotFound();
    }

    private static async Task<IResult> RetryAsync(string importId, HttpContext context, ImportService imports, CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;
        return await imports.RetryAsync(user.OrganizationId, importId, cancellationToken).ConfigureAwait(false)
            ? Results.Accepted()
            : Results.NotFound();
    }

    private static async Task<IResult> DismissAsync(string importId, HttpContext context, ImportService imports, CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;
        return await imports.DismissAsync(user.OrganizationId, importId, cancellationToken).ConfigureAwait(false)
            ? Results.NoContent()
            : Results.NotFound();
    }

    private static ImportResponse ToResponse(ImportRecord record, bool detail) => new()
    {
        ImportId = record.ImportId,
        Source = record.Source switch
        {
            ImportSource.Email => "email",
            ImportSource.Api => "api",
            _ => "upload",
        },
        Status = record.Status == ImportStatus.AwaitingApproval ? "awaitingApproval" : record.Status.ToString().ToLowerInvariant(),
        FileName = record.FileName,
        ByteSize = record.ByteSize,
        Sender = record.Sender,
        Subject = record.Subject,
        Error = record.Error,
        ExtractionId = record.ExtractionId,
        CanonicalInvoice = detail ? record.Draft : null,
        Warnings = detail ? [.. record.Warnings.Select(ExtractInvoiceEndpoint.ToWarning)] : null,
        InvoiceNumber = record.Draft?.InvoiceNumber,
        BuyerName = record.Draft?.Buyer?.Name,
        ProviderModel = record.ProviderModel,
        CreatedAt = record.CreatedAt,
    };
}
