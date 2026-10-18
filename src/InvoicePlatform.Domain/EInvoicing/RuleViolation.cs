namespace InvoicePlatform.Domain.EInvoicing;

/// <summary>A rule an invoice breaks, in terms the reviewer can act on.</summary>
/// <param name="RuleId">
/// The official rule id where one exists ("BR-07", "PEPPOL-EN16931-R003",
/// "NL-R-003"); "XC-..." for xChange's own, e.g. a category the model cannot
/// support yet.
/// </param>
/// <param name="BusinessTerm">EN 16931 business term, e.g. "BT-49", when the rule concerns one field.</param>
/// <param name="Path">
/// Dotted path of the field, e.g. "buyer.electronicAddress" or
/// "lines[0].vatCategoryCode". Equal to the input id on the review screen, so a
/// violation can take the reviewer to its field.
/// </param>
/// <param name="Message">Dutch, for the reviewer.</param>
public sealed record RuleViolation(string RuleId, string? BusinessTerm, string Path, string Message);
