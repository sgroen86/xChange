using System.Text.Json.Serialization;
using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Contracts.Invoices;

public sealed record BookInvoiceRequest
{
    /// <summary>Supply to re-book a correction; omit for a new invoice.</summary>
    [JsonPropertyName("invoiceId")]
    public string? InvoiceId { get; init; }

    [JsonPropertyName("canonicalInvoice")]
    public required CanonicalInvoiceDraft CanonicalInvoice { get; init; }

    /// <summary>Base64 of the source PDF.</summary>
    [JsonPropertyName("documentBase64")]
    public required string DocumentBase64 { get; init; }

    [JsonPropertyName("sourceFileName")]
    public required string SourceFileName { get; init; }

    [JsonPropertyName("providerModel")]
    public string? ProviderModel { get; init; }
}

public sealed record StoredInvoiceResponse
{
    [JsonPropertyName("invoiceId")]
    public required string InvoiceId { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("canonicalInvoice")]
    public required CanonicalInvoiceDraft CanonicalInvoice { get; init; }

    [JsonPropertyName("sourceFileName")]
    public string? SourceFileName { get; init; }

    [JsonPropertyName("providerModel")]
    public string? ProviderModel { get; init; }

    [JsonPropertyName("bookedAt")]
    public DateTimeOffset? BookedAt { get; init; }
}
