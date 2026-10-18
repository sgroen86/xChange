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
        TaxKind.Gst,
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

    public string TaxName => CommonRuleText.Current.TaxName(TaxKind.Gst);

    public IReadOnlyList<TaxCategory> TaxCategories
    {
        get
        {
            var text = SingaporeGstRuleText.Current;
            return
            [
                new("SR", text.StandardRate),
                new("ZR", text.ZeroRate),
                new("ES33", text.ExemptRegulation33),
                new("ESN33", text.ExemptOther),
                new("OS", text.OutOfScope),
                new("DS", text.DeemedSupply),
                new("NG", text.NotRegistered),
                new("SRCA-S", text.CustomerAccountingSupplier),
                new("SRCA-C", text.CustomerAccountingCustomer),
                new("SRRC", text.ReverseChargeImportedServices),
                new("SROVR-RS", text.RemoteServices),
                new("SROVR-LVG", text.LowValueGoods),
                new("SRLVG", text.OwnLowValueGoods),
                new("NA", text.NoGstDue),
            ];
        }
    }

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
