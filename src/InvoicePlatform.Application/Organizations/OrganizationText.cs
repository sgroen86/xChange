using InvoicePlatform.Domain.Localization;

namespace InvoicePlatform.Application.Organizations;

/// <summary>The messages of <see cref="PlatformService"/>, <see cref="OrganizationSettingsService"/> and <see cref="OrganizationDeletionService"/>.</summary>
internal interface IOrganizationText
{
    string ChooseOfferedModel { get; }

    string UserNameRequired { get; }

    string AccountWithEmailExists { get; }

    string UnknownRuleset { get; }

    string EndpointSchemeMissing { get; }

    string EndpointSchemeInvalid(string scheme);

    string ConfirmNameMismatch { get; }

    string CannotDeleteOwnOrganization { get; }

    string CannotDeletePlatformAdminOrganization { get; }

    string CancelSubscriptionFirst { get; }
}

internal sealed class OrganizationTextNl : IOrganizationText
{
    public string ChooseOfferedModel => "Kies een van de aangeboden modellen.";

    public string UserNameRequired => "Naam van de gebruiker is verplicht.";

    public string AccountWithEmailExists => "Er bestaat al een account met dit e-mailadres.";

    public string UnknownRuleset => "Onbekende regelset.";

    public string EndpointSchemeMissing => "Kies het schema van het elektronisch adres.";

    public string EndpointSchemeInvalid(string scheme) => $"'{scheme}' is geen geldig schema voor een elektronisch adres.";

    public string ConfirmNameMismatch => "Typ de naam van de organisatie precies zoals hij is, ter bevestiging.";

    public string CannotDeleteOwnOrganization => "Je eigen organisatie kun je niet verwijderen.";

    public string CannotDeletePlatformAdminOrganization => "Deze organisatie heeft een platformbeheerder en kan niet worden verwijderd.";

    public string CancelSubscriptionFirst =>
        "Deze organisatie heeft een lopend abonnement. Zeg het eerst op (in het klantportaal van Stripe of het Stripe-dashboard) en verwijder de organisatie daarna.";
}

internal sealed class OrganizationTextEn : IOrganizationText
{
    public string ChooseOfferedModel => "Choose one of the models offered.";

    public string UserNameRequired => "The user's name is required.";

    public string AccountWithEmailExists => "An account with this email address already exists.";

    public string UnknownRuleset => "Unknown ruleset.";

    public string EndpointSchemeMissing => "Choose the scheme of the electronic address.";

    public string EndpointSchemeInvalid(string scheme) => $"'{scheme}' is not a valid electronic address scheme.";

    public string ConfirmNameMismatch => "Type the organisation's name exactly as it is, to confirm.";

    public string CannotDeleteOwnOrganization => "You cannot delete your own organisation.";

    public string CannotDeletePlatformAdminOrganization => "This organisation has a platform administrator and cannot be deleted.";

    public string CancelSubscriptionFirst =>
        "This organisation has a running subscription. Cancel it first (in Stripe's customer portal or the Stripe dashboard), then delete the organisation.";
}

internal static class OrganizationText
{
    private static readonly IOrganizationText Nl = new OrganizationTextNl(), En = new OrganizationTextEn();

    public static IOrganizationText Current => Texts.Pick(Nl, En);
}
