using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Domain.EInvoicing;

/// <summary>
/// The EN 16931 core rules (CEN artefacts 1.3.16, as adopted by PEPPOL BIS
/// 3.0.21) that can be expressed on the canonical model. Shared by every
/// ruleset built on EN 16931.
///
/// Rules that hold by construction are not repeated here: the totals and the
/// VAT breakdown are calculated (BR-12..15, BR-45..48, BR-CO-10..18, BR-S-08
/// and the like), line ids are always written (BR-21), and the writer never
/// emits an element twice (UBL-SR-*).
/// </summary>
internal static class En16931CoreRules
{
    public static IReadOnlyList<RuleViolation> Check(CanonicalInvoiceDraft invoice)
    {
        var findings = new Findings();

        Header(invoice, findings);
        Parties(invoice, findings);
        Lines(invoice, findings);
        DocumentAllowancesAndCharges(invoice, findings);
        VatCategories(invoice, findings);
        Payment(invoice, findings);
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

    private static void Parties(CanonicalInvoiceDraft invoice, Findings findings)
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
                "Vermeld het btw-nummer of het KvK-nummer van de verkoper.");
        }

        VatPrefix(seller?.VatIdentifier, "seller.vatIdentifier", "BT-31", "verkoper", findings);
        VatPrefix(buyer?.VatIdentifier, "buyer.vatIdentifier", "BT-48", "afnemer", findings);

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

    private static void Lines(CanonicalInvoiceDraft invoice, Findings findings)
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
                findings.Add("BR-CO-04", "BT-151", $"{path}.vatCategoryCode", $"Regel {number}: btw-categorie ontbreekt.");
            }
            else if (!CodeLists.VatCategories.Contains(line.VatCategoryCode!))
            {
                findings.Add("BR-CL-18", "BT-151", $"{path}.vatCategoryCode",
                    $"Regel {number}: '{line.VatCategoryCode}' is geen btw-categorie.");
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
                    $"{kind} {number} op factuurniveau: btw-categorie ontbreekt.");
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

    private static void VatCategories(CanonicalInvoiceDraft invoice, Findings findings)
    {
        // Rates, per line and per document allowance or charge.
        for (var index = 0; index < invoice.Lines.Count; index++)
        {
            var line = invoice.Lines[index];
            Rate(line.VatCategoryCode, line.VatPercentage, CategorySource.Line,
                $"lines[{index}]", $"Regel {Findings.Number(index)}", findings);
        }

        for (var index = 0; index < invoice.AllowancesAndCharges.Count; index++)
        {
            var adjustment = invoice.AllowancesAndCharges[index];
            Rate(adjustment.VatCategoryCode, adjustment.VatPercentage,
                adjustment.IsCharge ? CategorySource.Charge : CategorySource.Allowance,
                $"allowancesAndCharges[{index}]",
                $"{(adjustment.IsCharge ? "Toeslag" : "Korting")} {Findings.Number(index)} op factuurniveau", findings);
        }

        // Identifiers each category requires (or, for O, forbids).
        var seller = invoice.Seller;
        var buyer = invoice.Buyer;
        var sellerHasVatOrTaxId = !Findings.Missing(seller?.VatIdentifier) || !Findings.Missing(seller?.TaxRegistrationId);
        var used = InvoiceFacts.CategoriesInUse(invoice).ToList();

        foreach (var (category, source) in used)
        {
            var suffix = source switch
            {
                CategorySource.Line => "02",
                CategorySource.Allowance => "03",
                _ => "04",
            };

            switch (category)
            {
                case "S" or "Z" or "E" when !sellerHasVatOrTaxId:
                    findings.Add($"BR-{category}-{suffix}", "BT-31", "seller.vatIdentifier",
                        $"Btw-nummer van de verkoper is verplicht bij btw-categorie {category}.");
                    break;

                case "AE":
                    if (!sellerHasVatOrTaxId)
                    {
                        findings.Add($"BR-AE-{suffix}", "BT-31", "seller.vatIdentifier",
                            "Btw-nummer van de verkoper is verplicht bij verlegde btw (AE).");
                    }

                    if (Findings.Missing(buyer?.VatIdentifier) && Findings.Missing(buyer?.LegalRegistrationId))
                    {
                        findings.Add($"BR-AE-{suffix}", "BT-48", "buyer.vatIdentifier",
                            "Btw-nummer of KvK-nummer van de afnemer is verplicht bij verlegde btw (AE).");
                    }

                    break;

                case "G" when Findings.Missing(seller?.VatIdentifier):
                    findings.Add($"BR-G-{suffix}", "BT-31", "seller.vatIdentifier",
                        "Btw-nummer van de verkoper is verplicht bij export buiten de EU (G).");
                    break;

                case "O":
                    if (!Findings.Missing(seller?.VatIdentifier))
                    {
                        findings.Add($"BR-O-{suffix}", "BT-31", "seller.vatIdentifier",
                            "Bij 'niet onderworpen aan btw' (O) mag het btw-nummer van de verkoper niet op de factuur staan.");
                    }

                    if (!Findings.Missing(buyer?.VatIdentifier))
                    {
                        findings.Add($"BR-O-{suffix}", "BT-48", "buyer.vatIdentifier",
                            "Bij 'niet onderworpen aan btw' (O) mag het btw-nummer van de afnemer niet op de factuur staan.");
                    }

                    break;
            }
        }

        if (used.Any(u => u.Category == "O") && used.Any(u => u.Category != "O"))
        {
            findings.Add("BR-O-12", "BT-151", "lines",
                "Btw-categorie O (niet onderworpen aan btw) kan niet samen met andere btw-categorieën op één factuur.");
        }

        // Exemption reasons in the calculated breakdown.
        for (var index = 0; index < invoice.VatBreakdown.Count; index++)
        {
            var bucket = invoice.VatBreakdown[index];
            var path = $"vatBreakdown[{index}].exemptionReason";
            var hasReason = !Findings.Missing(bucket.ExemptionReason);

            switch (bucket.VatCategoryCode)
            {
                case "S" or "Z" when hasReason:
                    findings.Add($"BR-{bucket.VatCategoryCode}-10", "BT-120", path,
                        $"Bij btw-categorie {bucket.VatCategoryCode} hoort geen vrijstellingsreden.");
                    break;

                case "E" or "AE" or "G" or "O" when !hasReason:
                    findings.Add($"BR-{bucket.VatCategoryCode}-10", "BT-120", path,
                        $"Vrijstellingsreden ontbreekt voor btw-categorie {bucket.VatCategoryCode}.");
                    break;
            }
        }
    }

    /// <summary>
    /// The rate each category requires. O has no rate at all; the writer leaves
    /// it out, so it is not something the reviewer needs to fix.
    /// </summary>
    private static void Rate(
        string? category, decimal? rate, CategorySource source, string path, string label, Findings findings)
    {
        var suffix = source switch
        {
            CategorySource.Line => "05",
            CategorySource.Allowance => "06",
            _ => "07",
        };

        switch (category)
        {
            case "S" when rate is not > 0m:
                findings.Add($"BR-S-{suffix}", "BT-152", $"{path}.vatPercentage",
                    $"{label}: btw-categorie S (standaardtarief) vraagt een btw-percentage boven nul.");
                break;

            case "Z" or "E" or "AE" or "G" when rate is { } actual && actual != 0m:
                findings.Add($"BR-{category}-{suffix}", "BT-152", $"{path}.vatPercentage",
                    $"{label}: btw-categorie {category} vraagt btw-percentage 0.");
                break;

            case "K":
                findings.Add("XC-VAT-UNSUPPORTED", "BT-151", $"{path}.vatCategoryCode",
                    $"{label}: btw-categorie K (intracommunautaire levering) vraagt een leverdatum en leveringsland, "
                    + "die xChange nog niet vastlegt.");
                break;

            case "L" or "M" or "B":
                findings.Add("XC-VAT-UNSUPPORTED", "BT-151", $"{path}.vatCategoryCode",
                    $"{label}: btw-categorie {category} wordt niet ondersteund.");
                break;
        }
    }

    private static void Payment(CanonicalInvoiceDraft invoice, Findings findings)
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

        if (!CodeLists.IsPaymentMeansCode(payment.PaymentMeansCode))
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
