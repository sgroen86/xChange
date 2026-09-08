using InvoicePlatform.Application.Invoices;
using InvoicePlatform.Contracts.Invoices;
using InvoicePlatform.Domain.Validation;
using InvoicePlatform.Infrastructure.Anthropic;

namespace InvoicePlatform.Api.Invoices;

/// <summary>
/// HTTP concerns only: reading the multipart body, enforcing upload limits,
/// mapping the result to the wire contract and choosing status codes. All
/// behaviour lives in <see cref="ExtractInvoiceService"/> (CLAUDE.md).
/// </summary>
public static class ExtractInvoiceEndpoint
{
    public const long MaxUploadBytes = 20 * 1024 * 1024;

    private const string FormFieldName = "file";

    /// <summary>%PDF- as bytes. Extension and content type are both trivially spoofed.</summary>
    private static ReadOnlySpan<byte> PdfMagic => "%PDF-"u8;

    public static RouteGroupBuilder MapInvoiceEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/invoices/extract", ExtractAsync)
            .WithName("ExtractInvoice")
            .WithSummary("Extract canonical invoice data from a PDF.")
            .DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<ExtractInvoiceResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        return group;
    }

    private static async Task<IResult> ExtractAsync(
        HttpRequest request,
        ExtractInvoiceService service,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(ExtractInvoiceEndpoint));

        if (!request.HasFormContentType)
        {
            return Problem(StatusCodes.Status400BadRequest,
                "Unsupported content type",
                "The request must be multipart/form-data.");
        }

        var form = await request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        var file = form.Files[FormFieldName];

        if (file is null)
        {
            return Problem(StatusCodes.Status400BadRequest,
                "Missing file",
                $"The request must contain a form field named \"{FormFieldName}\".");
        }

        if (file.Length == 0)
        {
            return Problem(StatusCodes.Status400BadRequest, "Empty file", "The uploaded file is empty.");
        }

        if (file.Length > MaxUploadBytes)
        {
            return Problem(StatusCodes.Status413PayloadTooLarge,
                "File too large",
                $"The file is {file.Length} bytes. The maximum is {MaxUploadBytes} bytes (20 MB).");
        }

        using var buffer = new MemoryStream(capacity: (int)file.Length);
        await using (var upload = file.OpenReadStream())
        {
            await upload.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        var bytes = buffer.ToArray();

        if (!IsPdf(bytes))
        {
            return Problem(StatusCodes.Status400BadRequest,
                "Not a PDF",
                "Only PDF files are accepted. The uploaded file does not start with a PDF header.");
        }

        try
        {
            var result = await service.ExtractAsync(bytes, cancellationToken).ConfigureAwait(false);
            return Results.Ok(ToResponse(result));
        }
        catch (InvoiceInterpretationException exception)
        {
            logger.LogWarning(exception, "Invoice interpretation failed");
            return Problem(StatusCodes.Status502BadGateway,
                "Extraction failed",
                exception.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller went away; 499 is nginx's "client closed request".
            return Results.StatusCode(499);
        }
    }

    private static bool IsPdf(ReadOnlySpan<byte> content)
        => content.Length >= PdfMagic.Length && content[..PdfMagic.Length].SequenceEqual(PdfMagic);

    private static ExtractInvoiceResponse ToResponse(ExtractInvoiceResult result) => new()
    {
        InvoiceId = result.InvoiceId,
        CanonicalInvoice = result.Draft,
        CanonicalXml = result.CanonicalXml,
        Warnings = [.. result.Warnings.Select(ToWarning)],
        ProviderExecution = new ProviderExecutionResponse
        {
            Provider = result.Execution.Provider,
            Model = result.Execution.Model,
            DurationMs = result.Execution.DurationMs,
        },
    };

    private static ExtractInvoiceWarning ToWarning(ValidationWarning warning) => new()
    {
        Code = warning.Code,
        Path = warning.Path,
        Message = warning.Message,
        Severity = warning.Severity == ValidationSeverity.Error ? "error" : "warning",
        ExtractedValue = warning.ExtractedValue,
        CalculatedValue = warning.CalculatedValue,
    };

    private static IResult Problem(int statusCode, string title, string detail)
        => Results.Problem(detail: detail, title: title, statusCode: statusCode);
}
