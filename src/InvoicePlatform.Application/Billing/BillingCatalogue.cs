namespace InvoicePlatform.Application.Billing;

/// <summary>A subscription plan: what it gives, and the Stripe price it is sold under.</summary>
/// <param name="Id">Stable id, used by the API and the screen: "starter", "pro", "business".</param>
/// <param name="LookupKey">The Stripe price's lookup_key. The amount is Stripe's, never ours.</param>
public sealed record BillingPlan(string Id, string Name, int MonthlyConversions, string LookupKey);

/// <summary>A one-off purchase of credits.</summary>
public sealed record CreditPack(int Credits, string LookupKey);

/// <summary>
/// What xChange sells. Conversions and lookup keys only: prices are looked up
/// in Stripe by lookup_key at runtime, so a price change there needs no deploy.
/// </summary>
public static class BillingCatalogue
{
    public static readonly BillingPlan Starter = new("starter", "Starter", 50, "xchange_starter_monthly");

    public static readonly BillingPlan Pro = new("pro", "Pro", 250, "xchange_pro_monthly");

    public static readonly BillingPlan Business = new("business", "Business", 1000, "xchange_business_monthly");

    public static readonly CreditPack Credits = new(20, "xchange_credits_20");

    public static IReadOnlyList<BillingPlan> Plans { get; } = [Starter, Pro, Business];

    /// <summary>Every lookup key, plans first.</summary>
    public static IReadOnlyList<string> LookupKeys { get; } = [.. Plans.Select(p => p.LookupKey), Credits.LookupKey];

    public static BillingPlan? FindPlan(string? id)
        => Plans.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

    public static BillingPlan? FindPlanByLookupKey(string? lookupKey)
        => Plans.FirstOrDefault(p => p.LookupKey == lookupKey);
}
