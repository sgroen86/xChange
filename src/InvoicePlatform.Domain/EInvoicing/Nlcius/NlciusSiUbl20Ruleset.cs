using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.EInvoicing.Peppol;

namespace InvoicePlatform.Domain.EInvoicing.Nlcius;

/// <summary>
/// NLCIUS, the Dutch CIUS on EN 16931, as SI-UBL 2.0 (validation set 2.0.3.13
/// of the Nederlandse Peppol Autoriteit, mandatory from 17 August 2026). Used
/// by Dutch government bodies and Dutch accounting software next to BIS 3.0.
/// Same UBL syntax and VAT categories as BIS 3.0, its own CustomizationID, the
/// Dutch BR-NL rules, and none of PEPPOL's own rules.
/// </summary>
public sealed class NlciusSiUbl20Ruleset : IInvoiceRuleset
{
    public const string RulesetId = "nlcius-si-ubl-2.0";

    private static readonly TaxRegime Regime = new(
        TaxKind.Vat,
        CodeLists.VatCategories,
        CodeLists.IsPaymentMeansCode,
        TaxNumbersHaveCountryPrefix: true);

    private static readonly PeppolBisBilling30Ruleset Bis = new();

    public string Id => RulesetId;

    public string DisplayName => NlciusRuleText.Current.DisplayName;

    public string RulesRelease => "2.0.3.13";

    public InvoiceSyntax Syntax => InvoiceSyntax.Ubl21;

    public string CustomizationId => "urn:cen.eu:en16931:2017#compliant#urn:fdc:nen.nl:nlcius:v1.0";

    /// <summary>The PEPPOL billing process, under which SI-UBL 2.0 travels over PEPPOL.</summary>
    public string ProfileId => "urn:fdc:peppol.eu:2017:poacc:billing:01:1.0";

    public string TaxName => Bis.TaxName;

    public IReadOnlyList<TaxCategory> TaxCategories => Bis.TaxCategories;

    public string? TaxCurrency => null;

    public UblDialect Ubl => Bis.Ubl;

    /// <summary>
    /// As BIS 3.0 (no tax currency, a GST number as BT-32), and without the
    /// fields NLCIUS advises against: the payment means text (BR-NL-29) and the
    /// account name (BR-NL-30). The booked invoice keeps them.
    /// </summary>
    public CanonicalInvoiceDraft Prepare(CanonicalInvoiceDraft draft)
    {
        var prepared = Bis.Prepare(draft);
        return prepared.Payment is null
            ? prepared
            : prepared with { Payment = prepared.Payment with { PaymentMeansText = null, AccountName = null } };
    }

    public IReadOnlyList<RuleViolation> Check(CanonicalInvoiceDraft invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        return CommonRules.Check(invoice, Regime)
            .Concat(EuVatRules.Check(invoice))
            .Concat(NlciusRules.Check(invoice))
            .Concat(VatNumberShapes.Check(invoice))
            .DistinctBy(violation => (violation.RuleId, violation.Path))
            .OrderBy(violation => violation.Path, StringComparer.Ordinal)
            .ThenBy(violation => violation.RuleId, StringComparer.Ordinal)
            .ToList();
    }
}
