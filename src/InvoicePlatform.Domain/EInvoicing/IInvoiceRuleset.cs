using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Domain.EInvoicing;

/// <summary>The XML syntax a ruleset is expressed in.</summary>
public enum InvoiceSyntax
{
    Ubl21,
}

/// <summary>A tax category a ruleset accepts, e.g. "S" standard rate, or "SR" for Singapore GST.</summary>
public sealed record TaxCategory(string Code, string Label);

/// <summary>
/// The details in which UBL documents differ between rulesets.
/// </summary>
/// <param name="TaxSchemeId">cac:TaxScheme/cbc:ID: "VAT" in Europe, "GST" in Singapore.</param>
/// <param name="CategoriesWithoutRate">Categories written without cbc:Percent ("O" in EN 16931, "NG" in PINT-SG).</param>
/// <param name="WritesDocumentUuid">Whether cbc:UUID is part of the document.</param>
/// <param name="WritesTaxCurrencyTotals">
/// Whether the totals without and with tax in the tax currency are written as
/// AdditionalDocumentReference (PINT-SG: sgdtotal-excl-gst, sgdtotal-incl-gst).
/// </param>
public sealed record UblDialect(
    string TaxSchemeId,
    IReadOnlySet<string> CategoriesWithoutRate,
    bool WritesDocumentUuid,
    bool WritesTaxCurrencyTotals);

/// <summary>
/// One e-invoicing ruleset: a specialisation of EN 16931 (PEPPOL BIS, NLCIUS,
/// XRechnung, ...) in one syntax. Adding a ruleset is one class and one
/// registration in <see cref="InvoiceRulesetRegistry"/>.
/// </summary>
public interface IInvoiceRuleset
{
    /// <summary>Stable identifier, used in URLs and settings, e.g. "peppol-bis-billing-3.0".</summary>
    string Id { get; }

    string DisplayName { get; }

    /// <summary>The release of the official rules this implementation follows, e.g. "3.0.21".</summary>
    string RulesRelease { get; }

    InvoiceSyntax Syntax { get; }

    /// <summary>BT-24, written as cbc:CustomizationID.</summary>
    string CustomizationId { get; }

    /// <summary>BT-23, written as cbc:ProfileID.</summary>
    string ProfileId { get; }

    /// <summary>What the tax is called on screen: "btw", "GST".</summary>
    string TaxName { get; }

    /// <summary>The tax categories the review screen offers for this ruleset.</summary>
    IReadOnlyList<TaxCategory> TaxCategories { get; }

    /// <summary>
    /// The currency tax must also be stated in when the invoice is in another
    /// one (PINT-SG: SGD), or null. The review screen then asks for the rate.
    /// </summary>
    string? TaxCurrency { get; }

    UblDialect Ubl { get; }

    /// <summary>
    /// Fills what the ruleset derives itself rather than asking the user for -
    /// PINT-SG's tax currency, for example - before the totals are calculated.
    /// </summary>
    CanonicalInvoiceDraft Prepare(CanonicalInvoiceDraft draft);

    /// <summary>
    /// Checks an invoice whose line nets, VAT breakdown and totals have already
    /// been calculated. Returns every violation found, not only the first.
    /// </summary>
    IReadOnlyList<RuleViolation> Check(CanonicalInvoiceDraft invoice);
}
