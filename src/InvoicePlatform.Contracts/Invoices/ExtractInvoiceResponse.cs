using System.Text.Json.Serialization;
using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Contracts.Invoices;

/// <summary>
/// Wire shape of POST /api/v1/invoices/extract.
///
/// Contracts references Domain because the canonical invoice is the payload,
/// and a duplicate wire copy of it would be two models to keep in step for no
/// benefit at this stage (CLAUDE.md permits the reference where it is actually
/// necessary). Note that decimals serialize as JSON numbers here; the frontend
/// must not round-trip them through a JS float where precision matters.
/// </summary>
public sealed record ExtractInvoiceResponse
{
    [JsonPropertyName("invoiceId")]
    public required string InvoiceId { get; init; }

    [JsonPropertyName("canonicalInvoice")]
    public required CanonicalInvoiceDraft CanonicalInvoice { get; init; }

    [JsonPropertyName("canonicalXml")]
    public required string CanonicalXml { get; init; }

    [JsonPropertyName("warnings")]
    public required IReadOnlyList<ExtractInvoiceWarning> Warnings { get; init; }

    [JsonPropertyName("providerExecution")]
    public required ProviderExecutionResponse ProviderExecution { get; init; }
}

public sealed record ExtractInvoiceWarning
{
    [JsonPropertyName("code")]
    public required string Code { get; init; }

    [JsonPropertyName("path")]
    public required string Path { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("severity")]
    public required string Severity { get; init; }

    [JsonPropertyName("extractedValue")]
    public decimal? ExtractedValue { get; init; }

    [JsonPropertyName("calculatedValue")]
    public decimal? CalculatedValue { get; init; }
}

public sealed record ProviderExecutionResponse
{
    [JsonPropertyName("provider")]
    public required string Provider { get; init; }

    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("durationMs")]
    public required long DurationMs { get; init; }
}
