using InvoicePlatform.Domain.Localization;

namespace InvoicePlatform.Api.Identity;

/// <summary>The problem titles of <see cref="AuthEndpoints"/> and <see cref="SignupEndpoints"/>. The details come from Application.</summary>
internal interface IIdentityApiText
{
    string SetupFailed { get; }

    string LoginFailed { get; }

    string InvitationInvalid { get; }

    string AccountNotCreated { get; }

    string SignupClosed { get; }

    string TooManyAttempts { get; }

    string SignupNotSent { get; }
}

internal sealed class IdentityApiTextNl : IIdentityApiText
{
    public string SetupFailed => "Setup mislukt";

    public string LoginFailed => "Inloggen mislukt";

    public string InvitationInvalid => "Uitnodiging ongeldig";

    public string AccountNotCreated => "Account niet aangemaakt";

    public string SignupClosed => "Aanmelden is gesloten";

    public string TooManyAttempts => "Te veel pogingen";

    public string SignupNotSent => "Aanmelding niet verstuurd";
}

internal sealed class IdentityApiTextEn : IIdentityApiText
{
    public string SetupFailed => "Setup failed";

    public string LoginFailed => "Login failed";

    public string InvitationInvalid => "Invalid invitation";

    public string AccountNotCreated => "Account not created";

    public string SignupClosed => "Sign-up is closed";

    public string TooManyAttempts => "Too many attempts";

    public string SignupNotSent => "Sign-up not sent";
}

internal static class IdentityApiText
{
    private static readonly IIdentityApiText Nl = new IdentityApiTextNl(), En = new IdentityApiTextEn();

    public static IIdentityApiText Current => Texts.Pick(Nl, En);
}
