namespace InvoicePlatform.Infrastructure.Anthropic;

/// <summary>
/// Configuration for the Anthropic interpreter.
///
/// Nothing here has a secret default. The API key is supplied by configuration
/// or environment - never checked in, and never sent to the browser: the
/// frontend talks to this API, and only this API talks to the provider.
/// </summary>
public sealed class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    /// <summary>
    /// API key. Leave unset in appsettings and supply it through the environment
    /// (ANTHROPIC_API_KEY, or Anthropic__ApiKey) or user-secrets in development.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>Model identifier. Configured, never hardcoded at the call site.</summary>
    public string Model { get; set; } = "claude-opus-5";

    /// <summary>Ceiling for the interpreted JSON. Large invoices have many lines.</summary>
    public int MaxTokens { get; set; } = 16000;

    // There is deliberately no Temperature setting. The SDK marks the parameter
    // obsolete: "Models released after Claude Opus 4.6 do not support setting
    // temperature. A value of 1.0 will be accepted for backwards compatibility,
    // all other values will be rejected with a 400 error." Temperature 0 is
    // therefore impossible on the configured model family; the deterministic
    // setting available instead is the constrained output schema, which is
    // always applied. Nothing downstream of the model is sampled at all: the
    // arithmetic and the XML are both deterministic application code.

    /// <summary>Timeout for a single interpretation call.</summary>
    public int TimeoutSeconds { get; set; } = 180;
}
