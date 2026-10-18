using System.Text.Json.Serialization;
using InvoicePlatform.Contracts.Invoices;

namespace InvoicePlatform.Contracts.Conversions;

/// <summary>A conversion as the conversion API reports it.</summary>
public sealed record ConversionResponse
{
    [JsonPropertyName("conversionId")]
    public required string ConversionId { get; init; }

    /// <summary>"queued", "processing", "ready", "needs_review" or "failed".</summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>The format the file is made in; null until one is chosen.</summary>
    [JsonPropertyName("format")]
    public string? Format { get; init; }

    [JsonPropertyName("invoiceNumber")]
    public string? InvoiceNumber { get; init; }

    /// <summary>For "needs_review": what a person has to resolve.</summary>
    [JsonPropertyName("violations")]
    public IReadOnlyList<RuleViolationResponse>? Violations { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

/// <summary>An API key as the settings page lists it. Never carries the key.</summary>
public record ApiKeyResponse
{
    [JsonPropertyName("keyId")]
    public required string KeyId { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>The first characters of the key, to recognise it by.</summary>
    [JsonPropertyName("prefix")]
    public required string Prefix { get; init; }

    [JsonPropertyName("createdAt")]
    public required DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("lastUsedAt")]
    public DateTimeOffset? LastUsedAt { get; init; }
}

/// <summary>A key just made: the only response that ever carries the key itself.</summary>
public sealed record CreatedApiKeyResponse : ApiKeyResponse
{
    [JsonPropertyName("key")]
    public required string Key { get; init; }
}

public sealed record CreateApiKeyRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }
}
