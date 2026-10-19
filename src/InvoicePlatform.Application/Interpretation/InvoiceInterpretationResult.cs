using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Application.Interpretation;

/// <summary>What an interpreter produced, plus how it was produced.</summary>
/// <param name="Draft">The interpreted invoice. Never null; fields inside may be.</param>
/// <param name="Execution">Provider, model, timing and tokens for this interpretation.</param>
public sealed record InvoiceInterpretationResult(
    CanonicalInvoiceDraft Draft,
    ProviderExecution Execution);

/// <summary>
/// Which provider ran, for how long, and how many tokens it was paid for.
/// Deliberately free of SDK types so that provider details never leak past
/// Infrastructure.
/// </summary>
/// <param name="Provider">Stable provider slug, e.g. "anthropic".</param>
/// <param name="Model">Model identifier the provider was asked for.</param>
/// <param name="DurationMs">Wall-clock duration of the provider call.</param>
/// <param name="Usage">The tokens the provider reported for this call.</param>
public sealed record ProviderExecution(string Provider, string Model, long DurationMs, TokenUsage Usage);

/// <summary>
/// Tokens of one model call, per kind, as the provider reported them. Each kind
/// has its own price (see <c>ModelPrices</c>).
/// </summary>
/// <param name="InputTokens">Input not read from or written to the prompt cache.</param>
/// <param name="OutputTokens">Generated tokens.</param>
/// <param name="CacheWriteTokens">Input written to the prompt cache.</param>
/// <param name="CacheReadTokens">Input read from the prompt cache.</param>
public sealed record TokenUsage(long InputTokens, long OutputTokens, long CacheWriteTokens, long CacheReadTokens)
{
    public static readonly TokenUsage None = new(0, 0, 0, 0);
}

/// <summary>
/// Interpreting a document failed. When the provider did answer - and so was
/// paid - but the answer could not be used, <see cref="Execution"/> says what
/// that answer cost; otherwise it is null and nothing is known to be spent.
/// </summary>
public class InterpretationFailedException : Exception
{
    public InterpretationFailedException(string message, ProviderExecution? execution = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Execution = execution;
    }

    public ProviderExecution? Execution { get; }
}
