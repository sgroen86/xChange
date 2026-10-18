using System.Text.RegularExpressions;
using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Domain.EInvoicing.Pint;

/// <summary>
/// The Singapore GST rules of PINT-SG Billing 1.4.1 that can be expressed on
/// the canonical model. Rule ids are the official ones (BR-*-GST-SG,
/// BR-NG-*-GST-SG); "XC-SG-0195" is xChange's own check of the 0195 endpoint
/// format, which the official rules check in the shared PINT set.
/// </summary>
internal static partial class SingaporeGstRules
{
    /// <summary>BR-CL-17-GST-SG / BR-CL-18-GST-SG.</summary>
    public static readonly IReadOnlySet<string> Categories = new HashSet<string>(StringComparer.Ordinal)
    {
        "SR", "SRCA-S", "SRCA-C", "ZR", "SRRC", "SROVR-RS", "SROVR-LVG", "SRLVG",
        "ES33", "ESN33", "DS", "OS", "NG", "NA",
    };

    /// <summary>The categories of a GST-registered supplier, which trigger BR-105 to BR-113.</summary>
    public static readonly IReadOnlySet<string> Registered = new HashSet<string>(StringComparer.Ordinal)
    {
        "SR", "SRCA-S", "SRCA-C", "ZR", "SRRC", "SROVR-RS", "SROVR-LVG", "SRLVG", "NA",
    };

    public const string TaxCurrency = "SGD";

    public static bool UsesRegisteredCategory(CanonicalInvoiceDraft invoice)
        => InvoiceFacts.CategoriesInUse(invoice).Any(used => Registered.Contains(used.Category));

    public static IReadOnlyList<RuleViolation> Check(CanonicalInvoiceDraft invoice)
    {
        var findings = new Findings();
        var text = SingaporeGstRuleText.Current;

        for (var index = 0; index < invoice.AllowancesAndCharges.Count; index++)
        {
            var category = invoice.AllowancesAndCharges[index].VatCategoryCode;
            if (!Findings.Missing(category) && !Categories.Contains(category!))
            {
                findings.Add("BR-CL-17-GST-SG", "BT-95", $"allowancesAndCharges[{index}].vatCategoryCode",
                    text.NotGstCategory(category));
            }
        }

        if (UsesRegisteredCategory(invoice))
        {
            Registered_(invoice, findings, text, CommonRuleText.Current);
        }

        NotRegistered(invoice, findings, text);
        DueDate(invoice, findings, text);
        Endpoint(invoice.Seller, "seller", findings, text);
        Endpoint(invoice.Buyer, "buyer", findings, text);

        return findings.Violations;
    }

    private static void Registered_(CanonicalInvoiceDraft invoice, Findings findings, ISingaporeGstRuleText text, ICommonRuleText common)
    {
        var seller = invoice.Seller;
        var buyer = invoice.Buyer;

        if (Findings.Missing(seller?.VatIdentifier))
        {
            findings.Add("BR-105-GST-SG", "BT-31", "seller.vatIdentifier", text.SellerGstNumberMissing);
        }

        if (Findings.Missing(seller?.Address?.StreetName))
        {
            findings.Add("BR-106-GST-SG", "BT-35", "seller.address.streetName", common.StreetMissing(PartyRole.Seller));
        }

        if (Findings.Missing(seller?.Address?.PostalZone))
        {
            findings.Add("BR-106-GST-SG", "BT-38", "seller.address.postalZone", common.PostalZoneMissing(PartyRole.Seller));
        }

        if (Findings.Missing(buyer?.Address?.StreetName))
        {
            findings.Add("BR-107-GST-SG", "BT-50", "buyer.address.streetName", common.StreetMissing(PartyRole.Buyer));
        }

        if (Findings.Missing(buyer?.Address?.PostalZone))
        {
            findings.Add("BR-107-GST-SG", "BT-53", "buyer.address.postalZone", common.PostalZoneMissing(PartyRole.Buyer));
        }

        if (Findings.Missing(seller?.LegalRegistrationId))
        {
            findings.Add("BR-112-GST-SG", "BT-30", "seller.legalRegistrationId", text.SellerUenMissing);
        }

        if (InvoiceFacts.IsCreditNote(invoice) && Findings.Missing(invoice.Note))
        {
            findings.Add("BR-111-GST-SG", "BT-22", "note", text.CreditNoteReasonMissing);
        }

        if (invoice.CurrencyCode is { } currency && currency != TaxCurrency)
        {
            if (invoice.TaxCurrencyRate is not { } rate)
            {
                findings.Add("BR-113-GST-SG", "BT-6", "taxCurrencyRate", text.ExchangeRateMissing(currency));
            }
            else if (rate <= 0m)
            {
                findings.Add("BR-113-GST-SG", "BT-6", "taxCurrencyRate", text.ExchangeRateNotPositive);
            }
        }
    }

    /// <summary>NG: a supplier not registered for GST. Only NG on the invoice, and no GST numbers.</summary>
    private static void NotRegistered(CanonicalInvoiceDraft invoice, Findings findings, ISingaporeGstRuleText text)
    {
        var used = InvoiceFacts.CategoriesInUse(invoice).Select(u => u.Category).Distinct().ToList();
        if (!used.Contains("NG"))
        {
            return;
        }

        if (used.Count > 1)
        {
            findings.Add("BR-NG-12-GST-SG", "BT-151", "lines", text.NotRegisteredCannotBeCombined);
        }

        if (!Findings.Missing(invoice.Seller?.VatIdentifier))
        {
            findings.Add("BR-NG-02-GST-SG", "BT-31", "seller.vatIdentifier", text.NotRegisteredSellerGstNumber);
        }

        if (!Findings.Missing(invoice.Buyer?.VatIdentifier))
        {
            findings.Add("BR-NG-02-GST-SG", "BT-48", "buyer.vatIdentifier", text.NotRegisteredBuyerGstNumber);
        }
    }

    /// <summary>BR-CO-25-SG: something to pay needs a due date (xChange records no payment terms).</summary>
    private static void DueDate(CanonicalInvoiceDraft invoice, Findings findings, ISingaporeGstRuleText text)
    {
        if ((invoice.Totals?.PayableAmount ?? 0m) > 0m && Findings.Missing(invoice.DueDate))
        {
            findings.Add("BR-CO-25-SG", "BT-9", "dueDate", text.DueDateMissing);
        }
    }

    private static void Endpoint(CanonicalParty? party, string side, Findings findings, ISingaporeGstRuleText text)
    {
        if (party?.ElectronicAddressScheme == "0195"
            && !Findings.Missing(party.ElectronicAddress)
            && !UenEndpoint().IsMatch(party.ElectronicAddress!.Trim()))
        {
            findings.Add("XC-SG-0195", "BT-34", $"{side}.electronicAddress", text.UenEndpointShape);
        }
    }

    /// <summary>The PEPPOL participant pattern for 0195: SGUEN + an 8- or 9-digit or T/S/R-form UEN + check letter.</summary>
    [GeneratedRegex("^SGUEN([0-9]{8}|[0-9]{9}|[RST][0-9]{2}[A-Z]{2}[0-9]{4})[0-9A-Z]$", RegexOptions.IgnoreCase)]
    private static partial Regex UenEndpoint();
}
