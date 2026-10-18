using System.Text.Json.Serialization;

namespace InvoicePlatform.Contracts.Learning;

/// <summary>Something xChange learned from a reviewer's correction, for the settings page.</summary>
public sealed record LearnedCorrectionResponse
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>"organization", or "buyer:&lt;VAT or registration number&gt;".</summary>
    [JsonPropertyName("scope")]
    public required string Scope { get; init; }

    /// <summary>A field path, or "lines.tax", "lines.unitCode", "exemptionReason:&lt;category&gt;".</summary>
    [JsonPropertyName("field")]
    public required string Field { get; init; }

    [JsonPropertyName("from")]
    public string? From { get; init; }

    [JsonPropertyName("to")]
    public string? To { get; init; }

    [JsonPropertyName("context")]
    public string? Context { get; init; }

    /// <summary>"apply" (put back by xChange) or "hint" (given to the model to read better).</summary>
    [JsonPropertyName("use")]
    public required string Use { get; init; }

    [JsonPropertyName("count")]
    public int Count { get; init; }

    [JsonPropertyName("lastSeen")]
    public DateTimeOffset LastSeen { get; init; }
}
