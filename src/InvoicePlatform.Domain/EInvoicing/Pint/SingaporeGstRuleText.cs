using InvoicePlatform.Domain.Localization;

namespace InvoicePlatform.Domain.EInvoicing.Pint;

/// <summary>The texts of <see cref="SingaporeGstRules"/> and the GST category labels of <see cref="PintSgBilling1Ruleset"/>.</summary>
internal interface ISingaporeGstRuleText
{
    string StandardRate { get; }

    string ZeroRate { get; }

    string ExemptRegulation33 { get; }

    string ExemptOther { get; }

    string OutOfScope { get; }

    string DeemedSupply { get; }

    string NotRegistered { get; }

    string CustomerAccountingSupplier { get; }

    string CustomerAccountingCustomer { get; }

    string ReverseChargeImportedServices { get; }

    string RemoteServices { get; }

    string LowValueGoods { get; }

    string OwnLowValueGoods { get; }

    string NoGstDue { get; }

    string NotGstCategory(string? category);

    string SellerGstNumberMissing { get; }

    string SellerUenMissing { get; }

    string CreditNoteReasonMissing { get; }

    string ExchangeRateMissing(string currency);

    string ExchangeRateNotPositive { get; }

    string NotRegisteredCannotBeCombined { get; }

    string NotRegisteredSellerGstNumber { get; }

    string NotRegisteredBuyerGstNumber { get; }

    string DueDateMissing { get; }

    string UenEndpointShape { get; }
}

internal sealed class SingaporeGstRuleTextNl : ISingaporeGstRuleText
{
    public string StandardRate => "standaardtarief (9%)";

    public string ZeroRate => "nultarief (export)";

    public string ExemptRegulation33 => "vrijgesteld (reg. 33)";

    public string ExemptOther => "vrijgesteld (overig)";

    public string OutOfScope => "buiten de werkingssfeer";

    public string DeemedSupply => "geachte levering";

    public string NotRegistered => "niet GST-geregistreerd";

    public string CustomerAccountingSupplier => "customer accounting (leverancier)";

    public string CustomerAccountingCustomer => "customer accounting (afnemer)";

    public string ReverseChargeImportedServices => "verlegd (ingevoerde diensten)";

    public string RemoteServices => "remote services (OVR)";

    public string LowValueGoods => "low-value goods (OVR)";

    public string OwnLowValueGoods => "eigen low-value goods";

    public string NoGstDue => "geen GST verschuldigd (bijzondere regeling)";

    public string NotGstCategory(string? category) => $"'{category}' is geen GST-categorie.";

    public string SellerGstNumberMissing => "GST-nummer van de verkoper ontbreekt.";

    public string SellerUenMissing => "UEN van de verkoper ontbreekt.";

    public string CreditNoteReasonMissing => "Een creditnota vermeldt in de opmerking de reden van de creditering.";

    public string ExchangeRateMissing(string currency) => $"De factuur is in {currency}: vul de door IRAS goedgekeurde wisselkoers naar SGD in.";

    public string ExchangeRateNotPositive => "De wisselkoers naar SGD moet groter dan nul zijn.";

    public string NotRegisteredCannotBeCombined
        => "Categorie NG (niet GST-geregistreerd) kan niet samen met andere GST-categorieën op één factuur.";

    public string NotRegisteredSellerGstNumber => "Bij categorie NG staat er geen GST-nummer van de verkoper op de factuur.";

    public string NotRegisteredBuyerGstNumber => "Bij categorie NG staat er geen GST-nummer van de afnemer op de factuur.";

    public string DueDateMissing => "Vervaldatum ontbreekt: er staat een bedrag open.";

    public string UenEndpointShape => "Een Singaporees PEPPOL-id (schema 0195) is SGUEN gevolgd door de UEN, bijv. SGUEN201912345K.";
}

internal sealed class SingaporeGstRuleTextEn : ISingaporeGstRuleText
{
    public string StandardRate => "standard rate (9%)";

    public string ZeroRate => "zero rate (export)";

    public string ExemptRegulation33 => "exempt (reg. 33)";

    public string ExemptOther => "exempt (other)";

    public string OutOfScope => "out of scope";

    public string DeemedSupply => "deemed supply";

    public string NotRegistered => "not GST-registered";

    public string CustomerAccountingSupplier => "customer accounting (supplier)";

    public string CustomerAccountingCustomer => "customer accounting (customer)";

    public string ReverseChargeImportedServices => "reverse charge (imported services)";

    public string RemoteServices => "remote services (OVR)";

    public string LowValueGoods => "low-value goods (OVR)";

    public string OwnLowValueGoods => "own low-value goods";

    public string NoGstDue => "no GST due (special scheme)";

    public string NotGstCategory(string? category) => $"'{category}' is not a GST category.";

    public string SellerGstNumberMissing => "The seller's GST number is missing.";

    public string SellerUenMissing => "The seller's UEN is missing.";

    public string CreditNoteReasonMissing => "A credit note states the reason for the credit in the note.";

    public string ExchangeRateMissing(string currency) => $"The invoice is in {currency}: enter the IRAS-approved exchange rate to SGD.";

    public string ExchangeRateNotPositive => "The exchange rate to SGD must be greater than zero.";

    public string NotRegisteredCannotBeCombined
        => "Category NG (not GST-registered) cannot be combined with other GST categories on one invoice.";

    public string NotRegisteredSellerGstNumber => "With category NG, the seller's GST number must not appear on the invoice.";

    public string NotRegisteredBuyerGstNumber => "With category NG, the buyer's GST number must not appear on the invoice.";

    public string DueDateMissing => "The due date is missing: there is an amount to pay.";

    public string UenEndpointShape => "A Singapore PEPPOL ID (scheme 0195) is SGUEN followed by the UEN, e.g. SGUEN201912345K.";
}

internal static class SingaporeGstRuleText
{
    private static readonly ISingaporeGstRuleText Nl = new SingaporeGstRuleTextNl(), En = new SingaporeGstRuleTextEn();

    public static ISingaporeGstRuleText Current => Texts.Pick(Nl, En);
}
