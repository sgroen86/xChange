using InvoicePlatform.Domain.Localization;

namespace InvoicePlatform.Application.Relations;

/// <summary>The messages of <see cref="RelationService"/>.</summary>
internal interface IRelationText
{
    string EndpointSchemeInvalid { get; }

    string RegistrationSchemeInvalid { get; }

    string UnknownExportFormat { get; }

    string IdentifierRequired { get; }
}

internal sealed class RelationTextNl : IRelationText
{
    public string EndpointSchemeInvalid => "Kies een geldig schema voor het elektronisch adres.";

    public string RegistrationSchemeInvalid => "Het schema van het registratienummer is een code van vier cijfers, zoals 0106.";

    public string UnknownExportFormat => "Kies een bestaand exportformaat, of geen.";

    public string IdentifierRequired => "Een relatie heeft een btw-nummer of KvK-nummer nodig om herkend te worden.";
}

internal sealed class RelationTextEn : IRelationText
{
    public string EndpointSchemeInvalid => "Choose a valid scheme for the electronic address.";

    public string RegistrationSchemeInvalid => "The registration number scheme is a four-digit code, such as 0106.";

    public string UnknownExportFormat => "Choose an existing export format, or none.";

    public string IdentifierRequired => "A customer needs a VAT number or registration number to be recognised.";
}

internal static class RelationText
{
    private static readonly IRelationText Nl = new RelationTextNl(), En = new RelationTextEn();

    public static IRelationText Current => Texts.Pick(Nl, En);
}
