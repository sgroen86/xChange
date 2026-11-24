namespace InvoicePlatform.Application.Interpretation;

/// <summary>
/// Why reading a document failed, in terms the user can act on. Each reason has
/// its own message in <see cref="InterpretationText"/>.
/// </summary>
public enum InterpretationFailure
{
    /// <summary>The answer was empty or did not match the schema.</summary>
    Unreadable,

    /// <summary>The answer was cut off at the token limit: too much to read in one go.</summary>
    TooLarge,

    /// <summary>The provider did not answer within the configured time.</summary>
    TimedOut,

    /// <summary>The model declined to process the document.</summary>
    Declined,

    /// <summary>The provider could not be reached, kept failing, or rejected the key.</summary>
    ProviderUnavailable,
}
