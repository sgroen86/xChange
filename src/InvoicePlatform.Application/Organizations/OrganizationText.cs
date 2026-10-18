using InvoicePlatform.Domain.Localization;

namespace InvoicePlatform.Application.Organizations;

/// <summary>The messages of <see cref="PlatformService"/> and <see cref="OrganizationSettingsService"/>.</summary>
internal interface IOrganizationText
{
    string ChooseOfferedModel { get; }

    string UserNameRequired { get; }

    string AccountWithEmailExists { get; }

    string UnknownRuleset { get; }

    string EndpointSchemeMissing { get; }

    string EndpointSchemeInvalid(string scheme);
}

internal sealed class OrganizationTextNl : IOrganizationText
{
    public string ChooseOfferedModel => "Kies een van de aangeboden modellen.";

    public string UserNameRequired => "Naam van de gebruiker is verplicht.";

    public string AccountWithEmailExists => "Er bestaat al een account met dit e-mailadres.";

    public string UnknownRuleset => "Onbekende regelset.";

    public string EndpointSchemeMissing => "Kies het schema van het elektronisch adres.";

    public string EndpointSchemeInvalid(string scheme) => $"'{scheme}' is geen geldig schema voor een elektronisch adres.";
}

internal sealed class OrganizationTextEn : IOrganizationText
{
    public string ChooseOfferedModel => "Choose one of the models offered.";

    public string UserNameRequired => "The user's name is required.";

    public string AccountWithEmailExists => "An account with this email address already exists.";

    public string UnknownRuleset => "Unknown ruleset.";

    public string EndpointSchemeMissing => "Choose the scheme of the electronic address.";

    public string EndpointSchemeInvalid(string scheme) => $"'{scheme}' is not a valid electronic address scheme.";
}

internal static class OrganizationText
{
    private static readonly IOrganizationText Nl = new OrganizationTextNl(), En = new OrganizationTextEn();

    public static IOrganizationText Current => Texts.Pick(Nl, En);
}
