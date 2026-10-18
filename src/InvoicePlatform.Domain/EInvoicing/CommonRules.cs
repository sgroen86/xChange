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
        var text = CommonRuleText.Current;

        Header(invoice, findings, text);
        Parties(invoice, regime, findings, text);
        Lines(invoice, regime, findings, text);
        DocumentAllowancesAndCharges(invoice, findings, text);
        Payment(invoice, regime, findings, text);
        Totals(invoice, findings, text);

        return findings.Violations;
    }

    private static void Header(CanonicalInvoiceDraft invoice, Findings findings, ICommonRuleText text)
    {
        if (Findings.Missing(invoice.InvoiceNumber))
        {
            findings.Add("BR-02", "BT-1", "invoiceNumber", text.InvoiceNumberMissing);
        }

        if (Findings.Missing(invoice.IssueDate))
        {
            findings.Add("BR-03", "BT-2", "issueDate", text.IssueDateMissing);
        }

        if (Findings.Missing(invoice.TypeCode))
        {
            findings.Add("BR-04", "BT-3", "typeCode", text.TypeCodeMissing);
        }

        if (Findings.Missing(invoice.CurrencyCode))
        {
            findings.Add("BR-05", "BT-5", "currencyCode", text.CurrencyMissing);
        }
        else if (!CodeLists.IsCurrencyCode(invoice.CurrencyCode))
        {
            findings.Add("BR-CL-04", "BT-5", "currencyCode", text.CurrencyInvalid(invoice.CurrencyCode));
        }
    }

    private static void Parties(CanonicalInvoiceDraft invoice, TaxRegime regime, Findings findings, ICommonRuleText text)
    {
        var seller = invoice.Seller;
        var buyer = invoice.Buyer;

        if (Findings.Missing(seller?.Name))
        {
            findings.Add("BR-06", "BT-27", "seller.name", text.SellerNameMissing);
        }

        if (Findings.Missing(buyer?.Name))
        {
            findings.Add("BR-07", "BT-44", "buyer.name", text.BuyerNameMissing);
        }

        Country(seller, "seller", "BR-09", "BT-40", PartyRole.Seller, findings, text);
        Country(buyer, "buyer", "BR-11", "BT-55", PartyRole.Buyer, findings, text);

        if (Findings.Missing(seller?.VatIdentifier) && Findings.Missing(seller?.LegalRegistrationId))
        {
            findings.Add("BR-CO-26", "BT-31", "seller.vatIdentifier", text.TaxNumberOrRegistrationMissing(regime.Tax));
        }

        if (regime.TaxNumbersHaveCountryPrefix)
        {
            VatPrefix(seller?.VatIdentifier, "seller.vatIdentifier", "BT-31", PartyRole.Seller, findings, text);
            VatPrefix(buyer?.VatIdentifier, "buyer.vatIdentifier", "BT-48", PartyRole.Buyer, findings, text);
        }

        EndpointScheme(seller, "seller", "BR-62", "BT-34", PartyRole.Seller, findings, text);
        EndpointScheme(buyer, "buyer", "BR-63", "BT-49", PartyRole.Buyer, findings, text);

        RegistrationScheme(seller, "seller", "BT-30", PartyRole.Seller, findings, text);
        RegistrationScheme(buyer, "buyer", "BT-47", PartyRole.Buyer, findings, text);
    }

    private static void Country(
        CanonicalParty? party, string side, string ruleId, string businessTerm, PartyRole role, Findings findings, ICommonRuleText text)
    {
        var code = party?.Address?.CountryCode;

        if (Findings.Missing(code))
        {
            findings.Add(ruleId, businessTerm, $"{side}.address.countryCode", text.CountryMissing(role));
        }
        else if (!CodeLists.IsCountryCode(code))
        {
            findings.Add("BR-CL-14", businessTerm, $"{side}.address.countryCode", text.CountryInvalid(role, code));
        }
    }

    private static void VatPrefix(string? vat, string path, string businessTerm, PartyRole role, Findings findings, ICommonRuleText text)
    {
        if (!Findings.Missing(vat) && !(vat!.Length > 2 && char.IsAsciiLetterUpper(vat[0]) && char.IsAsciiLetterUpper(vat[1])))
        {
            findings.Add("BR-CO-09", businessTerm, path, text.VatPrefixMissing(role));
        }
    }

    private static void EndpointScheme(
        CanonicalParty? party, string side, string ruleId, string businessTerm, PartyRole role, Findings findings, ICommonRuleText text)
    {
        if (Findings.Missing(party?.ElectronicAddress))
        {
            return;
        }

        if (Findings.Missing(party!.ElectronicAddressScheme))
        {
            findings.Add(ruleId, businessTerm, $"{side}.electronicAddressScheme", text.EndpointSchemeMissing(role));
        }
        else if (!CodeLists.IsElectronicAddressScheme(party.ElectronicAddressScheme))
        {
            findings.Add("BR-CL-25", businessTerm, $"{side}.electronicAddressScheme",
                text.EndpointSchemeInvalid(party.ElectronicAddressScheme));
        }
    }

    private static void RegistrationScheme(
        CanonicalParty? party, string side, string businessTerm, PartyRole role, Findings findings, ICommonRuleText text)
    {
        if (!Findings.Missing(party?.LegalRegistrationId)
            && !Findings.Missing(party!.LegalRegistrationScheme)
            && !CodeLists.IsRegistrationScheme(party.LegalRegistrationScheme))
        {
            findings.Add("BR-CL-11", businessTerm, $"{side}.legalRegistrationScheme", text.RegistrationSchemeInvalid(role));
        }
    }

    private static void Lines(CanonicalInvoiceDraft invoice, TaxRegime regime, Findings findings, ICommonRuleText text)
    {
        if (invoice.Lines.Count == 0)
        {
            findings.Add("BR-16", "BG-25", "lines", text.NoLines);
            return;
        }

        for (var index = 0; index < invoice.Lines.Count; index++)
        {
            var line = invoice.Lines[index];
            var path = $"lines[{index}]";
            var number = Findings.Number(index);

            if (line.Quantity is null)
            {
                findings.Add("BR-22", "BT-129", $"{path}.quantity", text.LineQuantityMissing(number));
            }

            if (Findings.Missing(line.UnitCode))
            {
                findings.Add("BR-23", "BT-130", $"{path}.unitCode", text.LineUnitMissing(number));
            }
            else if (!CodeLists.IsUnitCode(line.UnitCode))
            {
                findings.Add("BR-CL-23", "BT-130", $"{path}.unitCode", text.LineUnitInvalid(number, line.UnitCode));
            }

            // BT-153: the description is the item name (see the design).
            if (Findings.Missing(line.Description))
            {
                findings.Add("BR-25", "BT-153", $"{path}.description", text.LineDescriptionMissing(number));
            }

            if (line.UnitPrice is null)
            {
                findings.Add("BR-26", "BT-146", $"{path}.unitPrice", text.LinePriceMissing(number));
            }
            else if (line.UnitPrice < 0m)
            {
                findings.Add("BR-27", "BT-146", $"{path}.unitPrice", text.LinePriceNegative(number));
            }

            if (Findings.HasMoreThanTwoDecimals(line.NetAmount))
            {
                findings.Add("BR-DEC-23", "BT-131", $"{path}.netAmount", text.LineNetAmountDecimals(number));
            }

            if (Findings.Missing(line.VatCategoryCode))
            {
                findings.Add("BR-CO-04", "BT-151", $"{path}.vatCategoryCode", text.LineTaxCategoryMissing(number, regime.Tax));
            }
            else if (!regime.Categories.Contains(line.VatCategoryCode!))
            {
                findings.Add("BR-CL-18", "BT-151", $"{path}.vatCategoryCode",
                    text.LineTaxCategoryInvalid(number, line.VatCategoryCode, regime.Tax));
            }

            for (var adjustmentIndex = 0; adjustmentIndex < line.AllowancesAndCharges.Count; adjustmentIndex++)
            {
                LineAdjustment(line.AllowancesAndCharges[adjustmentIndex], $"{path}.allowancesAndCharges[{adjustmentIndex}]", number, findings, text);
            }
        }
    }

    private static void LineAdjustment(
        CanonicalAllowanceCharge adjustment, string path, string lineNumber, Findings findings, ICommonRuleText text)
    {
        var isCharge = adjustment.IsCharge;

        if (adjustment.Amount is null)
        {
            findings.Add(isCharge ? "BR-43" : "BR-41", isCharge ? "BT-141" : "BT-136",
                $"{path}.amount", text.LineAdjustmentAmountMissing(lineNumber, isCharge));
        }

        if (Findings.Missing(adjustment.Reason) && Findings.Missing(adjustment.ReasonCode))
        {
            findings.Add(isCharge ? "BR-44" : "BR-42", isCharge ? "BT-144" : "BT-139",
                $"{path}.reason", text.LineAdjustmentReasonMissing(lineNumber, isCharge));
        }

        if (Findings.HasMoreThanTwoDecimals(adjustment.Amount))
        {
            findings.Add(isCharge ? "BR-DEC-27" : "BR-DEC-24", null, $"{path}.amount",
                text.LineAdjustmentAmountDecimals(lineNumber, isCharge));
        }

        if (Findings.HasMoreThanTwoDecimals(adjustment.BaseAmount))
        {
            findings.Add(isCharge ? "BR-DEC-28" : "BR-DEC-25", null, $"{path}.baseAmount",
                text.LineAdjustmentBaseDecimals(lineNumber, isCharge));
        }
    }

    private static void DocumentAllowancesAndCharges(CanonicalInvoiceDraft invoice, Findings findings, ICommonRuleText text)
    {
        for (var index = 0; index < invoice.AllowancesAndCharges.Count; index++)
        {
            var adjustment = invoice.AllowancesAndCharges[index];
            var path = $"allowancesAndCharges[{index}]";
            var isCharge = adjustment.IsCharge;
            var label = text.DocumentAdjustmentLabel(isCharge, Findings.Number(index));

            if (adjustment.Amount is null)
            {
                findings.Add(isCharge ? "BR-36" : "BR-31", isCharge ? "BT-99" : "BT-92", $"{path}.amount",
                    text.DocumentAdjustmentAmountMissing(label));
            }

            if (Findings.Missing(adjustment.VatCategoryCode))
            {
                findings.Add(isCharge ? "BR-37" : "BR-32", isCharge ? "BT-102" : "BT-95", $"{path}.vatCategoryCode",
                    text.DocumentAdjustmentTaxCategoryMissing(label));
            }

            if (Findings.Missing(adjustment.Reason) && Findings.Missing(adjustment.ReasonCode))
            {
                findings.Add(isCharge ? "BR-38" : "BR-33", isCharge ? "BT-104" : "BT-97", $"{path}.reason",
                    text.DocumentAdjustmentReasonMissing(label));
            }

            if (Findings.HasMoreThanTwoDecimals(adjustment.Amount))
            {
                findings.Add(isCharge ? "BR-DEC-05" : "BR-DEC-01", null, $"{path}.amount",
                    text.DocumentAdjustmentAmountDecimals(label));
            }

            if (Findings.HasMoreThanTwoDecimals(adjustment.BaseAmount))
            {
                findings.Add(isCharge ? "BR-DEC-06" : "BR-DEC-02", null, $"{path}.baseAmount",
                    text.DocumentAdjustmentBaseDecimals(label));
            }
        }
    }

    private static void Payment(CanonicalInvoiceDraft invoice, TaxRegime regime, Findings findings, ICommonRuleText text)
    {
        if (!InvoiceFacts.HasPayment(invoice))
        {
            return;
        }

        var payment = invoice.Payment!;

        if (Findings.Missing(payment.PaymentMeansCode))
        {
            findings.Add("BR-49", "BT-81", "payment.paymentMeansCode", text.PaymentMeansMissing);
            return;
        }

        if (!regime.IsPaymentMeansCode(payment.PaymentMeansCode))
        {
            findings.Add("BR-CL-16", "BT-81", "payment.paymentMeansCode", text.PaymentMeansInvalid(payment.PaymentMeansCode));
        }

        if (CodeLists.IsCreditTransfer(payment.PaymentMeansCode) && Findings.Missing(payment.Iban))
        {
            findings.Add("BR-61", "BT-84", "payment.iban", text.IbanRequired);
        }
    }

    private static void Totals(CanonicalInvoiceDraft invoice, Findings findings, ICommonRuleText text)
    {
        if (Findings.HasMoreThanTwoDecimals(invoice.Totals?.PrepaidAmount))
        {
            findings.Add("BR-DEC-16", "BT-113", "totals.prepaidAmount", text.PrepaidAmountDecimals);
        }

        if (Findings.HasMoreThanTwoDecimals(invoice.Totals?.RoundingAmount))
        {
            findings.Add("BR-DEC-17", "BT-114", "totals.roundingAmount", text.RoundingAmountDecimals);
        }
    }
}
