using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Domain.EInvoicing;

/// <summary>
/// The structural rules every ruleset in xChange shares: what EN 16931 calls
/// BR-02 to BR-63 and the decimals rules, and what PINT carries over from it
/// as ibr-*. Rule ids are the EN 16931 ones. Where rulesets differ - the tax
/// categories, the payment codes, whether a tax number starts with a country
/// prefix - the <see cref="TaxRegime"/> says how.
///
/// Rules that hold by construction are not repeated here: the totals and the
/// tax breakdown are calculated (BR-12..15, BR-45..48, BR-CO-10..18), line ids
/// are always written (BR-21), and the writer never emits an element twice
/// (UBL-SR-*).
/// </summary>
internal static class CommonRules
{
    public static IReadOnlyList<RuleViolation> Check(CanonicalInvoiceDraft invoice, TaxRegime regime)
    {
        var findings = new Findings();

        Header(invoice, findings);
        Parties(invoice, regime, findings);
        Lines(invoice, regime, findings);
        DocumentAllowancesAndCharges(invoice, findings);
        Payment(invoice, regime, findings);
        Totals(invoice, findings);

        return findings.Violations;
    }

    private static void Header(CanonicalInvoiceDraft invoice, Findings findings)
    {
        if (Findings.Missing(invoice.InvoiceNumber))
        {
            findings.Add("BR-02", "BT-1", "invoiceNumber", "Factuurnummer ontbreekt.");
        }

        if (Findings.Missing(invoice.IssueDate))
        {
            findings.Add("BR-03", "BT-2", "issueDate", "Factuurdatum ontbreekt.");
        }

        if (Findings.Missing(invoice.TypeCode))
        {
            findings.Add("BR-04", "BT-3", "typeCode", "Factuurtype ontbreekt.");
        }

        if (Findings.Missing(invoice.CurrencyCode))
        {
            findings.Add("BR-05", "BT-5", "currencyCode", "Valuta ontbreekt.");
        }
        else if (!CodeLists.IsCurrencyCode(invoice.CurrencyCode))
        {
            findings.Add("BR-CL-04", "BT-5", "currencyCode",
                $"Valuta '{invoice.CurrencyCode}' is geen ISO 4217-code van drie hoofdletters, zoals EUR.");
        }
    }

    private static void Parties(CanonicalInvoiceDraft invoice, TaxRegime regime, Findings findings)
    {
        var seller = invoice.Seller;
        var buyer = invoice.Buyer;

        if (Findings.Missing(seller?.Name))
        {
            findings.Add("BR-06", "BT-27", "seller.name", "Naam van de verkoper ontbreekt.");
        }

        if (Findings.Missing(buyer?.Name))
        {
            findings.Add("BR-07", "BT-44", "buyer.name", "Naam van de afnemer ontbreekt.");
        }

        Country(seller, "seller", "BR-09", "BT-40", "verkoper", findings);
        Country(buyer, "buyer", "BR-11", "BT-55", "afnemer", findings);

        if (Findings.Missing(seller?.VatIdentifier) && Findings.Missing(seller?.LegalRegistrationId))
        {
            findings.Add("BR-CO-26", "BT-31", "seller.vatIdentifier",
                $"Vermeld het {regime.TaxName}-nummer of het registratienummer van de verkoper.");
        }

        if (regime.TaxNumbersHaveCountryPrefix)
        {
            VatPrefix(seller?.VatIdentifier, "seller.vatIdentifier", "BT-31", "verkoper", findings);
            VatPrefix(buyer?.VatIdentifier, "buyer.vatIdentifier", "BT-48", "afnemer", findings);
        }

        EndpointScheme(seller, "seller", "BR-62", "BT-34", "verkoper", findings);
        EndpointScheme(buyer, "buyer", "BR-63", "BT-49", "afnemer", findings);

        RegistrationScheme(seller, "seller", "BT-30", "verkoper", findings);
        RegistrationScheme(buyer, "buyer", "BT-47", "afnemer", findings);
    }

    private static void Country(
        CanonicalParty? party, string side, string ruleId, string businessTerm, string label, Findings findings)
    {
        var code = party?.Address?.CountryCode;

        if (Findings.Missing(code))
        {
            findings.Add(ruleId, businessTerm, $"{side}.address.countryCode", $"Landcode van de {label} ontbreekt.");
        }
        else if (!CodeLists.IsCountryCode(code))
        {
            findings.Add("BR-CL-14", businessTerm, $"{side}.address.countryCode",
                $"Landcode van de {label} '{code}' is geen ISO 3166-code van twee hoofdletters, zoals NL.");
        }
    }

    private static void VatPrefix(string? vat, string path, string businessTerm, string label, Findings findings)
    {
        if (!Findings.Missing(vat) && !(vat!.Length > 2 && char.IsAsciiLetterUpper(vat[0]) && char.IsAsciiLetterUpper(vat[1])))
        {
            findings.Add("BR-CO-09", businessTerm, path,
                $"Btw-nummer van de {label} moet beginnen met een landcode, zoals NL.");
        }
    }

    private static void EndpointScheme(
        CanonicalParty? party, string side, string ruleId, string businessTerm, string label, Findings findings)
    {
        if (Findings.Missing(party?.ElectronicAddress))
        {
            return;
        }

        if (Findings.Missing(party!.ElectronicAddressScheme))
        {
            findings.Add(ruleId, businessTerm, $"{side}.electronicAddressScheme",
                $"Kies het schema van het elektronisch adres van de {label}.");
        }
        else if (!CodeLists.IsElectronicAddressScheme(party.ElectronicAddressScheme))
        {
            findings.Add("BR-CL-25", businessTerm, $"{side}.electronicAddressScheme",
                $"'{party.ElectronicAddressScheme}' is geen geldig schema voor een elektronisch adres.");
        }
    }

    private static void RegistrationScheme(
        CanonicalParty? party, string side, string businessTerm, string label, Findings findings)
    {
        if (!Findings.Missing(party?.LegalRegistrationId)
            && !Findings.Missing(party!.LegalRegistrationScheme)
            && !CodeLists.IsRegistrationScheme(party.LegalRegistrationScheme))
        {
            findings.Add("BR-CL-11", businessTerm, $"{side}.legalRegistrationScheme",
                $"Schema van het registratienummer van de {label} moet een code van vier cijfers zijn, zoals 0106 (KvK).");
        }
    }

    private static void Lines(CanonicalInvoiceDraft invoice, TaxRegime regime, Findings findings)
    {
        if (invoice.Lines.Count == 0)
        {
            findings.Add("BR-16", "BG-25", "lines", "De factuur heeft geen regels.");
            return;
        }

        for (var index = 0; index < invoice.Lines.Count; index++)
        {
            var line = invoice.Lines[index];
            var path = $"lines[{index}]";
            var number = Findings.Number(index);

            if (line.Quantity is null)
            {
                findings.Add("BR-22", "BT-129", $"{path}.quantity", $"Regel {number}: aantal ontbreekt.");
            }

            if (Findings.Missing(line.UnitCode))
            {
                findings.Add("BR-23", "BT-130", $"{path}.unitCode", $"Regel {number}: eenheid ontbreekt.");
            }
            else if (!CodeLists.IsUnitCode(line.UnitCode))
            {
                findings.Add("BR-CL-23", "BT-130", $"{path}.unitCode",
                    $"Regel {number}: eenheid '{line.UnitCode}' is geen UN/ECE-code, zoals C62 (stuk) of HUR (uur).");
            }

            // BT-153: the description is the item name (see the design).
            if (Findings.Missing(line.Description))
            {
                findings.Add("BR-25", "BT-153", $"{path}.description",
                    $"Regel {number}: omschrijving ontbreekt. Die wordt de artikelnaam.");
            }

            if (line.UnitPrice is null)
            {
                findings.Add("BR-26", "BT-146", $"{path}.unitPrice", $"Regel {number}: prijs ontbreekt.");
            }
            else if (line.UnitPrice < 0m)
            {
                findings.Add("BR-27", "BT-146", $"{path}.unitPrice",
                    $"Regel {number}: de prijs mag niet negatief zijn. Gebruik een negatief aantal.");
            }

            if (Findings.HasMoreThanTwoDecimals(line.NetAmount))
            {
                findings.Add("BR-DEC-23", "BT-131", $"{path}.netAmount",
                    $"Regel {number}: het nettobedrag mag hoogstens twee decimalen hebben.");
            }

            if (Findings.Missing(line.VatCategoryCode))
            {
                findings.Add("BR-CO-04", "BT-151", $"{path}.vatCategoryCode",
                    $"Regel {number}: {regime.TaxName}-categorie ontbreekt.");
            }
            else if (!regime.Categories.Contains(line.VatCategoryCode!))
            {
                findings.Add("BR-CL-18", "BT-151", $"{path}.vatCategoryCode",
                    $"Regel {number}: '{line.VatCategoryCode}' is geen {regime.TaxName}-categorie.");
            }

            for (var adjustmentIndex = 0; adjustmentIndex < line.AllowancesAndCharges.Count; adjustmentIndex++)
            {
                LineAdjustment(line.AllowancesAndCharges[adjustmentIndex], $"{path}.allowancesAndCharges[{adjustmentIndex}]", number, findings);
            }
        }
    }

    private static void LineAdjustment(CanonicalAllowanceCharge adjustment, string path, string lineNumber, Findings findings)
    {
        var kind = adjustment.IsCharge ? "toeslag" : "korting";

        if (adjustment.Amount is null)
        {
            findings.Add(adjustment.IsCharge ? "BR-43" : "BR-41", adjustment.IsCharge ? "BT-141" : "BT-136",
                $"{path}.amount", $"Regel {lineNumber}: bedrag van de {kind} ontbreekt.");
        }

        if (Findings.Missing(adjustment.Reason) && Findings.Missing(adjustment.ReasonCode))
        {
            findings.Add(adjustment.IsCharge ? "BR-44" : "BR-42", adjustment.IsCharge ? "BT-144" : "BT-139",
                $"{path}.reason", $"Regel {lineNumber}: reden van de {kind} ontbreekt.");
        }

        if (Findings.HasMoreThanTwoDecimals(adjustment.Amount))
        {
            findings.Add(adjustment.IsCharge ? "BR-DEC-27" : "BR-DEC-24", null, $"{path}.amount",
                $"Regel {lineNumber}: bedrag van de {kind} mag hoogstens twee decimalen hebben.");
        }

        if (Findings.HasMoreThanTwoDecimals(adjustment.BaseAmount))
        {
            findings.Add(adjustment.IsCharge ? "BR-DEC-28" : "BR-DEC-25", null, $"{path}.baseAmount",
                $"Regel {lineNumber}: grondslag van de {kind} mag hoogstens twee decimalen hebben.");
        }
    }

    private static void DocumentAllowancesAndCharges(CanonicalInvoiceDraft invoice, Findings findings)
    {
        for (var index = 0; index < invoice.AllowancesAndCharges.Count; index++)
        {
            var adjustment = invoice.AllowancesAndCharges[index];
            var path = $"allowancesAndCharges[{index}]";
            var isCharge = adjustment.IsCharge;
            var kind = isCharge ? "Toeslag" : "Korting";
            var number = Findings.Number(index);

            if (adjustment.Amount is null)
            {
                findings.Add(isCharge ? "BR-36" : "BR-31", isCharge ? "BT-99" : "BT-92", $"{path}.amount",
                    $"{kind} {number} op factuurniveau: bedrag ontbreekt.");
            }

            if (Findings.Missing(adjustment.VatCategoryCode))
            {
                findings.Add(isCharge ? "BR-37" : "BR-32", isCharge ? "BT-102" : "BT-95", $"{path}.vatCategoryCode",
                    $"{kind} {number} op factuurniveau: belastingcategorie ontbreekt.");
            }

            if (Findings.Missing(adjustment.Reason) && Findings.Missing(adjustment.ReasonCode))
            {
                findings.Add(isCharge ? "BR-38" : "BR-33", isCharge ? "BT-104" : "BT-97", $"{path}.reason",
                    $"{kind} {number} op factuurniveau: reden ontbreekt.");
            }

            if (Findings.HasMoreThanTwoDecimals(adjustment.Amount))
            {
                findings.Add(isCharge ? "BR-DEC-05" : "BR-DEC-01", null, $"{path}.amount",
                    $"{kind} {number} op factuurniveau: bedrag mag hoogstens twee decimalen hebben.");
            }

            if (Findings.HasMoreThanTwoDecimals(adjustment.BaseAmount))
            {
                findings.Add(isCharge ? "BR-DEC-06" : "BR-DEC-02", null, $"{path}.baseAmount",
                    $"{kind} {number} op factuurniveau: grondslag mag hoogstens twee decimalen hebben.");
            }
        }
    }

    private static void Payment(CanonicalInvoiceDraft invoice, TaxRegime regime, Findings findings)
    {
        if (!InvoiceFacts.HasPayment(invoice))
        {
            return;
        }

        var payment = invoice.Payment!;

        if (Findings.Missing(payment.PaymentMeansCode))
        {
            findings.Add("BR-49", "BT-81", "payment.paymentMeansCode", "Kies een betaalwijze.");
            return;
        }

        if (!regime.IsPaymentMeansCode(payment.PaymentMeansCode))
        {
            findings.Add("BR-CL-16", "BT-81", "payment.paymentMeansCode",
                $"'{payment.PaymentMeansCode}' is geen geldige betaalwijze.");
        }

        if (CodeLists.IsCreditTransfer(payment.PaymentMeansCode) && Findings.Missing(payment.Iban))
        {
            findings.Add("BR-61", "BT-84", "payment.iban", "IBAN is verplicht bij betaling per overschrijving.");
        }
    }

    private static void Totals(CanonicalInvoiceDraft invoice, Findings findings)
    {
        if (Findings.HasMoreThanTwoDecimals(invoice.Totals?.PrepaidAmount))
        {
            findings.Add("BR-DEC-16", "BT-113", "totals.prepaidAmount", "Vooruitbetaald bedrag mag hoogstens twee decimalen hebben.");
        }

        if (Findings.HasMoreThanTwoDecimals(invoice.Totals?.RoundingAmount))
        {
            findings.Add("BR-DEC-17", "BT-114", "totals.roundingAmount", "Afrondingsbedrag mag hoogstens twee decimalen hebben.");
        }
    }
}
