using InvoicePlatform.Domain.Localization;

namespace InvoicePlatform.Application.Identity;

/// <summary>The messages and mails of <see cref="AuthService"/> and <see cref="SignupService"/>.</summary>
internal interface IIdentityText
{
    string TooManyAttempts { get; }

    string AccountLocked { get; }

    string TooManySignupMails { get; }

    string InvalidInvitation { get; }

    string AccountExists { get; }

    string OrganizationNameRequired { get; }

    string ChooseCountry { get; }

    string InvalidCredentials { get; }

    string AccountDeactivated { get; }

    string AccountWithEmailExistsLogIn { get; }

    string InvalidEmail { get; }

    string NameRequired { get; }

    string PasswordTooShort { get; }

    string InvalidConfirmationLink { get; }

    string SignupClosed { get; }

    string CompanyNameRequired { get; }

    string KnownAddressMailSubject { get; }

    string KnownAddressMailBody(string appUrl);

    string ConfirmationMailSubject { get; }

    string ConfirmationMailBody(string name, string organizationName, string link, int trialConversions);

    string InvalidCode { get; }

    string LoginExpired { get; }

    string TooManyWrongCodes { get; }

    string MfaAlreadyEnabled { get; }

    string MfaNotEnabled { get; }

    string MfaMandatory { get; }

    string MfaSetupNotStarted { get; }

    string UserNotFound { get; }
}

internal sealed class IdentityTextNl : IIdentityText
{
    public string TooManyAttempts => "Te veel pogingen. Wacht 15 minuten.";

    public string AccountLocked => "Te veel mislukte pogingen voor dit account. Wacht 15 minuten.";

    public string TooManySignupMails => "Er zijn het afgelopen uur al te veel bevestigingsmails naar dit adres gestuurd. Kijk in je inbox of probeer het later opnieuw.";

    public string InvalidInvitation => "Deze uitnodiging is ongeldig of verlopen.";

    public string AccountExists => "Er bestaat al een account. Gebruik inloggen.";

    public string OrganizationNameRequired => "Naam van de organisatie is verplicht.";

    public string ChooseCountry => "Kies een land uit de lijst.";

    public string InvalidCredentials => "Ongeldige inloggegevens.";

    public string AccountDeactivated => "Account gedeactiveerd.";

    public string AccountWithEmailExistsLogIn => "Er bestaat al een account met dit e-mailadres. Gebruik inloggen.";

    public string InvalidEmail => "Ongeldig e-mailadres.";

    public string NameRequired => "Naam is verplicht.";

    public string PasswordTooShort => "Wachtwoord moet minimaal 12 tekens bevatten.";

    public string InvalidConfirmationLink => "Deze bevestigingslink is ongeldig of verlopen. Meld je opnieuw aan.";

    public string SignupClosed => "Aanmelden is nog niet open.";

    public string CompanyNameRequired => "Naam van het bedrijf is verplicht.";

    public string KnownAddressMailSubject => "Je hebt al een xChange-account";

    public string KnownAddressMailBody(string appUrl) =>
        $"""
        Hallo,

        Iemand (waarschijnlijk jij) probeerde zich met dit e-mailadres aan te melden bij xChange.
        Er bestaat al een account met dit adres. Log in via {appUrl}/login/

        Was jij dit niet? Dan kun je deze e-mail negeren.
        """;

    public string ConfirmationMailSubject => "Bevestig je aanmelding bij xChange";

    public string ConfirmationMailBody(string name, string organizationName, string link, int trialConversions) =>
        $"""
        Hallo {name},

        Bevestig je e-mailadres om je xChange-account voor {organizationName} te activeren:

        {link}

        De link is 24 uur geldig. Je krijgt {trialConversions} gratis conversies om xChange te proberen.

        Heb jij je niet aangemeld? Dan kun je deze e-mail negeren; er wordt niets aangemaakt.
        """;

    public string InvalidCode => "Ongeldige code.";

    public string LoginExpired => "Je inlogpoging is verlopen. Log opnieuw in.";

    public string TooManyWrongCodes => "Te veel foute codes. Log opnieuw in.";

    public string MfaAlreadyEnabled => "Tweestapsverificatie staat al aan.";

    public string MfaNotEnabled => "Tweestapsverificatie staat niet aan.";

    public string MfaMandatory => "Tweestapsverificatie is verplicht voor beheerders en kan niet uit.";

    public string MfaSetupNotStarted => "Begin opnieuw met het instellen.";

    public string UserNotFound => "Er is geen gebruiker met dit e-mailadres.";
}

internal sealed class IdentityTextEn : IIdentityText
{
    public string TooManyAttempts => "Too many attempts. Wait 15 minutes.";

    public string AccountLocked => "Too many failed attempts for this account. Wait 15 minutes.";

    public string TooManySignupMails => "Too many confirmation emails have been sent to this address in the past hour. Check your inbox or try again later.";

    public string InvalidInvitation => "This invitation is invalid or has expired.";

    public string AccountExists => "An account already exists. Log in instead.";

    public string OrganizationNameRequired => "The organisation name is required.";

    public string ChooseCountry => "Choose a country from the list.";

    public string InvalidCredentials => "Invalid login details.";

    public string AccountDeactivated => "Account deactivated.";

    public string AccountWithEmailExistsLogIn => "An account with this email address already exists. Log in instead.";

    public string InvalidEmail => "Invalid email address.";

    public string NameRequired => "Name is required.";

    public string PasswordTooShort => "Your password must be at least 12 characters long.";

    public string InvalidConfirmationLink => "This confirmation link is invalid or has expired. Please sign up again.";

    public string SignupClosed => "Sign-up is not open yet.";

    public string CompanyNameRequired => "The company name is required.";

    public string KnownAddressMailSubject => "You already have an xChange account";

    public string KnownAddressMailBody(string appUrl) =>
        $"""
        Hello,

        Someone (probably you) tried to sign up for xChange with this email address.
        An account with this address already exists. Log in at {appUrl}/login/

        Wasn't this you? Then you can ignore this email.
        """;

    public string ConfirmationMailSubject => "Confirm your xChange sign-up";

    public string ConfirmationMailBody(string name, string organizationName, string link, int trialConversions) =>
        $"""
        Hello {name},

        Confirm your email address to activate your xChange account for {organizationName}:

        {link}

        The link is valid for 24 hours. You get {trialConversions} free conversions to try xChange.

        Didn't sign up? Then you can ignore this email; nothing will be created.
        """;

    public string InvalidCode => "Invalid code.";

    public string LoginExpired => "Your login attempt has expired. Please log in again.";

    public string TooManyWrongCodes => "Too many wrong codes. Please log in again.";

    public string MfaAlreadyEnabled => "Two-step verification is already on.";

    public string MfaNotEnabled => "Two-step verification is not on.";

    public string MfaMandatory => "Two-step verification is mandatory for administrators and cannot be turned off.";

    public string MfaSetupNotStarted => "Start the setup again.";

    public string UserNotFound => "There is no user with this email address.";
}

internal static class IdentityText
{
    private static readonly IIdentityText Nl = new IdentityTextNl(), En = new IdentityTextEn();

    public static IIdentityText Current => Texts.Pick(Nl, En);
}
