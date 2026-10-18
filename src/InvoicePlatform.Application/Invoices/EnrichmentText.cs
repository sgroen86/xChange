using InvoicePlatform.Domain.Localization;

namespace InvoicePlatform.Application.Invoices;

/// <summary>The notices of <see cref="InvoiceEnrichment"/>.</summary>
internal interface IEnrichmentText
{
    string SellerEndpointFromSettings { get; }

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
    public string SellerEndpointFromSettings => "Elektronisch adres van de verkoper ingevuld uit de organisatie-instellingen.";

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
    public string SellerEndpointFromSettings => "The seller's electronic address was filled in from the organisation settings.";

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
