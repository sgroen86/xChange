using System.Text.Json.Serialization;
using InvoicePlatform.Contracts.Invoices;
using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Contracts.Imports;

/// <summary>An import in the organisation's "to review" list.</summary>
public sealed record ImportResponse
{
    [JsonPropertyName("importId")]
    public required string ImportId { get; init; }

    /// <summary>"upload" or "email".</summary>
    [JsonPropertyName("source")]
    public required string Source { get; init; }

    /// <summary>"awaitingApproval" (mailed, not read until approved), "queued", "processing", "ready" or "failed".</summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("fileName")]
    public required string FileName { get; init; }

    [JsonPropertyName("byteSize")]
    public long ByteSize { get; init; }

    [JsonPropertyName("sender")]
    public string? Sender { get; init; }

    [JsonPropertyName("subject")]
    public string? Subject { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    /// <summary>Booking passes it back, so xChange learns from the corrections.</summary>
    [JsonPropertyName("extractionId")]
    public string? ExtractionId { get; init; }

    /// <summary>Only on a single import, not in the list.</summary>
    [JsonPropertyName("canonicalInvoice")]
    public CanonicalInvoiceDraft? CanonicalInvoice { get; init; }

    [JsonPropertyName("warnings")]
    public IReadOnlyList<ExtractInvoiceWarning>? Warnings { get; init; }

    /// <summary>A short summary for the list, once read.</summary>
    [JsonPropertyName("invoiceNumber")]
    public string? InvoiceNumber { get; init; }

    [JsonPropertyName("buyerName")]
    public string? BuyerName { get; init; }

    [JsonPropertyName("providerModel")]
    public string? ProviderModel { get; init; }

    [JsonPropertyName("createdAt")]
    public required DateTimeOffset CreatedAt { get; init; }
}
