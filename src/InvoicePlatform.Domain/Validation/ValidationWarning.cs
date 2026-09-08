namespace InvoicePlatform.Domain.Validation;

public enum ValidationSeverity
{
    Warning,
    Error,
}

/// <summary>
/// A discrepancy found while checking an extracted invoice.
///
/// Warnings are reported, never applied: the validator does not overwrite what
/// was extracted from the document, because a mismatch between the printed
/// total and the recomputed one is information a human needs, not noise to be
/// silently corrected away.
/// </summary>
/// <param name="Code">Stable machine-readable code, e.g. "CALC-LINE-NET".</param>
/// <param name="Path">Dotted path of the field concerned.</param>
/// <param name="Message">Human-readable explanation.</param>
/// <param name="ExtractedValue">The value as extracted, when the warning compares values.</param>
/// <param name="CalculatedValue">The value the deterministic calculation produced.</param>
public sealed record ValidationWarning(
    string Code,
    string Path,
    string Message,
    ValidationSeverity Severity = ValidationSeverity.Warning,
    decimal? ExtractedValue = null,
    decimal? CalculatedValue = null);
