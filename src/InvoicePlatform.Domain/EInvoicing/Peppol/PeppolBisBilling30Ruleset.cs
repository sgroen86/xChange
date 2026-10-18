using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Domain.EInvoicing.Peppol;

/// <summary>
/// PEPPOL BIS Billing 3.0, following rules release 3.0.21 (mandatory from
/// 17 August 2026): the shared structural rules, the EN 16931 VAT rules,
/// PEPPOL's own rules, and the Dutch national rules when the seller is in the
/// Netherlands.
///
/// The point release is not in the document: 3.0.21 rejects a CustomizationID
/// that carries version information beyond "3.0".
/// </summary>
public sealed class PeppolBisBilling30Ruleset : IInvoiceRuleset
{
    public const string RulesetId = "peppol-bis-billing-3.0";

    private static readonly TaxRegime Regime = new(
        "btw",
        CodeLists.VatCategories,
        CodeLists.IsPaymentMeansCode,
        TaxNumbersHaveCountryPrefix: true);

    public string Id => RulesetId;

    public string DisplayName => "PEPPOL BIS Billing 3.0";

    public string RulesRelease => "3.0.21";

    public InvoiceSyntax Syntax => InvoiceSyntax.Ubl21;

    public string CustomizationId => "urn:cen.eu:en16931:2017#compliant#urn:fdc:peppol.eu:2017:poacc:billing:3.0";

    public string ProfileId => "urn:fdc:peppol.eu:2017:poacc:billing:01:1.0";

    public string TaxName => "btw";

    /// <summary>K, L, M and B are left out: the model lacks the data they need.</summary>
    public IReadOnlyList<TaxCategory> TaxCategories { get; } =
    [
        new("S", "standaardtarief"),
        new("Z", "nultarief"),
        new("E", "vrijgesteld"),
        new("AE", "btw verlegd"),
        new("G", "export buiten de EU"),
        new("O", "niet onderworpen"),
    ];

    public UblDialect Ubl { get; } = new(
        "VAT",
        new HashSet<string>(StringComparer.Ordinal) { "O" },
        WritesDocumentUuid: false,
        WritesTaxCurrencyTotals: false);

    /// <summary>BIS 3.0 as xChange uses it has no separate tax currency.</summary>
    public CanonicalInvoiceDraft Prepare(CanonicalInvoiceDraft draft) => draft with { TaxCurrencyCode = null };

    public IReadOnlyList<RuleViolation> Check(CanonicalInvoiceDraft invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        return CommonRules.Check(invoice, Regime)
            .Concat(EuVatRules.Check(invoice))
            .Concat(PeppolRules.Check(invoice))
            .Concat(DutchRules.Check(invoice))
            .DistinctBy(violation => (violation.RuleId, violation.Path))
            .OrderBy(violation => violation.Path, StringComparer.Ordinal)
            .ThenBy(violation => violation.RuleId, StringComparer.Ordinal)
            .ToList();
    }
}
