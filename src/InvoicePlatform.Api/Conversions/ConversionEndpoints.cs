using InvoicePlatform.Api.Identity;
using InvoicePlatform.Api.Invoices;
using InvoicePlatform.Application.Conversions;
using InvoicePlatform.Application.Invoices;
using InvoicePlatform.Contracts.Conversions;

namespace InvoicePlatform.Api.Conversions;

/// <summary>
/// The conversion API: another system posts a PDF with an API key, polls the
/// conversion, and fetches the e-invoice. HTTP only - the work is in
/// <see cref="ConversionService"/>, and the organisation comes from the key.
/// </summary>
public static class ConversionEndpoints
{
    public const string FileNameHeader = "X-File-Name";

    private const int ReadBufferBytes = 81920;

    public static RouteGroupBuilder MapConversionEndpoints(this RouteGroupBuilder group)
    {
        // Guards per endpoint, never on the group (see Program.cs). These
        // accept an API key and nothing else; a key opens nothing but these.
        group.MapPost("/conversions", SubmitAsync)
            .WithName("SubmitConversion")
            .DisableAntiforgery()
            .Accepts<byte[]>("application/pdf")
            .RequireApiKey();

        group.MapGet("/conversions/{conversionId}", GetAsync)
            .WithName("GetConversion")
            .RequireApiKey();

        group.MapGet("/conversions/{conversionId}/xml", XmlAsync)
            .WithName("GetConversionXml")
            .RequireApiKey();

        return group;
    }

    private static async Task<IResult> SubmitAsync(
        HttpRequest request,
        string? format,
        ConversionService conversions,
        CancellationToken cancellationToken)
    {
        var caller = request.HttpContext.GetApiKeyCaller()!;

        var (pdf, problem) = await ReadPdfAsync(request, cancellationToken).ConfigureAwait(false);
        if (problem is not null)
        {
            return problem;
        }

        var submission = await conversions.SubmitAsync(caller, format, FileName(request), pdf!, cancellationToken).ConfigureAwait(false);
        if (!submission.FormatFound)
        {
            return Results.Problem(detail: $"Formaat '{format}' bestaat niet.", title: "Onbekend formaat", statusCode: StatusCodes.Status400BadRequest);
        }

        var conversion = submission.Conversion!;
        return Results.Accepted($"/api/v1/conversions/{conversion.ConversionId}", ToResponse(conversion));
    }

    private static async Task<IResult> GetAsync(
        string conversionId,
        HttpContext context,
        ConversionService conversions,
        CancellationToken cancellationToken)
    {
        var caller = context.GetApiKeyCaller()!;
        var conversion = await conversions.GetAsync(caller.OrganizationId, conversionId, cancellationToken).ConfigureAwait(false);
        return conversion is null ? Results.NotFound() : Results.Ok(ToResponse(conversion));
    }

    private static async Task<IResult> XmlAsync(
        string conversionId,
        HttpContext context,
        ConversionService conversions,
        CancellationToken cancellationToken)
    {
        var caller = context.GetApiKeyCaller()!;
        var file = await conversions.GetFileAsync(caller.OrganizationId, conversionId, cancellationToken).ConfigureAwait(false);

        return file.Kind switch
        {
            ConversionFileKind.NotFound => Results.NotFound(),
            ConversionFileKind.NotReady => Results.Problem(
                detail: "De conversie is nog niet klaar. Vraag de status op tot die 'ready' is.",
                title: "Nog niet klaar",
                statusCode: StatusCodes.Status409Conflict),
            ConversionFileKind.Refused when file.Export!.Kind == ExportOutcomeKind.UnknownRuleset => Results.Problem(
                detail: "Het formaat van deze conversie bestaat niet meer.",
                title: "Onbekend formaat",
                statusCode: StatusCodes.Status409Conflict),
            // The file, the trial rule (402) and the official check exactly as the app's export.
            _ => ExportEndpoints.ToResult(context, file.Export!, null),
        };
    }

    /// <summary>
    /// The body is the PDF itself, with the same checks an upload gets: not
    /// empty, within the 4 MB a Function URL lets through, and really a PDF.
    /// </summary>
    private static async Task<(byte[]? Pdf, IResult? Problem)> ReadPdfAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var max = ExtractInvoiceEndpoint.MaxUploadBytes;
        if (request.ContentLength > max)
        {
            return (null, TooLarge(request.ContentLength.Value));
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[ReadBufferBytes];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > max)
            {
                return (null, TooLarge(buffer.Length));
            }
        }

        if (buffer.Length == 0)
        {
            return (null, Results.Problem(detail: "De body is leeg. Stuur de PDF als body, met Content-Type application/pdf.",
                title: "Leeg bestand", statusCode: StatusCodes.Status400BadRequest));
        }

        var bytes = buffer.ToArray();
        if (!ExtractInvoiceEndpoint.IsPdf(bytes))
        {
            return (null, Results.Problem(detail: "Alleen PDF-bestanden worden geaccepteerd. Stuur de PDF zelf als body, niet als formulier.",
                title: "Geen PDF", statusCode: StatusCodes.Status400BadRequest));
        }

        return (bytes, null);
    }

    private static IResult TooLarge(long size)
        => Results.Problem(
            detail: $"Het bestand is {size} bytes. Het maximum is {ExtractInvoiceEndpoint.MaxUploadBytes} bytes.",
            title: "Bestand te groot",
            statusCode: StatusCodes.Status413PayloadTooLarge);

    private static string FileName(HttpRequest request)
    {
        var name = request.Headers[FileNameHeader].ToString();
        name = string.IsNullOrWhiteSpace(name) ? "" : Path.GetFileName(name.Trim());
        return name.Length == 0 ? "document.pdf" : name;
    }

    private static ConversionResponse ToResponse(Conversion conversion) => new()
    {
        ConversionId = conversion.ConversionId,
        Status = conversion.Status switch
        {
            ConversionStatus.Queued => "queued",
            ConversionStatus.Processing => "processing",
            ConversionStatus.Ready => "ready",
            ConversionStatus.NeedsReview => "needs_review",
            _ => "failed",
        },
        Format = conversion.Format,
        InvoiceNumber = conversion.InvoiceNumber,
        Violations = conversion.Violations.Count > 0 ? ExportEndpoints.ToResponse(conversion.Violations).Violations : null,
        Error = conversion.Error,
    };
}
