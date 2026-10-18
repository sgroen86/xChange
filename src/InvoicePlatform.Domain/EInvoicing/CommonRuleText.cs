using InvoicePlatform.Domain.Localization;

namespace InvoicePlatform.Domain.EInvoicing;

/// <summary>
/// The messages of <see cref="CommonRules"/>, plus the party and line wording
/// the other rule families share (address, Dutch registration scheme, labels).
/// </summary>
internal interface ICommonRuleText
{
    /// <summary>"btw"/"VAT", "GST": the tax as messages and screens call it.</summary>
    string TaxName(TaxKind tax);

    string LineLabel(string number);

    string DocumentAdjustmentLabel(bool isCharge, string number);

    string InvoiceNumberMissing { get; }

    string IssueDateMissing { get; }

    string TypeCodeMissing { get; }

    string CurrencyMissing { get; }

    string CurrencyInvalid(string? code);

    string SellerNameMissing { get; }

    string BuyerNameMissing { get; }

    string CountryMissing(PartyRole party);

    string CountryInvalid(PartyRole party, string? code);

    string TaxNumberOrRegistrationMissing(TaxKind tax);

    string VatPrefixMissing(PartyRole party);

    string EndpointSchemeMissing(PartyRole party);

    string EndpointSchemeInvalid(string? scheme);

    string RegistrationSchemeInvalid(PartyRole party);

    string NoLines { get; }

    string LineQuantityMissing(string line);

    string LineUnitMissing(string line);

    string LineUnitInvalid(string line, string? unit);

    string LineDescriptionMissing(string line);

    string LinePriceMissing(string line);

    string LinePriceNegative(string line);

    string LineNetAmountDecimals(string line);

    string LineTaxCategoryMissing(string line, TaxKind tax);

    string LineTaxCategoryInvalid(string line, string? category, TaxKind tax);

    string LineAdjustmentAmountMissing(string line, bool isCharge);

    string LineAdjustmentReasonMissing(string line, bool isCharge);

    string LineAdjustmentAmountDecimals(string line, bool isCharge);

    string LineAdjustmentBaseDecimals(string line, bool isCharge);

    string DocumentAdjustmentAmountMissing(string label);

    string DocumentAdjustmentTaxCategoryMissing(string label);

    string DocumentAdjustmentReasonMissing(string label);

    string DocumentAdjustmentAmountDecimals(string label);

    string DocumentAdjustmentBaseDecimals(string label);

    string PaymentMeansMissing { get; }

    string PaymentMeansInvalid(string? code);

    string IbanRequired { get; }

    string PrepaidAmountDecimals { get; }

    string RoundingAmountDecimals { get; }

    string StreetMissing(PartyRole party);

    string CityMissing(PartyRole party);

    string PostalZoneMissing(PartyRole party);

    string DutchRegistrationSchemeRequired(PartyRole party);

    string PaymentMeansNeeded { get; }
}

internal sealed class CommonRuleTextNl : ICommonRuleText
{
    /// <summary>"verkoper", "afnemer".</summary>
    public static string Of(PartyRole party) => party == PartyRole.Seller ? "verkoper" : "afnemer";

    /// <summary>"toeslag", "korting".</summary>
    public static string Kind(bool isCharge) => isCharge ? "toeslag" : "korting";

    public string TaxName(TaxKind tax) => tax == TaxKind.Gst ? "GST" : "btw";

    public string LineLabel(string number) => $"Regel {number}";

    public string DocumentAdjustmentLabel(bool isCharge, string number) => $"{(isCharge ? "Toeslag" : "Korting")} {number} op factuurniveau";

    public string InvoiceNumberMissing => "Factuurnummer ontbreekt.";

    public string IssueDateMissing => "Factuurdatum ontbreekt.";

    public string TypeCodeMissing => "Factuurtype ontbreekt.";

    public string CurrencyMissing => "Valuta ontbreekt.";

    public string CurrencyInvalid(string? code) => $"Valuta '{code}' is geen ISO 4217-code van drie hoofdletters, zoals EUR.";

    public string SellerNameMissing => "Naam van de verkoper ontbreekt.";

    public string BuyerNameMissing => "Naam van de afnemer ontbreekt.";

    public string CountryMissing(PartyRole party) => $"Landcode van de {Of(party)} ontbreekt.";

    public string CountryInvalid(PartyRole party, string? code)
        => $"Landcode van de {Of(party)} '{code}' is geen ISO 3166-code van twee hoofdletters, zoals NL.";

    public string TaxNumberOrRegistrationMissing(TaxKind tax)
        => $"Vermeld het {TaxName(tax)}-nummer of het registratienummer van de verkoper.";

    public string VatPrefixMissing(PartyRole party) => $"Btw-nummer van de {Of(party)} moet beginnen met een landcode, zoals NL.";

    public string EndpointSchemeMissing(PartyRole party) => $"Kies het schema van het elektronisch adres van de {Of(party)}.";

    public string EndpointSchemeInvalid(string? scheme) => $"'{scheme}' is geen geldig schema voor een elektronisch adres.";

    public string RegistrationSchemeInvalid(PartyRole party)
        => $"Schema van het registratienummer van de {Of(party)} moet een code van vier cijfers zijn, zoals 0106 (KvK).";

    public string NoLines => "De factuur heeft geen regels.";

    public string LineQuantityMissing(string line) => $"Regel {line}: aantal ontbreekt.";

    public string LineUnitMissing(string line) => $"Regel {line}: eenheid ontbreekt.";

    public string LineUnitInvalid(string line, string? unit)
        => $"Regel {line}: eenheid '{unit}' is geen UN/ECE-code, zoals C62 (stuk) of HUR (uur).";

    public string LineDescriptionMissing(string line) => $"Regel {line}: omschrijving ontbreekt. Die wordt de artikelnaam.";

    public string LinePriceMissing(string line) => $"Regel {line}: prijs ontbreekt.";

    public string LinePriceNegative(string line) => $"Regel {line}: de prijs mag niet negatief zijn. Gebruik een negatief aantal.";

    public string LineNetAmountDecimals(string line) => $"Regel {line}: het nettobedrag mag hoogstens twee decimalen hebben.";

    public string LineTaxCategoryMissing(string line, TaxKind tax) => $"Regel {line}: {TaxName(tax)}-categorie ontbreekt.";

    public string LineTaxCategoryInvalid(string line, string? category, TaxKind tax)
        => $"Regel {line}: '{category}' is geen {TaxName(tax)}-categorie.";

    public string LineAdjustmentAmountMissing(string line, bool isCharge) => $"Regel {line}: bedrag van de {Kind(isCharge)} ontbreekt.";

    public string LineAdjustmentReasonMissing(string line, bool isCharge) => $"Regel {line}: reden van de {Kind(isCharge)} ontbreekt.";

    public string LineAdjustmentAmountDecimals(string line, bool isCharge)
        => $"Regel {line}: bedrag van de {Kind(isCharge)} mag hoogstens twee decimalen hebben.";

    public string LineAdjustmentBaseDecimals(string line, bool isCharge)
        => $"Regel {line}: grondslag van de {Kind(isCharge)} mag hoogstens twee decimalen hebben.";

    public string DocumentAdjustmentAmountMissing(string label) => $"{label}: bedrag ontbreekt.";

    public string DocumentAdjustmentTaxCategoryMissing(string label) => $"{label}: belastingcategorie ontbreekt.";

    public string DocumentAdjustmentReasonMissing(string label) => $"{label}: reden ontbreekt.";

    public string DocumentAdjustmentAmountDecimals(string label) => $"{label}: bedrag mag hoogstens twee decimalen hebben.";

    public string DocumentAdjustmentBaseDecimals(string label) => $"{label}: grondslag mag hoogstens twee decimalen hebben.";

    public string PaymentMeansMissing => "Kies een betaalwijze.";

    public string PaymentMeansInvalid(string? code) => $"'{code}' is geen geldige betaalwijze.";

    public string IbanRequired => "IBAN is verplicht bij betaling per overschrijving.";

    public string PrepaidAmountDecimals => "Vooruitbetaald bedrag mag hoogstens twee decimalen hebben.";

    public string RoundingAmountDecimals => "Afrondingsbedrag mag hoogstens twee decimalen hebben.";

    public string StreetMissing(PartyRole party) => $"Straat van de {Of(party)} ontbreekt.";

    public string CityMissing(PartyRole party) => $"Plaats van de {Of(party)} ontbreekt.";

    public string PostalZoneMissing(PartyRole party) => $"Postcode van de {Of(party)} ontbreekt.";

    public string DutchRegistrationSchemeRequired(PartyRole party)
        => $"Kies KvK (0106) of OIN (0190) als schema van het registratienummer van de {Of(party)}.";

    public string PaymentMeansNeeded => "Geef een betaalwijze op: er staat een bedrag open.";
}

internal sealed class CommonRuleTextEn : ICommonRuleText
{
    /// <summary>"seller", "buyer".</summary>
    public static string Of(PartyRole party) => party == PartyRole.Seller ? "seller" : "buyer";

    /// <summary>"charge", "allowance".</summary>
    public static string Kind(bool isCharge) => isCharge ? "charge" : "allowance";

    public string TaxName(TaxKind tax) => tax == TaxKind.Gst ? "GST" : "VAT";

    public string LineLabel(string number) => $"Line {number}";

    public string DocumentAdjustmentLabel(bool isCharge, string number) => $"Document-level {Kind(isCharge)} {number}";

    public string InvoiceNumberMissing => "The invoice number is missing.";

    public string IssueDateMissing => "The invoice date is missing.";

    public string TypeCodeMissing => "The invoice type is missing.";

    public string CurrencyMissing => "The currency is missing.";

    public string CurrencyInvalid(string? code) => $"Currency '{code}' is not a three-letter ISO 4217 code, such as EUR.";

    public string SellerNameMissing => "The seller's name is missing.";

    public string BuyerNameMissing => "The buyer's name is missing.";

    public string CountryMissing(PartyRole party) => $"The {Of(party)}'s country code is missing.";

    public string CountryInvalid(PartyRole party, string? code)
        => $"The {Of(party)}'s country code '{code}' is not a two-letter ISO 3166 code, such as NL.";

    public string TaxNumberOrRegistrationMissing(TaxKind tax)
        => $"Enter the seller's {TaxName(tax)} number or registration number.";

    public string VatPrefixMissing(PartyRole party) => $"The {Of(party)}'s VAT number must start with a country code, such as NL.";

    public string EndpointSchemeMissing(PartyRole party) => $"Choose the scheme of the {Of(party)}'s electronic address.";

    public string EndpointSchemeInvalid(string? scheme) => $"'{scheme}' is not a valid electronic address scheme.";

    public string RegistrationSchemeInvalid(PartyRole party)
        => $"The scheme of the {Of(party)}'s registration number must be a four-digit code, such as 0106 (KvK).";

    public string NoLines => "The invoice has no lines.";

    public string LineQuantityMissing(string line) => $"Line {line}: the quantity is missing.";

    public string LineUnitMissing(string line) => $"Line {line}: the unit is missing.";

    public string LineUnitInvalid(string line, string? unit)
        => $"Line {line}: unit '{unit}' is not a UN/ECE code, such as C62 (piece) or HUR (hour).";

    public string LineDescriptionMissing(string line) => $"Line {line}: the description is missing. It becomes the item name.";

    public string LinePriceMissing(string line) => $"Line {line}: the price is missing.";

    public string LinePriceNegative(string line) => $"Line {line}: the price cannot be negative. Use a negative quantity.";

    public string LineNetAmountDecimals(string line) => $"Line {line}: the net amount can have at most two decimals.";

    public string LineTaxCategoryMissing(string line, TaxKind tax) => $"Line {line}: the {TaxName(tax)} category is missing.";

    public string LineTaxCategoryInvalid(string line, string? category, TaxKind tax)
        => $"Line {line}: '{category}' is not a {TaxName(tax)} category.";

    public string LineAdjustmentAmountMissing(string line, bool isCharge) => $"Line {line}: the {Kind(isCharge)} amount is missing.";

    public string LineAdjustmentReasonMissing(string line, bool isCharge) => $"Line {line}: the {Kind(isCharge)} reason is missing.";

    public string LineAdjustmentAmountDecimals(string line, bool isCharge)
        => $"Line {line}: the {Kind(isCharge)} amount can have at most two decimals.";

    public string LineAdjustmentBaseDecimals(string line, bool isCharge)
        => $"Line {line}: the {Kind(isCharge)} base amount can have at most two decimals.";

    public string DocumentAdjustmentAmountMissing(string label) => $"{label}: the amount is missing.";

    public string DocumentAdjustmentTaxCategoryMissing(string label) => $"{label}: the tax category is missing.";

    public string DocumentAdjustmentReasonMissing(string label) => $"{label}: the reason is missing.";

    public string DocumentAdjustmentAmountDecimals(string label) => $"{label}: the amount can have at most two decimals.";

    public string DocumentAdjustmentBaseDecimals(string label) => $"{label}: the base amount can have at most two decimals.";

    public string PaymentMeansMissing => "Choose a payment method.";

    public string PaymentMeansInvalid(string? code) => $"'{code}' is not a valid payment method.";

    public string IbanRequired => "An IBAN is required for payment by credit transfer.";

    public string PrepaidAmountDecimals => "The prepaid amount can have at most two decimals.";

    public string RoundingAmountDecimals => "The rounding amount can have at most two decimals.";

    public string StreetMissing(PartyRole party) => $"The {Of(party)}'s street is missing.";

    public string CityMissing(PartyRole party) => $"The {Of(party)}'s city is missing.";

    public string PostalZoneMissing(PartyRole party) => $"The {Of(party)}'s postcode is missing.";

    public string DutchRegistrationSchemeRequired(PartyRole party)
        => $"Choose KvK (0106) or OIN (0190) as the scheme of the {Of(party)}'s registration number.";

    public string PaymentMeansNeeded => "Enter a payment method: there is an amount to pay.";
}

internal static class CommonRuleText
{
    private static readonly ICommonRuleText Nl = new CommonRuleTextNl(), En = new CommonRuleTextEn();

    public static ICommonRuleText Current => Texts.Pick(Nl, En);
}
