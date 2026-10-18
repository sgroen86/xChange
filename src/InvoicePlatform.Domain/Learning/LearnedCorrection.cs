namespace InvoicePlatform.Domain.Learning;

/// <summary>How a learned correction is used on the next invoice.</summary>
public enum CorrectionUse
{
    /// <summary>Applied by code when the model reads exactly the same value again.</summary>
    Apply,

    /// <summary>Given to the model as a reading hint; never applied by code.</summary>
    Hint,
}

/// <summary>
/// Something a reviewer corrected, remembered per organisation (tenant-owned:
/// it lives in the organisation's partition and is only read with its id).
/// </summary>
/// <param name="Id">Stable for the same scope, field and change, so a repeat counts rather than duplicates.</param>
/// <param name="Scope">"organization", or "buyer:&lt;VAT or registration number&gt;" for one customer.</param>
/// <param name="Field">
/// A field path ("seller.vatIdentifier", "buyerReference"), or a family:
/// "lines.tax" (category|rate per line), "lines.unitCode", "exemptionReason:&lt;category&gt;".
/// </param>
/// <param name="From">What the model read; null for "nothing".</param>
/// <param name="To">What the reviewer booked.</param>
/// <param name="Context">For a line: its description, so a hint says which kind of line it was.</param>
public sealed record LearnedCorrection(
    string Id,
    string Scope,
    string Field,
    string? From,
    string? To,
    string? Context,
    CorrectionUse Use,
    int Count,
    DateTimeOffset LastSeen)
{
    public const string OrganizationScope = "organization";

    public static string BuyerScope(string buyerKey) => $"buyer:{buyerKey}";
}

/// <summary>A correction as the model sees it: where it read what, and what the reviewer entered.</summary>
public sealed record ReadingHint(string Field, string? YouRead, string? ReviewerEntered, string? Context);
