using InvoicePlatform.Domain.Localization;

namespace InvoicePlatform.Api.Organizations;

/// <summary>The problem titles of the organisation and platform endpoints. The details come from Application.</summary>
internal interface IOrganizationApiText
{
    string SettingsNotSaved { get; }

    string NotSaved { get; }

    string SettingNotSaved { get; }

    string InvalidMonthTitle { get; }

    string InvalidMonthDetail { get; }
}

internal sealed class OrganizationApiTextNl : IOrganizationApiText
{
    public string SettingsNotSaved => "Instellingen niet opgeslagen";

    public string NotSaved => "Niet opgeslagen";

    public string SettingNotSaved => "Instelling niet opgeslagen";

    public string InvalidMonthTitle => "Ongeldige maand";

    public string InvalidMonthDetail => "Geef de maand als jjjj-MM, bijvoorbeeld 2026-10.";
}

internal sealed class OrganizationApiTextEn : IOrganizationApiText
{
    public string SettingsNotSaved => "Settings not saved";

    public string NotSaved => "Not saved";

    public string SettingNotSaved => "Setting not saved";

    public string InvalidMonthTitle => "Invalid month";

    public string InvalidMonthDetail => "Give the month as yyyy-MM, for example 2026-10.";
}

internal static class OrganizationApiText
{
    private static readonly IOrganizationApiText Nl = new OrganizationApiTextNl(), En = new OrganizationApiTextEn();

    public static IOrganizationApiText Current => Texts.Pick(Nl, En);
}
