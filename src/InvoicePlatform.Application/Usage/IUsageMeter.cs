namespace InvoicePlatform.Application.Usage;

/// <summary>
/// How many conversions (PDFs read) an organisation has used, and how many it
/// may use. Tenant-owned: kept and read per organisation. An organisation
/// without a limit (the platform's own, and those the platform administrator
/// creates) is never stopped until it subscribes; a self-signed-up one starts
/// on a trial limit.
/// </summary>
public interface IUsageMeter
{
    /// <summary>
    /// Counts one conversion, from the first bucket that has one left: the
    /// subscription's monthly conversions, then purchased credits, then the
    /// trial or limit as before. Each step is atomic, so two uploads at once
    /// cannot both take the last one.
    /// </summary>
    /// <returns>The bucket it came from, or null when nothing is left; nothing is counted then.</returns>
    Task<ConversionBucket?> TryConsumeAsync(string organizationId, CancellationToken cancellationToken = default);

    /// <summary>Gives a conversion back to the bucket it came from, when reading the PDF failed after it was counted.</summary>
    Task RefundAsync(string organizationId, ConversionBucket bucket, CancellationToken cancellationToken = default);

    Task<UsageSummary> GetAsync(string organizationId, CancellationToken cancellationToken = default);

    /// <param name="limit">Null removes the limit.</param>
    /// <param name="trial">
    /// A trial may read and check invoices but not take the result away:
    /// downloading (and later sending) needs a plan or purchased credits.
    /// </param>
    Task SetLimitAsync(string organizationId, int? limit, bool trial = false, CancellationToken cancellationToken = default);
}

/// <summary>Where a conversion was counted, so a refund goes back to the same place.</summary>
public enum ConversionBucket
{
    /// <summary>The subscription's conversions for the current billing period.</summary>
    Subscription,

    /// <summary>Purchased credits, which never expire.</summary>
    Credits,

    /// <summary>The trial, a limit set by the platform, or no limit at all - as before billing.</summary>
    Base,
}

/// <param name="Used">Conversions counted against the trial or limit (or in total, without a limit).</param>
/// <param name="Limit">Null: no limit.</param>
/// <param name="Trial">On a trial and never paid: no downloading or sending.</param>
public sealed record UsageSummary(int Used, int? Limit, bool Trial = false)
{
    public int? Remaining => Limit is { } limit ? Math.Max(0, limit - Used) : null;
}

/// <summary>
/// Reading a PDF was refused because the organisation used all its conversions.
/// The message is for the user, in the language of the moment it is thrown.
/// </summary>
public sealed class ConversionLimitReachedException()
    : Exception(UsageText.Current.ConversionLimitReached);
