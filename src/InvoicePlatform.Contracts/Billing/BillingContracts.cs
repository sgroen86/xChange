using System.Text.Json.Serialization;

namespace InvoicePlatform.Contracts.Billing;

/// <summary>The organisation's plan, conversions and credits. Amounts are not part of it.</summary>
public sealed record BillingStatusResponse
{
    /// <summary>"starter", "pro", "business"; null without a running plan.</summary>
    [JsonPropertyName("plan")]
    public string? Plan { get; init; }

    /// <summary>The subscription's status at Stripe (active, past_due, canceled, ...); null without one.</summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("periodStart")]
    public DateTimeOffset? PeriodStart { get; init; }

    [JsonPropertyName("periodEnd")]
    public DateTimeOffset? PeriodEnd { get; init; }

    /// <summary>The plan ends at the period end instead of renewing.</summary>
    [JsonPropertyName("cancelAtPeriodEnd")]
    public bool CancelAtPeriodEnd { get; init; }

    /// <summary>Conversions this period; null without a plan.</summary>
    [JsonPropertyName("periodAllowance")]
    public int? PeriodAllowance { get; init; }

    [JsonPropertyName("periodUsed")]
    public int PeriodUsed { get; init; }

    /// <summary>Purchased credits left; used after the period's conversions, never expire.</summary>
    [JsonPropertyName("credits")]
    public int Credits { get; init; }

    /// <summary>On the trial and never paid: downloading needs a plan or credits.</summary>
    [JsonPropertyName("trial")]
    public bool Trial { get; init; }

    /// <summary>The trial's (or a set limit's) conversions; null without a limit.</summary>
    [JsonPropertyName("baseLimit")]
    public int? BaseLimit { get; init; }

    [JsonPropertyName("baseUsed")]
    public int BaseUsed { get; init; }

    /// <summary>A Stripe customer exists, so the portal can open.</summary>
    [JsonPropertyName("hasCustomer")]
    public bool HasCustomer { get; init; }
}

/// <summary>A plan with its current price from Stripe.</summary>
public sealed record BillingPlanResponse
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("monthlyConversions")]
    public int MonthlyConversions { get; init; }

    /// <summary>Per month, excluding VAT, as a string ("19.00"); null when Stripe has no active price.</summary>
    [JsonPropertyName("amount")]
    public decimal? Amount { get; init; }

    [JsonPropertyName("currency")]
    public string? Currency { get; init; }
}

public sealed record BillingPlansResponse
{
    [JsonPropertyName("plans")]
    public required IReadOnlyList<BillingPlanResponse> Plans { get; init; }

    [JsonPropertyName("credits")]
    public required BillingCreditsResponse Credits { get; init; }
}

public sealed record BillingCreditsResponse
{
    /// <summary>Conversions in the pack.</summary>
    [JsonPropertyName("conversions")]
    public int Conversions { get; init; }

    /// <summary>For the whole pack, excluding VAT, as a string; null when Stripe has no active price.</summary>
    [JsonPropertyName("amount")]
    public decimal? Amount { get; init; }

    [JsonPropertyName("currency")]
    public string? Currency { get; init; }
}

/// <summary>Either a plan, or credits: true.</summary>
public sealed record CheckoutRequest
{
    [JsonPropertyName("plan")]
    public string? Plan { get; init; }

    [JsonPropertyName("credits")]
    public bool Credits { get; init; }
}

/// <summary>Where to send the browser: Stripe's checkout or portal.</summary>
public sealed record RedirectResponse
{
    [JsonPropertyName("url")]
    public required string Url { get; init; }
}
