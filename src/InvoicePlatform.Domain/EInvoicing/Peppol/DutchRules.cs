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

        if (InvoiceFacts.IsCreditNote(invoice) && Findings.Missing(invoice.PrecedingInvoiceReference))
        {
            findings.Add("NL-R-001", "BT-25", "precedingInvoiceReference",
                "Een creditnota moet verwijzen naar de oorspronkelijke factuur.");
        }

        Address(invoice.Seller, "seller", "NL-R-002", "verkoper", findings);
        RegistrationScheme(invoice.Seller, "seller", "NL-R-003", "BT-30", "verkoper", findings);

        if (buyerIsDutch)
        {
            Address(invoice.Buyer, "buyer", "NL-R-004", "afnemer", findings);
            RegistrationScheme(invoice.Buyer, "buyer", "NL-R-005", "BT-47", "afnemer", findings);
        }

        PaymentMeans(invoice, buyerIsDutch, findings);

        return findings.Violations;
    }

    private static void Address(CanonicalParty? party, string side, string ruleId, string label, Findings findings)
    {
        var address = party?.Address;

        if (Findings.Missing(address?.StreetName))
        {
            findings.Add(ruleId, null, $"{side}.address.streetName", $"Straat van de {label} ontbreekt.");
        }

        if (Findings.Missing(address?.CityName))
        {
            findings.Add(ruleId, null, $"{side}.address.cityName", $"Plaats van de {label} ontbreekt.");
        }

        if (Findings.Missing(address?.PostalZone))
        {
            findings.Add(ruleId, null, $"{side}.address.postalZone", $"Postcode van de {label} ontbreekt.");
        }
    }

    /// <summary>Only when a registration number is given: then it must be KvK or OIN.</summary>
    private static void RegistrationScheme(
        CanonicalParty? party, string side, string ruleId, string businessTerm, string label, Findings findings)
    {
        if (!Findings.Missing(party?.LegalRegistrationId)
            && !CodeLists.DutchRegistrationSchemes.Contains(party!.LegalRegistrationScheme ?? string.Empty))
        {
            findings.Add(ruleId, businessTerm, $"{side}.legalRegistrationScheme",
                $"Kies KvK (0106) of OIN (0190) als schema van het registratienummer van de {label}.");
        }
    }

    private static void PaymentMeans(CanonicalInvoiceDraft invoice, bool buyerIsDutch, Findings findings)
    {
        var payable = invoice.Totals?.PayableAmount ?? 0m;
        var somethingToPay = InvoiceFacts.IsCreditNote(invoice) ? payable < 0m : payable > 0m;

        // With partial payment details BR-49 already asks for the code.
        if (somethingToPay && !InvoiceFacts.HasPayment(invoice))
        {
            findings.Add("NL-R-007", "BT-81", "payment.paymentMeansCode",
                "Geef een betaalwijze op: er staat een bedrag open.");
        }

        var code = invoice.Payment?.PaymentMeansCode;
        if (buyerIsDutch && !Findings.Missing(code) && !CodeLists.DutchPaymentMeansCodes.Contains(code!))
        {
            findings.Add("NL-R-008", "BT-81", "payment.paymentMeansCode",
                "Tussen Nederlandse partijen is de betaalwijze 30, 48, 49, 57, 58 of 59.");
        }
    }
}
