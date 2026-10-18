using InvoicePlatform.Domain.Localization;

namespace InvoicePlatform.Api;

/// <summary>
/// The problem texts every part of the API shares: not signed in, no access,
/// an unreadable request. Area-specific texts live next to their endpoints.
/// </summary>
internal interface IApiText
{
    string NotSignedInTitle { get; }

    string NotSignedInDetail { get; }

    string ForbiddenTitle { get; }

    string RoleForbiddenDetail { get; }

    string PlatformAdminOnlyDetail { get; }

    string InvalidRequestTitle { get; }

    string InvalidJsonDetail { get; }

    string MissingFieldsDetail { get; }
}

internal sealed class ApiTextNl : IApiText
{
    public string NotSignedInTitle => "Niet ingelogd";

    public string NotSignedInDetail => "Log in om deze actie uit te voeren.";

    public string ForbiddenTitle => "Geen toegang";

    public string RoleForbiddenDetail => "Uw account heeft hiervoor geen rechten.";

    public string PlatformAdminOnlyDetail => "Alleen de platformbeheerder kan dit.";

    public string InvalidRequestTitle => "Ongeldige aanvraag";

    public string InvalidJsonDetail => "De aanvraag is geen geldige JSON of mist verplichte velden.";

    public string MissingFieldsDetail => "De aanvraag mist verplichte velden.";
}

internal sealed class ApiTextEn : IApiText
{
    public string NotSignedInTitle => "Not signed in";

    public string NotSignedInDetail => "Log in to do this.";

    public string ForbiddenTitle => "No access";

    public string RoleForbiddenDetail => "Your account does not have permission for this.";

    public string PlatformAdminOnlyDetail => "Only the platform administrator can do this.";

    public string InvalidRequestTitle => "Invalid request";

    public string InvalidJsonDetail => "The request is not valid JSON or is missing required fields.";

    public string MissingFieldsDetail => "The request is missing required fields.";
}

internal static class ApiText
{
    private static readonly IApiText Nl = new ApiTextNl(), En = new ApiTextEn();

    public static IApiText Current => Texts.Pick(Nl, En);
}
