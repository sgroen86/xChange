using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Domain.Validation;

/// <summary>
/// Recomputes an extracted invoice's arithmetic and reports where the document
/// and the recalculation disagree.
///
/// This deliberately never mutates the draft. An extracted total that does not
/// match the recomputed one is usually the most interesting thing on the page -
/// a misread digit, a missed discount line, an unusual rounding rule - so it is
/// surfaced for review rather than quietly replaced (CLAUDE.md, hard rule 2:
/// deterministic, no model in the path).
/// </summary>
public sealed class InvoiceCalculationValidator
{
    /// <summary>Amounts are compared to the cent; anything larger is reported.</summary>
    public const decimal Tolerance = 0.01m;

    private const int MoneyScale = 2;

    public IReadOnlyList<ValidationWarning> Validate(CanonicalInvoiceDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var warnings = new List<ValidationWarning>();

        var lineNetTotals = ValidateLines(draft, warnings);
        var vatTotals = ValidateVatBreakdown(draft, lineNetTotals, warnings);
        ValidateTotals(draft, lineNetTotals, vatTotals, warnings);

        return warnings;
    }

    /// <summary>Recalculates each line's net amount from quantity, price and line adjustments.</summary>
    private static Dictionary<string, decimal> ValidateLines(
        CanonicalInvoiceDraft draft,
        List<ValidationWarning> warnings)
    {
        var netByVatKey = new Dictionary<string, decimal>();

        for (var index = 0; index < draft.Lines.Count; index++)
        {
            var line = draft.Lines[index];
            var path = $"lines[{index}]";

            var effectiveNet = line.NetAmount;

            if (line.Quantity is { } quantity && line.UnitPrice is { } unitPrice)
            {
                // BaseQuantity is the quantity the unit price refers to; absent means 1.
                var baseQuantity = line.BaseQuantity is { } b && b != 0m ? b : 1m;

                var calculated = Round(quantity * unitPrice / baseQuantity);
                calculated += SumAdjustments(line.AllowancesAndCharges);
                calculated = Round(calculated);

                if (line.NetAmount is { } extracted)
                {
                    if (Differs(extracted, calculated))
                    {
                        warnings.Add(new ValidationWarning(
                            "CALC-LINE-NET",
                            $"{path}.netAmount",
                            $"Line {index + 1}: extracted net amount {Format(extracted)} does not match "
                                + $"the recalculated {Format(calculated)}.",
                            ValidationSeverity.Warning,
                            extracted,
                            calculated));
                    }
                }
                else
                {
                    effectiveNet = calculated;
                }
            }
            else if (line.NetAmount is null)
            {
                warnings.Add(new ValidationWarning(
                    "CALC-LINE-INCOMPLETE",
                    $"{path}.netAmount",
                    $"Line {index + 1}: no net amount and not enough data (quantity and unit price) "
                        + "to calculate one.",
                    ValidationSeverity.Warning));
            }

            if (effectiveNet is { } net)
            {
                var key = VatKey(line.VatCategoryCode, line.VatPercentage);
                netByVatKey[key] = netByVatKey.GetValueOrDefault(key) + net;
            }
        }

        return netByVatKey;
    }

    /// <summary>Recalculates the VAT subtotal per category/rate bucket.</summary>
    private static Dictionary<string, decimal> ValidateVatBreakdown(
        CanonicalInvoiceDraft draft,
        Dictionary<string, decimal> lineNetTotals,
        List<ValidationWarning> warnings)
    {
        var calculatedTax = new Dictionary<string, decimal>();

        for (var index = 0; index < draft.VatBreakdown.Count; index++)
        {
            var bucket = draft.VatBreakdown[index];
            var path = $"vatBreakdown[{index}]";
            var key = VatKey(bucket.VatCategoryCode, bucket.VatPercentage);

            // Taxable base: compare against the lines that fall in this bucket.
            if (lineNetTotals.TryGetValue(key, out var lineNet) && bucket.TaxableAmount is { } taxable)
            {
                var expected = Round(lineNet);
                if (Differs(taxable, expected))
                {
                    warnings.Add(new ValidationWarning(
                        "CALC-VAT-TAXABLE",
                        $"{path}.taxableAmount",
                        $"VAT bucket {key}: extracted taxable amount {Format(taxable)} does not match "
                            + $"the sum of matching invoice lines {Format(expected)}.",
                        ValidationSeverity.Warning,
                        taxable,
                        expected));
                }
            }

            // Tax amount: taxable x rate.
            if (bucket.TaxableAmount is { } basis && bucket.VatPercentage is { } percentage)
            {
                var expectedTax = Round(basis * percentage / 100m);
                calculatedTax[key] = expectedTax;

                if (bucket.TaxAmount is { } reportedTax && Differs(reportedTax, expectedTax))
                {
                    warnings.Add(new ValidationWarning(
                        "CALC-VAT-AMOUNT",
                        $"{path}.taxAmount",
                        $"VAT bucket {key}: extracted VAT {Format(reportedTax)} does not match "
                            + $"{Format(basis)} x {Format(percentage)}% = {Format(expectedTax)}.",
                        ValidationSeverity.Warning,
                        reportedTax,
                        expectedTax));
                }
            }
            else if (bucket.TaxAmount is { } onlyTax)
            {
                calculatedTax[key] = onlyTax;
            }
        }

        // Lines in a bucket the document never declared.
        foreach (var (key, net) in lineNetTotals)
        {
            var declared = draft.VatBreakdown.Any(b => VatKey(b.VatCategoryCode, b.VatPercentage) == key);
            if (!declared)
            {
                warnings.Add(new ValidationWarning(
                    "CALC-VAT-MISSING-BUCKET",
                    "vatBreakdown",
                    $"Invoice lines totalling {Format(Round(net))} fall in VAT bucket {key}, "
                        + "which has no entry in the VAT breakdown.",
                    ValidationSeverity.Warning));
            }
        }

        return calculatedTax;
    }

    private static void ValidateTotals(
        CanonicalInvoiceDraft draft,
        Dictionary<string, decimal> lineNetTotals,
        Dictionary<string, decimal> calculatedTax,
        List<ValidationWarning> warnings)
    {
        var totals = draft.Totals;
        if (totals is null)
        {
            warnings.Add(new ValidationWarning(
                "CALC-TOTALS-MISSING",
                "totals",
                "The invoice has no document totals, so they could not be checked.",
                ValidationSeverity.Warning));
            return;
        }

        // Net total = sum of line nets.
        var calculatedLineExtension = Round(lineNetTotals.Values.Sum());
        if (draft.Lines.Count > 0 && totals.LineExtensionAmount is { } lineExtension
            && Differs(lineExtension, calculatedLineExtension))
        {
            warnings.Add(new ValidationWarning(
                "CALC-TOTAL-LINE-EXTENSION",
                "totals.lineExtensionAmount",
                $"Extracted line total {Format(lineExtension)} does not match the sum of the "
                    + $"invoice lines {Format(calculatedLineExtension)}.",
                ValidationSeverity.Warning,
                lineExtension,
                calculatedLineExtension));
        }

        // Taxable base = line total - allowances + charges.
        var basis = totals.LineExtensionAmount ?? calculatedLineExtension;
        var documentAdjustments = SumAdjustments(draft.AllowancesAndCharges);
        var calculatedTaxExclusive = Round(basis + documentAdjustments);

        if (totals.TaxExclusiveAmount is { } taxExclusive && Differs(taxExclusive, calculatedTaxExclusive))
        {
            warnings.Add(new ValidationWarning(
                "CALC-TOTAL-TAX-EXCLUSIVE",
                "totals.taxExclusiveAmount",
                $"Extracted net total {Format(taxExclusive)} does not match "
                    + $"{Format(calculatedTaxExclusive)} calculated from the lines and document "
                    + "allowances and charges.",
                ValidationSeverity.Warning,
                taxExclusive,
                calculatedTaxExclusive));
        }

        // VAT total = sum of the breakdown.
        if (calculatedTax.Count > 0 && totals.TaxAmount is { } taxAmount)
        {
            var calculatedTotalTax = Round(calculatedTax.Values.Sum());
            if (Differs(taxAmount, calculatedTotalTax))
            {
                warnings.Add(new ValidationWarning(
                    "CALC-TOTAL-TAX",
                    "totals.taxAmount",
                    $"Extracted VAT total {Format(taxAmount)} does not match the sum of the VAT "
                        + $"breakdown {Format(calculatedTotalTax)}.",
                    ValidationSeverity.Warning,
                    taxAmount,
                    calculatedTotalTax));
            }
        }

        // Gross total = net + VAT.
        if (totals.TaxExclusiveAmount is { } net2 && totals.TaxAmount is { } vat2
            && totals.TaxInclusiveAmount is { } gross)
        {
            var calculatedGross = Round(net2 + vat2);
            if (Differs(gross, calculatedGross))
            {
                warnings.Add(new ValidationWarning(
                    "CALC-TOTAL-TAX-INCLUSIVE",
                    "totals.taxInclusiveAmount",
                    $"Extracted gross total {Format(gross)} does not match net {Format(net2)} "
                        + $"plus VAT {Format(vat2)} = {Format(calculatedGross)}.",
                    ValidationSeverity.Warning,
                    gross,
                    calculatedGross));
            }
        }

        // Payable = gross - prepaid + rounding.
        if (totals.TaxInclusiveAmount is { } gross2 && totals.PayableAmount is { } payable)
        {
            var calculatedPayable = Round(
                gross2 - (totals.PrepaidAmount ?? 0m) + (totals.RoundingAmount ?? 0m));

            if (Differs(payable, calculatedPayable))
            {
                warnings.Add(new ValidationWarning(
                    "CALC-TOTAL-PAYABLE",
                    "totals.payableAmount",
                    $"Extracted payable amount {Format(payable)} does not match gross "
                        + $"{Format(gross2)} less prepaid plus rounding = {Format(calculatedPayable)}.",
                    ValidationSeverity.Warning,
                    payable,
                    calculatedPayable));
            }
        }
    }

    /// <summary>Charges add, allowances subtract.</summary>
    private static decimal SumAdjustments(IReadOnlyList<CanonicalAllowanceCharge> adjustments)
    {
        var total = 0m;
        foreach (var adjustment in adjustments)
        {
            if (adjustment.Amount is not { } amount)
            {
                continue;
            }

            total += adjustment.IsCharge ? amount : -amount;
        }

        return total;
    }

    private static string VatKey(string? categoryCode, decimal? percentage)
        => $"{categoryCode ?? "?"}/{(percentage.HasValue ? Format(percentage.Value) : "?")}";

    private static bool Differs(decimal left, decimal right)
        => Math.Abs(left - right) > Tolerance;

    private static decimal Round(decimal value)
        => Math.Round(value, MoneyScale, MidpointRounding.AwayFromZero);

    private static string Format(decimal value)
        => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
