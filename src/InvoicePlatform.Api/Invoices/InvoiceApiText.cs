using InvoicePlatform.Domain.Localization;

namespace InvoicePlatform.Api.Invoices;

/// <summary>
/// The problem texts of the invoice endpoints (extract, store, check, export).
/// The upload checks of <see cref="ExtractInvoiceEndpoint"/> were English from
/// the start and stay as they are.
/// </summary>
internal interface IInvoiceApiText
{
    string OutOfCreditTitle { get; }

    string UnknownRulesetTitle { get; }

    string UnknownRulesetDetail(string? rulesetId);

    string ValidationUnavailableTitle { get; }

    string ValidationUnavailableDetail { get; }

    string PlanRequiredTitle { get; }

    string PlanRequiredDetail { get; }

    string NotBookedTitle { get; }

    string NotBookedDetail { get; }

    string AlreadyBookedTitle { get; }

    string AlreadyBookedDetail { get; }

    string InvalidDocumentTitle { get; }

    string InvalidDocumentDetail { get; }
}

internal sealed class InvoiceApiTextNl : IInvoiceApiText
{
    public string OutOfCreditTitle => "Tegoed op";

    public string UnknownRulesetTitle => "Onbekende regelset";

    public string UnknownRulesetDetail(string? rulesetId) => $"Regelset '{rulesetId}' bestaat niet.";

    public string ValidationUnavailableTitle => "Controle niet beschikbaar";

    public string ValidationUnavailableDetail => "De officiële controle is nu niet beschikbaar. Probeer het zo opnieuw.";

    public string PlanRequiredTitle => "Abonnement nodig";

    public string PlanRequiredDetail => "In de proefperiode kun je alles controleren, maar het bestand downloaden kan met een abonnement of gekocht tegoed.";

    public string NotBookedTitle => "Nog niet geboekt";

    public string NotBookedDetail => "Dit is een concept. Boek de factuur om het bestand te maken.";

    public string AlreadyBookedTitle => "Al geboekt";

    public string AlreadyBookedDetail => "Deze factuur is al geboekt. Boek hem opnieuw om je wijzigingen te bewaren.";

    public string InvalidDocumentTitle => "Ongeldig document";

    public string InvalidDocumentDetail => "Het document is geen geldige base64.";
}

internal sealed class InvoiceApiTextEn : IInvoiceApiText
{
    public string OutOfCreditTitle => "Out of credit";

    public string UnknownRulesetTitle => "Unknown ruleset";

    public string UnknownRulesetDetail(string? rulesetId) => $"Ruleset '{rulesetId}' does not exist.";

    public string ValidationUnavailableTitle => "Check unavailable";

    public string ValidationUnavailableDetail => "The official check is not available right now. Try again shortly.";

    public string PlanRequiredTitle => "Plan required";

    public string PlanRequiredDetail => "During the trial you can check everything, but downloading the file requires a plan or purchased credits.";

    public string NotBookedTitle => "Not booked yet";

    public string NotBookedDetail => "This is a draft. Book the invoice to create the file.";

    public string AlreadyBookedTitle => "Already booked";

    public string AlreadyBookedDetail => "This invoice has already been booked. Book it again to keep your changes.";

    public string InvalidDocumentTitle => "Invalid document";

    public string InvalidDocumentDetail => "The document is not valid base64.";
}

internal static class InvoiceApiText
{
    private static readonly IInvoiceApiText Nl = new InvoiceApiTextNl(), En = new InvoiceApiTextEn();

    public static IInvoiceApiText Current => Texts.Pick(Nl, En);
}
