using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Domain.EInvoicing.Nlcius;

/// <summary>
/// The Dutch rules of NLCIUS as SI-UBL 2.0.3.13 checks them (BR-NL-*). Like
/// the official rules they apply when the seller is in the Netherlands, by
/// postal address. BR-NL-5 (tax representative) and BR-NL-13 (order line
/// references) concern data the canonical model does not carry; BR-NL-8 holds
/// by construction (a 381 is written as a CreditNote).
/// </summary>
internal static class NlciusRules
{
    /// <summary>BR-NL-7: the document types SI-UBL 2.0 accepts.</summary>
    private static readonly IReadOnlySet<string> TypeCodes = new HashSet<string>(StringComparer.Ordinal) { "380", "381", "384", "389" };

    public static IReadOnlyList<RuleViolation> Check(CanonicalInvoiceDraft invoice)
    {
        var findings = new Findings();
        if (!InvoiceFacts.SellerIsDutch(invoice))
        {
            return findings.Violations;
        }

        var text = NlciusRuleText.Current;
        var common = CommonRuleText.Current;

        LegalRegistration(invoice.Seller, "seller", "BR-NL-1", "BT-30", PartyRole.Seller, findings, text, common);

        if (Findings.Missing(invoice.BuyerReference) && Findings.Missing(invoice.PurchaseOrderReference))
        {
            findings.Add("BR-NL-2", "BT-10", "buyerReference", text.BuyerReferenceMissing);
        }

        Address(invoice.Seller, "seller", "BR-NL-3", PartyRole.Seller, findings, common);

        if (InvoiceFacts.BuyerIsDutch(invoice))
        {
            Address(invoice.Buyer, "buyer", "BR-NL-4", PartyRole.Buyer, findings, common);
            LegalRegistration(invoice.Buyer, "buyer", "BR-NL-10", "BT-47", PartyRole.Buyer, findings, text, common);
        }

        if (!Findings.Missing(invoice.TypeCode) && !TypeCodes.Contains(invoice.TypeCode!))
        {
            findings.Add("BR-NL-7", "BT-3", "typeCode", text.TypeCodeNotAllowed);
        }

        if (invoice.TypeCode == "384" && Findings.Missing(invoice.PrecedingInvoiceReference))
        {
            findings.Add("BR-NL-9", "BT-25", "precedingInvoiceReference", text.CorrectedInvoiceNeedsPrecedingInvoice);
        }

        Payment(invoice, findings, text, common);

        return findings.Violations;
    }

    /// <summary>BR-NL-1 and BR-NL-10: a KvK or OIN number, with its scheme.</summary>
    private static void LegalRegistration(
        CanonicalParty? party, string side, string ruleId, string businessTerm, PartyRole role, Findings findings,
        INlciusRuleText text, ICommonRuleText common)
    {
        if (Findings.Missing(party?.LegalRegistrationId))
        {
            findings.Add(ruleId, businessTerm, $"{side}.legalRegistrationId", text.RegistrationMissing(role));
        }
        else if (!CodeLists.DutchRegistrationSchemes.Contains(party!.LegalRegistrationScheme ?? string.Empty))
        {
            findings.Add(ruleId, businessTerm, $"{side}.legalRegistrationScheme", common.DutchRegistrationSchemeRequired(role));
        }
    }

    private static void Address(CanonicalParty? party, string side, string ruleId, PartyRole role, Findings findings, ICommonRuleText text)
    {
        var address = party?.Address;

        if (Findings.Missing(address?.StreetName))
        {
            findings.Add(ruleId, null, $"{side}.address.streetName", text.StreetMissing(role));
        }

        if (Findings.Missing(address?.CityName))
        {
            findings.Add(ruleId, null, $"{side}.address.cityName", text.CityMissing(role));
        }

        if (Findings.Missing(address?.PostalZone))
        {
            findings.Add(ruleId, null, $"{side}.address.postalZone", text.PostalZoneMissing(role));
        }
    }

    private static void Payment(CanonicalInvoiceDraft invoice, Findings findings, INlciusRuleText text, ICommonRuleText common)
    {
        // BR-NL-11 is checked on invoices only (its context is ubl:Invoice).
        if (!InvoiceFacts.IsCreditNote(invoice) && (invoice.Totals?.PayableAmount ?? 0m) > 0m && !InvoiceFacts.HasPayment(invoice))
        {
            findings.Add("BR-NL-11", "BT-81", "payment.paymentMeansCode", common.PaymentMeansNeeded);
        }

        var code = invoice.Payment?.PaymentMeansCode;
        if (!Findings.Missing(code) && !CodeLists.DutchPaymentMeansCodes.Contains(code!.Trim()))
        {
            findings.Add("BR-NL-12", "BT-81", "payment.paymentMeansCode", text.PaymentMeansNotAllowed);
        }
    }
}
