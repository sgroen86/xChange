using System.Globalization;
using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Domain.EInvoicing.Peppol;

/// <summary>
/// PEPPOL BIS Billing 3.0 rules (release 3.0.21) beyond EN 16931 that can be
/// expressed on the canonical model. Rules that hold by construction (R001,
/// R004, R007 identifiers; R008 no empty elements; R053 one tax total; R120 the
/// line net, which is calculated) are the writer's responsibility.
/// </summary>
internal static class PeppolRules
{
    /// <summary>R040 and R120 allow this much difference, as the official rules do.</summary>
    private const decimal Slack = 0.02m;

    public static IReadOnlyList<RuleViolation> Check(CanonicalInvoiceDraft invoice)
    {
        var findings = new Findings();
        var text = PeppolRuleText.Current;
        var common = CommonRuleText.Current;

        TypeCode(invoice, findings, text);
        Dates(invoice, findings, text);

        if (Findings.Missing(invoice.BuyerReference) && Findings.Missing(invoice.PurchaseOrderReference))
        {
            findings.Add("PEPPOL-EN16931-R003", "BT-10", "buyerReference", text.BuyerReferenceMissing);
        }

        if (Findings.Missing(invoice.Buyer?.ElectronicAddress))
        {
            findings.Add("PEPPOL-EN16931-R010", "BT-49", "buyer.electronicAddress", text.BuyerEndpointMissing);
        }

        if (Findings.Missing(invoice.Seller?.ElectronicAddress))
        {
            findings.Add("PEPPOL-EN16931-R020", "BT-34", "seller.electronicAddress", text.SellerEndpointMissing);
        }

        Identifiers(invoice.Seller, "seller", findings, text);
        Identifiers(invoice.Buyer, "buyer", findings, text);

        for (var index = 0; index < invoice.Lines.Count; index++)
        {
            var line = invoice.Lines[index];

            if (line.BaseQuantity is { } baseQuantity && baseQuantity <= 0m)
            {
                findings.Add("PEPPOL-EN16931-R121", "BT-149", $"lines[{index}].baseQuantity",
                    text.BaseQuantityNotPositive(Findings.Number(index)));
            }

            for (var adjustmentIndex = 0; adjustmentIndex < line.AllowancesAndCharges.Count; adjustmentIndex++)
            {
                Percentage(line.AllowancesAndCharges[adjustmentIndex],
                    $"lines[{index}].allowancesAndCharges[{adjustmentIndex}]", common.LineLabel(Findings.Number(index)), findings, text);
            }
        }

        for (var index = 0; index < invoice.AllowancesAndCharges.Count; index++)
        {
            Percentage(invoice.AllowancesAndCharges[index], $"allowancesAndCharges[{index}]",
                text.DocumentLevelLabel(Findings.Number(index)), findings, text);
        }

        if (InvoiceFacts.HasPayment(invoice) && CodeLists.IsDirectDebit(invoice.Payment!.PaymentMeansCode))
        {
            findings.Add("PEPPOL-EN16931-R061", "BT-89", "payment.paymentMeansCode", text.DirectDebitUnsupported);
        }

        return findings.Violations;
    }

    /// <summary>
    /// Check digits for the identifier schemes the review screen offers besides
    /// the Dutch ones: GLN (0088, PEPPOL-COMMON-R040) and the Belgian enterprise
    /// number (0208, PEPPOL-COMMON-R043), on both the endpoint and the legal
    /// registration.
    /// </summary>
    private static void Identifiers(CanonicalParty? party, string side, Findings findings, IPeppolRuleText text)
    {
        Identifier(party?.ElectronicAddress, party?.ElectronicAddressScheme, $"{side}.electronicAddress", findings, text);
        Identifier(party?.LegalRegistrationId, party?.LegalRegistrationScheme, $"{side}.legalRegistrationId", findings, text);
    }

    private static void Identifier(string? value, string? scheme, string path, Findings findings, IPeppolRuleText text)
    {
        if (Findings.Missing(value))
        {
            return;
        }

        var trimmed = value!.Trim();

        if (scheme == "0088" && !IsGln(trimmed))
        {
            findings.Add("PEPPOL-COMMON-R040", null, path, text.GlnInvalid(trimmed));
        }
        else if (scheme == "0208" && !IsBelgianEnterpriseNumber(trimmed))
        {
            findings.Add("PEPPOL-COMMON-R043", null, path, text.BelgianEnterpriseNumberInvalid(trimmed));
        }
    }

    /// <summary>GS1 check digit: weights 3 and 1 from the right, as u:gln in the official rules.</summary>
    private static bool IsGln(string value)
    {
        if (value.Length < 2 || !value.All(char.IsAsciiDigit))
        {
            return false;
        }

        var sum = 0;
        for (int position = value.Length - 2, weightIndex = 0; position >= 0; position--, weightIndex++)
        {
            sum += (value[position] - '0') * (weightIndex % 2 == 0 ? 3 : 1);
        }

        return (10 - (sum % 10)) % 10 == value[^1] - '0';
    }

    /// <summary>Ten digits; the last two equal 97 minus the first eight modulo 97, as u:mod97-0208.</summary>
    private static bool IsBelgianEnterpriseNumber(string value)
        => value.Length == 10
            && value.All(char.IsAsciiDigit)
            && int.Parse(value[8..], CultureInfo.InvariantCulture) == 97 - (int.Parse(value[..8], CultureInfo.InvariantCulture) % 97);

    private static void TypeCode(CanonicalInvoiceDraft invoice, Findings findings, IPeppolRuleText text)
    {
        var code = invoice.TypeCode;
        if (Findings.Missing(code))
        {
            return; // BR-04
        }

        if (!CodeLists.IsInvoiceType(code) && !CodeLists.IsCreditNoteType(code))
        {
            findings.Add("PEPPOL-EN16931-P0100", "BT-3", "typeCode", text.TypeCodeNotAllowed(code));
        }
        else if (CodeLists.GermanOnlyTypeCodes.Contains(code!)
            && !(invoice.Seller?.Address?.CountryCode == "DE" && invoice.Buyer?.Address?.CountryCode == "DE"))
        {
            findings.Add("PEPPOL-EN16931-P0112", "BT-3", "typeCode", text.TypeCodeGermanOnly(code));
        }
    }

    private static void Dates(CanonicalInvoiceDraft invoice, Findings findings, IPeppolRuleText text)
    {
        Date(invoice.IssueDate, "issueDate", "BT-2", text.IssueDateFormat, findings);
        Date(invoice.DueDate, "dueDate", "BT-9", text.DueDateFormat, findings);
    }

    private static void Date(string? value, string path, string businessTerm, string message, Findings findings)
    {
        if (!Findings.Missing(value)
            && !DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            findings.Add("PEPPOL-EN16931-F001", businessTerm, path, message);
        }
    }

    /// <summary>R040, R041, R042: a percentage and its base go together and must agree with the amount.</summary>
    private static void Percentage(CanonicalAllowanceCharge adjustment, string path, string label, Findings findings, IPeppolRuleText text)
    {
        var isCharge = adjustment.IsCharge;

        if (adjustment.Percentage is not null && adjustment.BaseAmount is null)
        {
            findings.Add("PEPPOL-EN16931-R041", null, $"{path}.baseAmount", text.PercentageNeedsBase(label, isCharge));
        }
        else if (adjustment.Percentage is null && adjustment.BaseAmount is not null)
        {
            findings.Add("PEPPOL-EN16931-R042", null, $"{path}.percentage", text.BaseNeedsPercentage(label, isCharge));
        }
        else if (adjustment.Percentage is { } percentage && adjustment.BaseAmount is { } baseAmount)
        {
            var expected = baseAmount * percentage / 100m;
            if (Math.Abs((adjustment.Amount ?? 0m) - expected) > Slack)
            {
                findings.Add("PEPPOL-EN16931-R040", null, $"{path}.amount",
                    text.AmountIsNotBaseTimesPercentage(label, isCharge, Findings.Format(baseAmount), Findings.Format(percentage)));
            }
        }
    }
}
