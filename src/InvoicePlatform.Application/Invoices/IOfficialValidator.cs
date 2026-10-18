namespace InvoicePlatform.Application.Invoices;

/// <summary>
/// The complete official rules of a ruleset, run over a generated document.
/// The implementation lives outside .NET (the rules are XSLT 2.0); the export
/// only needs the verdict.
/// </summary>
public interface IOfficialValidator
{
    /// <returns>
    /// The verdict, or <see cref="OfficialValidation.Unavailable"/> when the
    /// validator could not be asked. Never throws for an unreachable validator:
    /// the caller decides, and refuses to hand out an unchecked file.
    /// </returns>
    Task<OfficialValidation> ValidateAsync(string rulesetId, string xml, CancellationToken cancellationToken = default);
}

/// <param name="Flag">"fatal" or "warning", as the official rules say.</param>
/// <param name="Location">Where in the document, e.g. "/Invoice[1]/LegalMonetaryTotal[1]".</param>
public sealed record OfficialFailure(string Flag, string RuleId, string Message, string Location)
{
    public bool IsFatal => Flag == "fatal";
}

/// <param name="Available">False when the validator could not be asked or answered with an error.</param>
/// <param name="Release">The rules release the validator used, e.g. "3.0.21".</param>
public sealed record OfficialValidation(
    bool Available,
    string? Release,
    IReadOnlyList<OfficialFailure> Failures,
    string? Error = null)
{
    public static OfficialValidation Unavailable(string error) => new(false, null, [], error);
}
