namespace InvoicePlatform.Application.Organizations;

/// <summary>Settings for the whole platform, set by the platform administrator.</summary>
public sealed record PlatformSettings
{
    /// <summary>
    /// The model that reads PDFs, one of <see cref="ExtractionModels.All"/>.
    /// Null means the configured default (Anthropic:Model). Platform-wide
    /// because the platform pays for every token.
    /// </summary>
    public string? ExtractionModel { get; init; }
}

/// <param name="Id">The provider's model id, as the API expects it.</param>
/// <param name="Name">For the screen.</param>
public sealed record ExtractionModel(string Id, string Name);

/// <summary>
/// The models the platform administrator can choose from. Each must handle the
/// extraction request as it is (PDF input, structured output); a model is
/// added here only after it has read real invoices correctly.
/// </summary>
public static class ExtractionModels
{
    public static readonly IReadOnlyList<ExtractionModel> All =
    [
        new("claude-opus-5", "Claude Opus 5"),
        new("claude-opus-5-5", "Claude Opus 5.5"),
        // Both read the test invoice correctly on 2026-10-18, at about a third
        // (Sonnet) and a thirtieth (Haiku) of the cost of Opus.
        new("claude-sonnet-5-5", "Claude Sonnet 5.5"),
        new("claude-haiku-5-5", "Claude Haiku 5.5"),
    ];

    public static bool IsAllowed(string? id) => All.Any(m => m.Id == id);
}
