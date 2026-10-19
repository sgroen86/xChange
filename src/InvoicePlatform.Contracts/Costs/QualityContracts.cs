using System.Text.Json.Serialization;

namespace InvoicePlatform.Contracts.Costs;

/// <summary>Fields counted and how they came out. Whole numbers.</summary>
public sealed record OutcomeCountsResponse
{
    [JsonPropertyName("counted")]
    public long Counted { get; init; }

    [JsonPropertyName("correct")]
    public long Correct { get; init; }

    /// <summary>The model had a value; the reviewer booked another.</summary>
    [JsonPropertyName("corrected")]
    public long Corrected { get; init; }

    /// <summary>The model left it empty; the reviewer filled it.</summary>
    [JsonPropertyName("missed")]
    public long Missed { get; init; }

    /// <summary>The model filled it; the reviewer emptied it.</summary>
    [JsonPropertyName("extra")]
    public long Extra { get; init; }
}

/// <summary>One measure over the invoices measured. Percentages travel as strings, like amounts.</summary>
public sealed record QualityCountsResponse
{
    [JsonPropertyName("invoices")]
    public long Invoices { get; init; }

    [JsonPropertyName("perfectInvoices")]
    public long PerfectInvoices { get; init; }

    [JsonPropertyName("fields")]
    public required OutcomeCountsResponse Fields { get; init; }

    /// <summary>Correct per counted field, 0-100 to one decimal; null when nothing was counted.</summary>
    [JsonPropertyName("fieldAccuracyPercent")]
    public decimal? FieldAccuracyPercent { get; init; }

    /// <summary>Invoices without a wrong field, 0-100 to one decimal; null when none was measured.</summary>
    [JsonPropertyName("perfectPercent")]
    public decimal? PerfectPercent { get; init; }
}

/// <summary>Both measures: the model's own reading, and what the reviewer was shown.</summary>
public sealed record QualitySumsResponse
{
    /// <summary>The model's own reading; learning does not count.</summary>
    [JsonPropertyName("read")]
    public required QualityCountsResponse Read { get; init; }

    /// <summary>What the reviewer was shown: learned corrections applied, empty fields filled.</summary>
    [JsonPropertyName("shown")]
    public required QualityCountsResponse Shown { get; init; }
}

/// <summary>A model or an organisation.</summary>
public sealed record QualityRowResponse
{
    [JsonPropertyName("key")]
    public required string Key { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("sums")]
    public required QualitySumsResponse Sums { get; init; }

    /// <summary>A model's cost that month, USD.</summary>
    [JsonPropertyName("costUsd")]
    public decimal? CostUsd { get; init; }

    /// <summary>A model's cost that month per invoice it read perfectly; null when none.</summary>
    [JsonPropertyName("costPerPerfectInvoiceUsd")]
    public decimal? CostPerPerfectInvoiceUsd { get; init; }
}

public sealed record FieldQualityResponse
{
    /// <summary>A field in any invoice, e.g. "seller.vatIdentifier" or "lines[].netAmount".</summary>
    [JsonPropertyName("field")]
    public required string Field { get; init; }

    [JsonPropertyName("read")]
    public required OutcomeCountsResponse Read { get; init; }

    [JsonPropertyName("shown")]
    public required OutcomeCountsResponse Shown { get; init; }
}

/// <summary>How well invoices booked in a month were read, for the platform administrator.</summary>
public sealed record PlatformQualityResponse
{
    /// <summary>yyyy-MM (UTC).</summary>
    [JsonPropertyName("month")]
    public required string Month { get; init; }

    [JsonPropertyName("total")]
    public required QualitySumsResponse Total { get; init; }

    /// <summary>All model cost of the month, booked or not.</summary>
    [JsonPropertyName("costUsd")]
    public decimal CostUsd { get; init; }

    [JsonPropertyName("costPerPerfectInvoiceUsd")]
    public decimal? CostPerPerfectInvoiceUsd { get; init; }

    [JsonPropertyName("models")]
    public required IReadOnlyList<QualityRowResponse> Models { get; init; }

    [JsonPropertyName("organizations")]
    public required IReadOnlyList<QualityRowResponse> Organizations { get; init; }

    /// <summary>Fields that went wrong at least once, most often wrong first.</summary>
    [JsonPropertyName("fields")]
    public required IReadOnlyList<FieldQualityResponse> Fields { get; init; }
}
