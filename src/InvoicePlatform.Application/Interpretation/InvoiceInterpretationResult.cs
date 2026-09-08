using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Application.Interpretation;

/// <summary>What an interpreter produced, plus how it was produced.</summary>
/// <param name="Draft">The interpreted invoice. Never null; fields inside may be.</param>
/// <param name="Execution">Provider, model and timing for this interpretation.</param>
public sealed record InvoiceInterpretationResult(
    CanonicalInvoiceDraft Draft,
    ProviderExecution Execution);

/// <summary>
/// Which provider ran, and for how long. Deliberately free of SDK types so that
/// provider details never leak past Infrastructure.
/// </summary>
/// <param name="Provider">Stable provider slug, e.g. "anthropic".</param>
/// <param name="Model">Model identifier the provider was asked for.</param>
/// <param name="DurationMs">Wall-clock duration of the provider call.</param>
public sealed record ProviderExecution(string Provider, string Model, long DurationMs);
