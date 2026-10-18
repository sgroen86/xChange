using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Domain.EInvoicing.Peppol;

/// <summary>
/// The Dutch national rules in PEPPOL BIS 3.0.21. They apply when the seller's
/// postal address is in the Netherlands - the address, not the VAT prefix.
///
/// NL-R-006 (tax representative) and NL-R-009 (order line references) concern
/// data the canonical model does not carry, so they cannot fire.
/// </summary>
internal static class DutchRules
{
    public static IReadOnlyList<RuleViolation> Check(CanonicalInvoiceDraft invoice)
    {
        var findings = new Findings();
        if (!InvoiceFacts.SellerIsDutch(invoice))
        {
            return findings.Violations;
        }

        var buyerIsDutch = InvoiceFacts.BuyerIsDutch(invoice);
        var text = PeppolRuleText.Current;
        var common = CommonRuleText.Current;

        if (InvoiceFacts.IsCreditNote(invoice) && Findings.Missing(invoice.PrecedingInvoiceReference))
        {
            findings.Add("NL-R-001", "BT-25", "precedingInvoiceReference", text.CreditNoteNeedsPrecedingInvoice);
        }

        Address(invoice.Seller, "seller", "NL-R-002", PartyRole.Seller, findings, common);
        RegistrationScheme(invoice.Seller, "seller", "NL-R-003", "BT-30", PartyRole.Seller, findings, common);

        if (buyerIsDutch)
        {
            Address(invoice.Buyer, "buyer", "NL-R-004", PartyRole.Buyer, findings, common);
            RegistrationScheme(invoice.Buyer, "buyer", "NL-R-005", "BT-47", PartyRole.Buyer, findings, common);
        }

        PaymentMeans(invoice, buyerIsDutch, findings, text, common);

        return findings.Violations;
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

    /// <summary>Only when a registration number is given: then it must be KvK or OIN.</summary>
    private static void RegistrationScheme(
        CanonicalParty? party, string side, string ruleId, string businessTerm, PartyRole role, Findings findings, ICommonRuleText text)
    {
        if (!Findings.Missing(party?.LegalRegistrationId)
            && !CodeLists.DutchRegistrationSchemes.Contains(party!.LegalRegistrationScheme ?? string.Empty))
        {
            findings.Add(ruleId, businessTerm, $"{side}.legalRegistrationScheme", text.DutchRegistrationSchemeRequired(role));
        }
    }

    private static void PaymentMeans(
        CanonicalInvoiceDraft invoice, bool buyerIsDutch, Findings findings, IPeppolRuleText text, ICommonRuleText common)
    {
        var payable = invoice.Totals?.PayableAmount ?? 0m;
        var somethingToPay = InvoiceFacts.IsCreditNote(invoice) ? payable < 0m : payable > 0m;

        // With partial payment details BR-49 already asks for the code.
        if (somethingToPay && !InvoiceFacts.HasPayment(invoice))
        {
            findings.Add("NL-R-007", "BT-81", "payment.paymentMeansCode", common.PaymentMeansNeeded);
        }

        var code = invoice.Payment?.PaymentMeansCode;
        if (buyerIsDutch && !Findings.Missing(code) && !CodeLists.DutchPaymentMeansCodes.Contains(code!))
        {
            findings.Add("NL-R-008", "BT-81", "payment.paymentMeansCode", text.DutchPaymentMeansOnly);
        }
    }
}
