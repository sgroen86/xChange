using InvoicePlatform.Domain.Localization;

namespace InvoicePlatform.Domain.EInvoicing.Peppol;

/// <summary>
/// The texts of PEPPOL BIS 3.0 in xChange: <see cref="PeppolRules"/>,
/// <see cref="DutchRules"/>, <see cref="VatNumberShapes"/> and what
/// <see cref="PeppolBisBilling30Ruleset"/> shows on screen.
/// </summary>
internal interface IPeppolRuleText
{
    string DisplayName { get; }

    string StandardRate { get; }

    string ZeroRate { get; }

    string Exempt { get; }

    string ReverseCharge { get; }

    string ExportOutsideEu { get; }

    string NotSubject { get; }

    string BuyerReferenceMissing { get; }

    string BuyerEndpointMissing { get; }

    string SellerEndpointMissing { get; }

    string BaseQuantityNotPositive(string line);

    string DocumentLevelLabel(string number);

    string DirectDebitUnsupported { get; }

    string GlnInvalid(string value);

    string BelgianEnterpriseNumberInvalid(string value);

    string TypeCodeNotAllowed(string? code);

    string TypeCodeGermanOnly(string? code);

    string IssueDateFormat { get; }

    string DueDateFormat { get; }

    string PercentageNeedsBase(string label, bool isCharge);

    string BaseNeedsPercentage(string label, bool isCharge);

    string AmountIsNotBaseTimesPercentage(string label, bool isCharge, string baseAmount, string percentage);

    string CreditNoteNeedsPrecedingInvoice { get; }

    string DutchPaymentMeansOnly { get; }

    string DutchVatShape(PartyRole party, string value);

    string BelgianVatShape(PartyRole party, string value);

    string BritishVatShape(PartyRole party, string value);
}

internal sealed class PeppolRuleTextNl : IPeppolRuleText
{
    private static string Of(PartyRole party) => CommonRuleTextNl.Of(party);

    private static string Kind(bool isCharge) => CommonRuleTextNl.Kind(isCharge);

    public string DisplayName => "PEPPOL BIS Billing 3.0 (VK, België, Nederland)";

    public string StandardRate => "standaardtarief";

    public string ZeroRate => "nultarief";

    public string Exempt => "vrijgesteld";

    public string ReverseCharge => "btw verlegd";

    public string ExportOutsideEu => "export buiten de EU";

    public string NotSubject => "niet onderworpen";

    public string BuyerReferenceMissing => "Vul de referentie van de afnemer of het inkoopordernummer in.";

    public string BuyerEndpointMissing => "Elektronisch adres (PEPPOL-id) van de afnemer ontbreekt.";

    public string SellerEndpointMissing => "Elektronisch adres (PEPPOL-id) van de verkoper ontbreekt.";

    public string BaseQuantityNotPositive(string line) => $"Regel {line}: de basishoeveelheid moet groter dan nul zijn.";

    public string DocumentLevelLabel(string number) => $"Factuurniveau {number}";

    public string DirectDebitUnsupported
        => "Incasso vraagt een machtigingskenmerk, dat xChange nog niet vastlegt. Kies een andere betaalwijze.";

    public string GlnInvalid(string value) => $"'{value}' is geen geldig GLN-nummer (controlecijfer klopt niet).";

    public string BelgianEnterpriseNumberInvalid(string value)
        => $"'{value}' is geen geldig Belgisch ondernemingsnummer (tien cijfers, controlegetal modulo 97).";

    public string TypeCodeNotAllowed(string? code) => $"Factuurtype {code} is niet toegestaan in PEPPOL BIS 3.0.";

    public string TypeCodeGermanOnly(string? code) => $"Factuurtype {code} mag alleen tussen twee Duitse partijen.";

    public string IssueDateFormat => "Factuurdatum moet de vorm JJJJ-MM-DD hebben.";

    public string DueDateFormat => "Vervaldatum moet de vorm JJJJ-MM-DD hebben.";

    public string PercentageNeedsBase(string label, bool isCharge) => $"{label}: bij een percentage hoort een grondslag voor de {Kind(isCharge)}.";

    public string BaseNeedsPercentage(string label, bool isCharge) => $"{label}: bij een grondslag hoort een percentage voor de {Kind(isCharge)}.";

    public string AmountIsNotBaseTimesPercentage(string label, bool isCharge, string baseAmount, string percentage)
        => $"{label}: bedrag van de {Kind(isCharge)} is niet gelijk aan grondslag x percentage "
            + $"({baseAmount} x {percentage}%).";

    public string CreditNoteNeedsPrecedingInvoice => "Een creditnota moet verwijzen naar de oorspronkelijke factuur.";

    public string DutchPaymentMeansOnly => "Tussen Nederlandse partijen is de betaalwijze 30, 48, 49, 57, 58 of 59.";

    public string DutchVatShape(PartyRole party, string value)
        => $"Btw-nummer van de {Of(party)} '{value}' heeft niet de Nederlandse vorm NL + 9 cijfers + B + 2 cijfers, zonder spaties of punten.";

    public string BelgianVatShape(PartyRole party, string value)
        => $"Btw-nummer van de {Of(party)} '{value}' is geen geldig Belgisch nummer: BE + 10 cijfers (het ondernemingsnummer), zonder spaties of punten.";

    public string BritishVatShape(PartyRole party, string value)
        => $"Btw-nummer van de {Of(party)} '{value}' heeft niet de Britse vorm GB + 9 of 12 cijfers (of GBGD/GBHA + 3 cijfers), zonder spaties.";
}

internal sealed class PeppolRuleTextEn : IPeppolRuleText
{
    private static string Of(PartyRole party) => CommonRuleTextEn.Of(party);

    private static string Kind(bool isCharge) => CommonRuleTextEn.Kind(isCharge);

    public string DisplayName => "PEPPOL BIS Billing 3.0 (UK, Belgium, Netherlands)";

    public string StandardRate => "standard rate";

    public string ZeroRate => "zero rate";

    public string Exempt => "exempt";

    public string ReverseCharge => "reverse charge";

    public string ExportOutsideEu => "export outside the EU";

    public string NotSubject => "not subject to VAT";

    public string BuyerReferenceMissing => "Enter the buyer reference or the purchase order number.";

    public string BuyerEndpointMissing => "The buyer's electronic address (PEPPOL ID) is missing.";

    public string SellerEndpointMissing => "The seller's electronic address (PEPPOL ID) is missing.";

    public string BaseQuantityNotPositive(string line) => $"Line {line}: the base quantity must be greater than zero.";

    public string DocumentLevelLabel(string number) => $"Document level {number}";

    public string DirectDebitUnsupported
        => "Direct debit needs a mandate reference, which xChange does not record yet. Choose another payment method.";

    public string GlnInvalid(string value) => $"'{value}' is not a valid GLN (the check digit is wrong).";

    public string BelgianEnterpriseNumberInvalid(string value)
        => $"'{value}' is not a valid Belgian enterprise number (ten digits, modulo 97 check).";

    public string TypeCodeNotAllowed(string? code) => $"Invoice type {code} is not allowed in PEPPOL BIS 3.0.";

    public string TypeCodeGermanOnly(string? code) => $"Invoice type {code} is only allowed between two German parties.";

    public string IssueDateFormat => "The invoice date must have the form YYYY-MM-DD.";

    public string DueDateFormat => "The due date must have the form YYYY-MM-DD.";

    public string PercentageNeedsBase(string label, bool isCharge) => $"{label}: a {Kind(isCharge)} percentage needs a base amount.";

    public string BaseNeedsPercentage(string label, bool isCharge) => $"{label}: a {Kind(isCharge)} base amount needs a percentage.";

    public string AmountIsNotBaseTimesPercentage(string label, bool isCharge, string baseAmount, string percentage)
        => $"{label}: the {Kind(isCharge)} amount does not equal base amount x percentage "
            + $"({baseAmount} x {percentage}%).";

    public string CreditNoteNeedsPrecedingInvoice => "A credit note must refer to the original invoice.";

    public string DutchPaymentMeansOnly => "Between Dutch parties the payment method is 30, 48, 49, 57, 58 or 59.";

    public string DutchVatShape(PartyRole party, string value)
        => $"The {Of(party)}'s VAT number '{value}' does not have the Dutch form NL + 9 digits + B + 2 digits, without spaces or dots.";

    public string BelgianVatShape(PartyRole party, string value)
        => $"The {Of(party)}'s VAT number '{value}' is not a valid Belgian number: BE + 10 digits (the enterprise number), without spaces or dots.";

    public string BritishVatShape(PartyRole party, string value)
        => $"The {Of(party)}'s VAT number '{value}' does not have the British form GB + 9 or 12 digits (or GBGD/GBHA + 3 digits), without spaces.";
}

internal static class PeppolRuleText
{
    private static readonly IPeppolRuleText Nl = new PeppolRuleTextNl(), En = new PeppolRuleTextEn();

    public static IPeppolRuleText Current => Texts.Pick(Nl, En);
}
