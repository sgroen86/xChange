using InvoicePlatform.Domain.Localization;

namespace InvoicePlatform.Domain.EInvoicing;

/// <summary>The messages of <see cref="EuVatRules"/>.</summary>
internal interface IEuVatRuleText
{
    string SellerVatRequired(string category);

    string SellerVatRequiredReverseCharge { get; }

    string BuyerVatOrRegistrationRequiredReverseCharge { get; }

    string SellerVatRequiredExport { get; }

    string SellerVatNotAllowedNotSubject { get; }

    string BuyerVatNotAllowedNotSubject { get; }

    string NotSubjectCannotBeCombined { get; }

    string ExemptionReasonNotAllowed(string? category);

    string ExemptionReasonMissing(string? category);

    string StandardRateNeedsRate(string label);

    string ZeroRateRequired(string label, string? category);

    string IntraCommunitySupplyUnsupported(string label);

    string CategoryUnsupported(string label, string? category);
}

internal sealed class EuVatRuleTextNl : IEuVatRuleText
{
    public string SellerVatRequired(string category) => $"Btw-nummer van de verkoper is verplicht bij btw-categorie {category}.";

    public string SellerVatRequiredReverseCharge => "Btw-nummer van de verkoper is verplicht bij verlegde btw (AE).";

    public string BuyerVatOrRegistrationRequiredReverseCharge => "Btw-nummer of KvK-nummer van de afnemer is verplicht bij verlegde btw (AE).";

    public string SellerVatRequiredExport => "Btw-nummer van de verkoper is verplicht bij export buiten de EU (G).";

    public string SellerVatNotAllowedNotSubject
        => "Bij 'niet onderworpen aan btw' (O) mag het btw-nummer van de verkoper niet op de factuur staan.";

    public string BuyerVatNotAllowedNotSubject
        => "Bij 'niet onderworpen aan btw' (O) mag het btw-nummer van de afnemer niet op de factuur staan.";

    public string NotSubjectCannotBeCombined
        => "Btw-categorie O (niet onderworpen aan btw) kan niet samen met andere btw-categorieën op één factuur.";

    public string ExemptionReasonNotAllowed(string? category) => $"Bij btw-categorie {category} hoort geen vrijstellingsreden.";

    public string ExemptionReasonMissing(string? category) => $"Vrijstellingsreden ontbreekt voor btw-categorie {category}.";

    public string StandardRateNeedsRate(string label) => $"{label}: btw-categorie S (standaardtarief) vraagt een btw-percentage boven nul.";

    public string ZeroRateRequired(string label, string? category) => $"{label}: btw-categorie {category} vraagt btw-percentage 0.";

    public string IntraCommunitySupplyUnsupported(string label)
        => $"{label}: btw-categorie K (intracommunautaire levering) vraagt een leverdatum en leveringsland, "
            + "die xChange nog niet vastlegt.";

    public string CategoryUnsupported(string label, string? category) => $"{label}: btw-categorie {category} wordt niet ondersteund.";
}

internal sealed class EuVatRuleTextEn : IEuVatRuleText
{
    public string SellerVatRequired(string category) => $"The seller's VAT number is required for VAT category {category}.";

    public string SellerVatRequiredReverseCharge => "The seller's VAT number is required for reverse charge (AE).";

    public string BuyerVatOrRegistrationRequiredReverseCharge
        => "The buyer's VAT number or registration number is required for reverse charge (AE).";

    public string SellerVatRequiredExport => "The seller's VAT number is required for exports outside the EU (G).";

    public string SellerVatNotAllowedNotSubject
        => "With 'not subject to VAT' (O), the seller's VAT number must not appear on the invoice.";

    public string BuyerVatNotAllowedNotSubject
        => "With 'not subject to VAT' (O), the buyer's VAT number must not appear on the invoice.";

    public string NotSubjectCannotBeCombined
        => "VAT category O (not subject to VAT) cannot be combined with other VAT categories on one invoice.";

    public string ExemptionReasonNotAllowed(string? category) => $"VAT category {category} takes no exemption reason.";

    public string ExemptionReasonMissing(string? category) => $"The exemption reason for VAT category {category} is missing.";

    public string StandardRateNeedsRate(string label) => $"{label}: VAT category S (standard rate) needs a VAT rate above zero.";

    public string ZeroRateRequired(string label, string? category) => $"{label}: VAT category {category} needs a VAT rate of 0.";

    public string IntraCommunitySupplyUnsupported(string label)
        => $"{label}: VAT category K (intra-community supply) needs a delivery date and delivery country, "
            + "which xChange does not record yet.";

    public string CategoryUnsupported(string label, string? category) => $"{label}: VAT category {category} is not supported.";
}

internal static class EuVatRuleText
{
    private static readonly IEuVatRuleText Nl = new EuVatRuleTextNl(), En = new EuVatRuleTextEn();

    public static IEuVatRuleText Current => Texts.Pick(Nl, En);
}
