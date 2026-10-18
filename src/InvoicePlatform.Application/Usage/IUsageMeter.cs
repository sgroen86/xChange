namespace InvoicePlatform.Application.Usage;

/// <summary>
/// How many conversions (PDFs read) an organisation has used, and how many it
/// may use. Tenant-owned: kept and read per organisation. An organisation
/// without a limit (the platform's own, and those the platform administrator
/// creates) is never stopped; a self-signed-up one starts on a trial limit.
/// </summary>
public interface IUsageMeter
{
    /// <summary>
    /// Counts one conversion if the organisation is under its limit; atomic,
    /// so two uploads at once cannot both take the last one.
    /// </summary>
    /// <returns>False when the limit is reached; nothing is counted then.</returns>
    Task<bool> TryConsumeAsync(string organizationId, CancellationToken cancellationToken = default);

    /// <summary>Gives a conversion back, when reading the PDF failed after it was counted.</summary>
    Task RefundAsync(string organizationId, CancellationToken cancellationToken = default);

    Task<UsageSummary> GetAsync(string organizationId, CancellationToken cancellationToken = default);

    /// <param name="limit">Null removes the limit.</param>
    Task SetLimitAsync(string organizationId, int? limit, CancellationToken cancellationToken = default);
}

/// <param name="Limit">Null: no limit.</param>
public sealed record UsageSummary(int Used, int? Limit)
{
    public int? Remaining => Limit is { } limit ? Math.Max(0, limit - Used) : null;
}

/// <summary>Reading a PDF was refused because the organisation used all its conversions.</summary>
public sealed class ConversionLimitReachedException()
    : Exception("Je gratis conversies zijn op. Kies een abonnement of koop tegoed om verder te gaan.");
