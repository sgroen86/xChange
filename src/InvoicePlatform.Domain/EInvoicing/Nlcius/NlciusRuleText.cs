using InvoicePlatform.Domain.Localization;

namespace InvoicePlatform.Domain.EInvoicing.Nlcius;

/// <summary>The texts of <see cref="NlciusRules"/> and <see cref="NlciusSiUbl20Ruleset"/>.</summary>
internal interface INlciusRuleText
{
    string DisplayName { get; }

    string BuyerReferenceMissing { get; }

    string TypeCodeNotAllowed { get; }

    string CorrectedInvoiceNeedsPrecedingInvoice { get; }

    string RegistrationMissing(PartyRole party);

    string PaymentMeansNotAllowed { get; }
}

internal sealed class NlciusRuleTextNl : INlciusRuleText
{
    public string DisplayName => "NLCIUS / SI-UBL 2.0 (Nederland)";

    public string BuyerReferenceMissing => "Vermeld de referentie van de afnemer of een inkoopordernummer.";

    public string TypeCodeNotAllowed
        => "NLCIUS kent alleen factuur (380), creditnota (381), corrigerende factuur (384) en zelffactuur (389).";

    public string CorrectedInvoiceNeedsPrecedingInvoice => "Een corrigerende factuur moet verwijzen naar de oorspronkelijke factuur.";

    public string RegistrationMissing(PartyRole party) => $"Vermeld het KvK- of OIN-nummer van de {CommonRuleTextNl.Of(party)}.";

    public string PaymentMeansNotAllowed => "NLCIUS kent als betaalwijze alleen 30, 48, 49, 57, 58 en 59.";
}

internal sealed class NlciusRuleTextEn : INlciusRuleText
{
    public string DisplayName => "NLCIUS / SI-UBL 2.0 (Netherlands)";

    public string BuyerReferenceMissing => "Enter the buyer reference or a purchase order number.";

    public string TypeCodeNotAllowed
        => "NLCIUS only allows invoice (380), credit note (381), corrected invoice (384) and self-billed invoice (389).";

    public string CorrectedInvoiceNeedsPrecedingInvoice => "A corrected invoice must refer to the original invoice.";

    public string RegistrationMissing(PartyRole party) => $"Enter the {CommonRuleTextEn.Of(party)}'s KvK or OIN number.";

    public string PaymentMeansNotAllowed => "NLCIUS only allows payment methods 30, 48, 49, 57, 58 and 59.";
}

internal static class NlciusRuleText
{
    private static readonly INlciusRuleText Nl = new NlciusRuleTextNl(), En = new NlciusRuleTextEn();

    public static INlciusRuleText Current => Texts.Pick(Nl, En);
}
