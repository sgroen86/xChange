namespace InvoicePlatform.Domain.Canonical;

/// <summary>
/// Where an extracted value came from in the source document, and how sure the
/// interpreter was about it. Carried alongside the value so the review step can
/// show a human what to check first.
/// </summary>
/// <param name="Field">Dotted path of the field this describes, e.g. "totals.payableAmount".</param>
/// <param name="Confidence">0.0 - 1.0. Null when the interpreter gave no score.</param>
/// <param name="PageNumber">1-based page in the source PDF, when known.</param>
/// <param name="SourceText">Verbatim snippet the value was read from, when known.</param>
public sealed record FieldEvidence(
    string Field,
    decimal? Confidence,
    int? PageNumber,
    string? SourceText);
