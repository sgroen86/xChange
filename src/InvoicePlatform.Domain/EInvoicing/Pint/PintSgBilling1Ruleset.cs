using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.EInvoicing.Peppol;

namespace InvoicePlatform.Domain.EInvoicing.Pint;

/// <summary>
/// PINT-SG Billing 1.4.1: Singapore's specialisation of the Peppol
/// International invoice model, used by InvoiceNow and the IRAS GST InvoiceNow
/// requirement. Same UBL syntax as BIS 3.0, but GST instead of VAT, its own
/// tax categories, a UUID, and the tax total in SGD for an invoice in another
/// currency. Not valid as BIS 3.0, and BIS 3.0 is not valid as PINT-SG.
/// </summary>
public sealed class PintSgBilling1Ruleset : IInvoiceRuleset
{
    public const string RulesetId = "pint-sg-billing-1";

    private static readonly TaxRegime Regime = new(
        "GST",
        SingaporeGstRules.Categories,
        // BR-CL-16-SG: UNCL 4461, plus GIRO (Z01) and PayNow (Z02).
        code => code is "Z01" or "Z02" || CodeLists.IsPaymentMeansCode(code),
        TaxNumbersHaveCountryPrefix: false);

    public string Id => RulesetId;

    public string DisplayName => "PINT-SG (Singapore, InvoiceNow)";

    public string RulesRelease => "1.4.1";

    public InvoiceSyntax Syntax => InvoiceSyntax.Ubl21;

    public string CustomizationId => "urn:peppol:pint:billing-1@sg-1";

    public string ProfileId => "urn:peppol:bis:billing";

    public string TaxName => "GST";

    public IReadOnlyList<TaxCategory> TaxCategories { get; } =
    [
        new("SR", "standaardtarief (9%)"),
        new("ZR", "nultarief (export)"),
        new("ES33", "vrijgesteld (reg. 33)"),
        new("ESN33", "vrijgesteld (overig)"),
        new("OS", "buiten de werkingssfeer"),
        new("DS", "geachte levering"),
        new("NG", "niet GST-geregistreerd"),
        new("SRCA-S", "customer accounting (leverancier)"),
        new("SRCA-C", "customer accounting (afnemer)"),
        new("SRRC", "verlegd (ingevoerde diensten)"),
        new("SROVR-RS", "remote services (OVR)"),
        new("SROVR-LVG", "low-value goods (OVR)"),
        new("SRLVG", "eigen low-value goods"),
        new("NA", "geen GST verschuldigd (bijzondere regeling)"),
    ];

    public string? TaxCurrency => SingaporeGstRules.TaxCurrency;

    public UblDialect Ubl { get; } = new(
        "GST",
        new HashSet<string>(StringComparer.Ordinal) { "NG" },
        WritesDocumentUuid: true,
        WritesTaxCurrencyTotals: true);

    /// <summary>
    /// BR-113-GST-SG: a GST-registered invoice in another currency carries SGD
    /// as tax currency; an invoice in SGD must not carry one.
    /// </summary>
    public CanonicalInvoiceDraft Prepare(CanonicalInvoiceDraft draft)
        => draft with
        {
            TaxCurrencyCode = draft.CurrencyCode is { } currency
                && currency != SingaporeGstRules.TaxCurrency
                && SingaporeGstRules.UsesRegisteredCategory(draft)
                    ? SingaporeGstRules.TaxCurrency
                    : null,
        };

    public IReadOnlyList<RuleViolation> Check(CanonicalInvoiceDraft invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        return CommonRules.Check(invoice, Regime)
            .Concat(PeppolRules.Check(invoice))
            .Concat(SingaporeGstRules.Check(invoice))
            .DistinctBy(violation => (violation.RuleId, violation.Path))
            .OrderBy(violation => violation.Path, StringComparer.Ordinal)
            .ThenBy(violation => violation.RuleId, StringComparer.Ordinal)
            .ToList();
    }
}
