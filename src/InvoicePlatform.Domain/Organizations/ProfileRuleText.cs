using InvoicePlatform.Domain.Localization;

namespace InvoicePlatform.Domain.Organizations;

/// <summary>The messages of <see cref="OrganizationProfileRules"/>.</summary>
internal interface IProfileRuleText
{
    string VatPrefix(string country);

    string VatShape(string vat);

    string RegistrationSchemeMissing { get; }

    string RegistrationSchemeInvalid(string scheme);

    string KvkShape { get; }

    string KboShape { get; }

    string PaymentMeansInvalid(string code);

    string IbanInvalid { get; }

    string EndpointSchemeMissing { get; }

    string EndpointSchemeInvalid(string scheme);
}

internal sealed class ProfileRuleTextNl : IProfileRuleText
{
    public string VatPrefix(string country) => $"Een btw-nummer uit {country} begint met {country}.";

    public string VatShape(string vat) => $"'{vat}' heeft niet de vorm van een btw-nummer uit dit land.";

    public string RegistrationSchemeMissing => "Kies het schema van het inschrijvingsnummer (KvK of KBO).";

    public string RegistrationSchemeInvalid(string scheme) => $"'{scheme}' is geen geldig schema voor een inschrijvingsnummer.";

    public string KvkShape => "Een KvK-nummer heeft 8 cijfers.";

    public string KboShape => "Een KBO-nummer heeft 10 cijfers en een geldige controle.";

    public string PaymentMeansInvalid(string code) => $"'{code}' is geen geldige betaalwijze.";

    public string IbanInvalid => "Dit IBAN klopt niet. Controleer het nummer.";

    public string EndpointSchemeMissing => "Kies het schema van het PEPPOL-id.";

    public string EndpointSchemeInvalid(string scheme) => $"'{scheme}' is geen geldig schema voor een PEPPOL-id.";
}

internal sealed class ProfileRuleTextEn : IProfileRuleText
{
    public string VatPrefix(string country) => $"A VAT number from {country} starts with {country}.";

    public string VatShape(string vat) => $"'{vat}' does not have the shape of a VAT number from this country.";

    public string RegistrationSchemeMissing => "Choose the scheme of the registration number (KvK or KBO).";

    public string RegistrationSchemeInvalid(string scheme) => $"'{scheme}' is not a valid registration number scheme.";

    public string KvkShape => "A KvK number has 8 digits.";

    public string KboShape => "A KBO number has 10 digits and a valid check.";

    public string PaymentMeansInvalid(string code) => $"'{code}' is not a valid payment means.";

    public string IbanInvalid => "This IBAN is not correct. Check the number.";

    public string EndpointSchemeMissing => "Choose the scheme of the PEPPOL ID.";

    public string EndpointSchemeInvalid(string scheme) => $"'{scheme}' is not a valid PEPPOL ID scheme.";
}

internal static class ProfileRuleText
{
    private static readonly IProfileRuleText Nl = new ProfileRuleTextNl(), En = new ProfileRuleTextEn();

    public static IProfileRuleText Current => Texts.Pick(Nl, En);
}
