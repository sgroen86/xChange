namespace InvoicePlatform.Application.Usage;

/// <summary>What an organisation has left in each bucket.</summary>
/// <param name="PeriodAllowance">The subscription's conversions this period; null without a plan.</param>
/// <param name="Used">Counted against the trial or limit.</param>
/// <param name="Limit">The trial or a set limit; null: none.</param>
/// <param name="SubscribedOnce">Ever had a plan; ends "no limit" for good.</param>
public sealed record ConversionBalance(
    int? PeriodAllowance,
    int PeriodUsed,
    int Credits,
    int Used,
    int? Limit,
    bool SubscribedOnce);

/// <summary>
/// Which bucket a conversion comes from. The order is the product rule: the
/// month's conversions first (they expire), then purchased credits (they do
/// not), then the trial or limit as before billing. An organisation without a
/// limit is unlimited only until it subscribes.
///
/// The usage meter applies the same rules as one conditional update per
/// bucket, in this order, so the check and the count cannot be separated.
/// </summary>
public static class ConversionRules
{
    public static IReadOnlyList<ConversionBucket> Order { get; } =
        [ConversionBucket.Subscription, ConversionBucket.Credits, ConversionBucket.Base];

    public static bool HasOne(ConversionBalance balance, ConversionBucket bucket) => bucket switch
    {
        ConversionBucket.Subscription => balance.PeriodAllowance is { } allowance && balance.PeriodUsed < allowance,
        ConversionBucket.Credits => balance.Credits > 0,
        _ => balance.Limit is { } limit ? balance.Used < limit : !balance.SubscribedOnce,
    };

    /// <returns>The first bucket with a conversion left, or null: refused.</returns>
    public static ConversionBucket? Choose(ConversionBalance balance)
    {
        foreach (var bucket in Order)
        {
            if (HasOne(balance, bucket))
            {
                return bucket;
            }
        }

        return null;
    }
}
