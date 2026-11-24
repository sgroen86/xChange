using InvoicePlatform.Domain.Localization;

namespace InvoicePlatform.Application.Invoices;

/// <summary>The notices of <see cref="InvoiceEnrichment"/>.</summary>
internal interface IEnrichmentText
{
    string SellerFilledFromProfile(string field);

    string SellerReplacedFromProfile(string field, string documentValue);

    /// <summary>A seller field's name by its path, for the two notices above.</summary>
    string SellerField(string path);

    string BuyerEndpointFromRelation { get; }

    string BuyerRegistrationFromRelation { get; }

    string BuyerVatFromRelation { get; }

    string BuyerCountryFromRelation { get; }

    /// <summary>The fields a conflict names, as <see cref="Conflict"/> uses them.</summary>
    string ElectronicAddress { get; }

    string Registration { get; }

    string Vat { get; }

    string Country { get; }

    string Conflict(string field, string relationValue);
}

internal sealed class EnrichmentTextNl : IEnrichmentText
{
    public string SellerFilledFromProfile(string field) => $"{field} van de verkoper ingevuld uit het bedrijfsprofiel.";

    public string SellerReplacedFromProfile(string field, string documentValue)
        => $"{field} van de verkoper komt uit het bedrijfsprofiel; de PDF toonde {documentValue}.";

    public string SellerField(string path) => path switch
    {
        "seller.name" => "Naam",
        "seller.vatIdentifier" => "Btw-nummer",
        "seller.legalRegistrationId" => "Inschrijvingsnummer",
        "seller.electronicAddress" => "PEPPOL-id",
        "seller.contactName" => "Contactpersoon",
        "seller.contactEmail" => "E-mailadres",
        "seller.contactPhone" => "Telefoonnummer",
        "seller.address.streetName" => "Straat",
        "seller.address.additionalStreetName" => "Extra adresregel",
        "seller.address.postalZone" => "Postcode",
        "seller.address.cityName" => "Plaats",
        "seller.address.countryCode" => "Landcode",
        "payment.paymentMeansCode" => "Betaalwijze",
        "payment.iban" => "IBAN",
        "payment.bic" => "BIC",
        "payment.accountName" => "Rekeninghouder",
        _ => path,
    };
    public string BuyerEndpointFromRelation => "Elektronisch adres van de afnemer ingevuld uit de relatiegegevens.";

    public string BuyerRegistrationFromRelation => "KvK-nummer van de afnemer ingevuld uit de relatiegegevens.";

    public string BuyerVatFromRelation => "Btw-nummer van de afnemer ingevuld uit de relatiegegevens.";

    public string BuyerCountryFromRelation => "Landcode van de afnemer ingevuld uit de relatiegegevens.";

    public string ElectronicAddress => "Elektronisch adres";

    public string Registration => "KvK-nummer";

    public string Vat => "Btw-nummer";

    public string Country => "Landcode";

    public string Conflict(string field, string relationValue)
        => $"{field} van de afnemer wijkt af van de relatiegegevens ({relationValue}). De waarde uit de PDF is behouden.";
}

internal sealed class EnrichmentTextEn : IEnrichmentText
{
    public string SellerFilledFromProfile(string field) => $"The seller's {field} was filled in from the company profile.";

    public string SellerReplacedFromProfile(string field, string documentValue)
        => $"The seller's {field} comes from the company profile; the PDF showed {documentValue}.";

    public string SellerField(string path) => path switch
    {
        "seller.name" => "name",
        "seller.vatIdentifier" => "VAT number",
        "seller.legalRegistrationId" => "registration number",
        "seller.electronicAddress" => "PEPPOL ID",
        "seller.contactName" => "contact person",
        "seller.contactEmail" => "email address",
        "seller.contactPhone" => "phone number",
        "seller.address.streetName" => "street",
        "seller.address.additionalStreetName" => "additional address line",
        "seller.address.postalZone" => "postcode",
        "seller.address.cityName" => "city",
        "seller.address.countryCode" => "country code",
        "payment.paymentMeansCode" => "payment means",
        "payment.iban" => "IBAN",
        "payment.bic" => "BIC",
        "payment.accountName" => "account holder",
        _ => path,
    };
    public string BuyerEndpointFromRelation => "The buyer's electronic address was filled in from the customer details.";

    public string BuyerRegistrationFromRelation => "The buyer's registration number was filled in from the customer details.";

    public string BuyerVatFromRelation => "The buyer's VAT number was filled in from the customer details.";

    public string BuyerCountryFromRelation => "The buyer's country code was filled in from the customer details.";

    public string ElectronicAddress => "electronic address";

    public string Registration => "registration number";

    public string Vat => "VAT number";

    public string Country => "country code";

    public string Conflict(string field, string relationValue)
        => $"The buyer's {field} differs from the customer details ({relationValue}). The value from the PDF was kept.";
}

internal static class EnrichmentText
{
    private static readonly IEnrichmentText Nl = new EnrichmentTextNl(), En = new EnrichmentTextEn();

    public static IEnrichmentText Current => Texts.Pick(Nl, En);
}
