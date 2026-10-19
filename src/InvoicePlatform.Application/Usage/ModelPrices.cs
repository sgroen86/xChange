using InvoicePlatform.Application.Interpretation;

namespace InvoicePlatform.Application.Usage;

/// <param name="InputPerMillion">USD per million input tokens.</param>
/// <param name="OutputPerMillion">USD per million output tokens.</param>
public sealed record ModelPrice(decimal InputPerMillion, decimal OutputPerMillion)
{
    /// <summary>Writing to the prompt cache costs a quarter more than plain input.</summary>
    public decimal CacheWritePerMillion => InputPerMillion * 1.25m;

    /// <summary>Reading from the prompt cache costs a tenth of plain input.</summary>
    public decimal CacheReadPerMillion => InputPerMillion * 0.1m;
}

/// <param name="CostUsd">Rounded to <see cref="ModelPrices.Decimals"/> decimals; 0 when the price is unknown.</param>
/// <param name="PriceKnown">False when the model has no price in this version of the table.</param>
public sealed record ModelCost(decimal CostUsd, bool PriceKnown, string PriceVersion);

/// <summary>
/// What a model call costs, in USD, per version of the price list. The cost of
/// a call is computed once, when it is recorded, with the version in force
/// then: a later price change never rewrites history. A new price list is a
/// new <see cref="Version"/>.
///
/// Deterministic and decimal throughout (CLAUDE.md, hard rules 2 and 3).
/// </summary>
public static class ModelPrices
{
    public const string Version = "2026-10-18";

    /// <summary>Costs are kept to the micro-dollar.</summary>
    public const int Decimals = 6;

    private const decimal Million = 1_000_000m;

    private static readonly IReadOnlyDictionary<string, ModelPrice> Prices = new Dictionary<string, ModelPrice>(StringComparer.Ordinal)
    {
        ["claude-opus-5"] = new(5m, 25m),
        ["claude-opus-5-5"] = new(4m, 20m),
        ["claude-sonnet-5-5"] = new(2m, 10m),
        ["claude-haiku-5-5"] = new(0.10m, 0.50m),
    };

    public static ModelPrice? Find(string model) => Prices.GetValueOrDefault(model);

    /// <summary>
    /// The cost of one call. A model without a price costs 0 here and says so,
    /// rather than failing: the customer's extraction matters more than our
    /// bookkeeping, and the unpriced event stays visible.
    /// </summary>
    public static ModelCost Cost(string model, TokenUsage usage)
    {
        if (Find(model) is not { } price)
        {
            return new ModelCost(0m, false, Version);
        }

        var cost = (usage.InputTokens * price.InputPerMillion
            + usage.OutputTokens * price.OutputPerMillion
            + usage.CacheWriteTokens * price.CacheWritePerMillion
            + usage.CacheReadTokens * price.CacheReadPerMillion) / Million;

        return new ModelCost(Math.Round(cost, Decimals, MidpointRounding.AwayFromZero), true, Version);
    }
}
