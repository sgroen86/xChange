using System.Text.Json.Serialization;

namespace InvoicePlatform.Contracts.Costs;

/// <summary>
/// Running sums of model calls. Amounts are USD and travel as strings
/// (CLAUDE.md, hard rule 3); token counts are whole numbers.
/// </summary>
public sealed record CostTotalsResponse
{
    [JsonPropertyName("calls")]
    public long Calls { get; init; }

    [JsonPropertyName("failedCalls")]
    public long FailedCalls { get; init; }

    [JsonPropertyName("inputTokens")]
    public long InputTokens { get; init; }

    [JsonPropertyName("outputTokens")]
    public long OutputTokens { get; init; }

    [JsonPropertyName("cacheWriteTokens")]
    public long CacheWriteTokens { get; init; }

    [JsonPropertyName("cacheReadTokens")]
    public long CacheReadTokens { get; init; }

    [JsonPropertyName("costUsd")]
    public decimal CostUsd { get; init; }

    [JsonPropertyName("averageCostUsd")]
    public decimal AverageCostUsd { get; init; }
}

/// <summary>A day, a model or an organisation, with its sums.</summary>
public sealed record CostRowResponse
{
    /// <summary>yyyy-MM-dd, a model id, or an organisation id.</summary>
    [JsonPropertyName("key")]
    public required string Key { get; init; }

    /// <summary>The organisation's name, for an organisation row.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("totals")]
    public required CostTotalsResponse Totals { get; init; }
}

/// <summary>The platform's model cost for a month.</summary>
public sealed record PlatformCostsResponse
{
    /// <summary>yyyy-MM (UTC).</summary>
    [JsonPropertyName("month")]
    public required string Month { get; init; }

    /// <summary>The price list new calls are costed with; each call keeps the one it was recorded with.</summary>
    [JsonPropertyName("priceVersion")]
    public required string PriceVersion { get; init; }

    [JsonPropertyName("total")]
    public required CostTotalsResponse Total { get; init; }

    [JsonPropertyName("days")]
    public required IReadOnlyList<CostRowResponse> Days { get; init; }

    [JsonPropertyName("models")]
    public required IReadOnlyList<CostRowResponse> Models { get; init; }

    [JsonPropertyName("organizations")]
    public required IReadOnlyList<CostRowResponse> Organizations { get; init; }
}

/// <summary>What one user (or "email") of an organisation cost in a month.</summary>
public sealed record CostActorResponse
{
    /// <summary>A user id, "email", or "apikey:&lt;id&gt;".</summary>
    [JsonPropertyName("actor")]
    public required string Actor { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("totals")]
    public required CostTotalsResponse Totals { get; init; }
}

/// <summary>One model call.</summary>
public sealed record CostEventResponse
{
    [JsonPropertyName("eventId")]
    public required string EventId { get; init; }

    [JsonPropertyName("at")]
    public DateTimeOffset At { get; init; }

    [JsonPropertyName("actor")]
    public required string Actor { get; init; }

    [JsonPropertyName("actorName")]
    public string? ActorName { get; init; }

    /// <summary>upload, batch or email.</summary>
    [JsonPropertyName("source")]
    public required string Source { get; init; }

    /// <summary>The extraction, not an invoice: there is no invoice page for it.</summary>
    [JsonPropertyName("extractionId")]
    public required string ExtractionId { get; init; }

    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("inputTokens")]
    public long InputTokens { get; init; }

    [JsonPropertyName("outputTokens")]
    public long OutputTokens { get; init; }

    [JsonPropertyName("cacheWriteTokens")]
    public long CacheWriteTokens { get; init; }

    [JsonPropertyName("cacheReadTokens")]
    public long CacheReadTokens { get; init; }

    [JsonPropertyName("costUsd")]
    public decimal CostUsd { get; init; }

    [JsonPropertyName("priceVersion")]
    public required string PriceVersion { get; init; }

    /// <summary>False: the model had no price, and the call is counted at 0.</summary>
    [JsonPropertyName("priceKnown")]
    public bool PriceKnown { get; init; }

    [JsonPropertyName("succeeded")]
    public bool Succeeded { get; init; }
}

/// <summary>One organisation's model cost for a month.</summary>
public sealed record OrganizationCostsResponse
{
    [JsonPropertyName("month")]
    public required string Month { get; init; }

    [JsonPropertyName("organizationId")]
    public required string OrganizationId { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("total")]
    public required CostTotalsResponse Total { get; init; }

    [JsonPropertyName("users")]
    public required IReadOnlyList<CostActorResponse> Users { get; init; }

    [JsonPropertyName("models")]
    public required IReadOnlyList<CostRowResponse> Models { get; init; }

    /// <summary>The most expensive calls, most expensive first.</summary>
    [JsonPropertyName("topEvents")]
    public required IReadOnlyList<CostEventResponse> TopEvents { get; init; }
}
