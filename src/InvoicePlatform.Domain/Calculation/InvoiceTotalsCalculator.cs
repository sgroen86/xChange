using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Domain.Calculation;

/// <summary>
/// Derives line net amounts, the VAT breakdown and the document totals from the
/// lines, allowances and charges, the way EN 16931 defines them: VAT per
/// category and rate over the summed base rather than per line, amounts
/// rounded to the cent, half away from zero. Prepaid and rounding amounts are
/// inputs and are taken from the draft.
///
/// Pure: a new draft is returned and the input is untouched (CLAUDE.md, hard
/// rule 2). The review screen's apps/web/lib/totals.ts is a port of this class
/// and is tested on the same examples.
///
/// Unlike <see cref="Validation.InvoiceCalculationValidator"/>, which reports
/// where the printed figures disagree with the arithmetic, this produces the
/// figures an exported e-invoice carries.
/// </summary>
public static class InvoiceTotalsCalculator
{
    private const int MoneyScale = 2;

    public static CanonicalInvoiceDraft Apply(CanonicalInvoiceDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var lines = draft.Lines.Select(line => line with { NetAmount = LineNet(line) }).ToList();

        var buckets = new SortedDictionary<(string Category, decimal Rate), decimal>(BucketOrder.Instance);
        foreach (var line in lines)
        {
            Add(buckets, line.VatCategoryCode, line.VatPercentage, line.NetAmount ?? 0m);
        }

        foreach (var adjustment in draft.AllowancesAndCharges)
        {
            Add(buckets, adjustment.VatCategoryCode, adjustment.VatPercentage, Signed(adjustment));
        }

        var breakdown = buckets.Select(bucket =>
        {
            var taxable = Round(bucket.Value);
            return new CanonicalVatBreakdownLine
            {
                VatCategoryCode = bucket.Key.Category,
                VatPercentage = bucket.Key.Rate,
                TaxableAmount = taxable,
                TaxAmount = Round(taxable * bucket.Key.Rate / 100m),
                ExemptionReason = draft.VatBreakdown.FirstOrDefault(existing =>
                    existing.VatCategoryCode == bucket.Key.Category
                    && (existing.VatPercentage ?? 0m) == bucket.Key.Rate)?.ExemptionReason,
            };
        }).ToList();

        var allowances = draft.AllowancesAndCharges.Where(a => !a.IsCharge).ToList();
        var charges = draft.AllowancesAndCharges.Where(a => a.IsCharge).ToList();

        var lineExtension = Round(lines.Sum(l => l.NetAmount ?? 0m));
        decimal? allowanceTotal = allowances.Count == 0 ? null : Round(allowances.Sum(a => a.Amount ?? 0m));
        decimal? chargeTotal = charges.Count == 0 ? null : Round(charges.Sum(a => a.Amount ?? 0m));

        var taxExclusive = Round(lineExtension - (allowanceTotal ?? 0m) + (chargeTotal ?? 0m));
        var tax = Round(breakdown.Sum(b => b.TaxAmount ?? 0m));
        var taxInclusive = Round(taxExclusive + tax);
        var prepaid = draft.Totals?.PrepaidAmount;
        var rounding = draft.Totals?.RoundingAmount;

        // In the tax currency (PINT-SG: SGD) when the invoice is in another:
        // without tax and the tax each rounded to the cent, with tax their sum.
        decimal? exclusiveInTaxCurrency = null, taxInTaxCurrency = null, inclusiveInTaxCurrency = null;
        if (draft.TaxCurrencyCode is not null && draft.TaxCurrencyRate is { } rate)
        {
            exclusiveInTaxCurrency = Round(taxExclusive * rate);
            taxInTaxCurrency = Round(tax * rate);
            inclusiveInTaxCurrency = exclusiveInTaxCurrency + taxInTaxCurrency;
        }

        return draft with
        {
            Lines = lines,
            VatBreakdown = breakdown,
            Totals = new CanonicalTotals
            {
                LineExtensionAmount = lineExtension,
                AllowanceTotalAmount = allowanceTotal,
                ChargeTotalAmount = chargeTotal,
                TaxExclusiveAmount = taxExclusive,
                TaxAmount = tax,
                TaxInclusiveAmount = taxInclusive,
                PrepaidAmount = prepaid,
                RoundingAmount = rounding,
                PayableAmount = Round(taxInclusive - (prepaid ?? 0m) + (rounding ?? 0m)),
                TaxExclusiveAmountInTaxCurrency = exclusiveInTaxCurrency,
                TaxAmountInTaxCurrency = taxInTaxCurrency,
                TaxInclusiveAmountInTaxCurrency = inclusiveInTaxCurrency,
            },
        };
    }

    /// <summary>
    /// quantity x price / base quantity, rounded, plus line charges minus line
    /// allowances. A line without quantity or price keeps the net amount it has.
    /// </summary>
    private static decimal? LineNet(CanonicalInvoiceLine line)
    {
        if (line.Quantity is not { } quantity || line.UnitPrice is not { } price)
        {
            return line.NetAmount;
        }

        var baseQuantity = line.BaseQuantity is { } b && b != 0m ? b : 1m;
        return Round(Round(quantity * price / baseQuantity) + line.AllowancesAndCharges.Sum(Signed));
    }

    /// <summary>Charges add, allowances subtract.</summary>
    private static decimal Signed(CanonicalAllowanceCharge adjustment)
        => adjustment.IsCharge ? adjustment.Amount ?? 0m : -(adjustment.Amount ?? 0m);

    private static void Add(
        SortedDictionary<(string Category, decimal Rate), decimal> buckets,
        string? category,
        decimal? rate,
        decimal amount)
    {
        var key = (category ?? string.Empty, rate ?? 0m);
        buckets[key] = buckets.GetValueOrDefault(key) + amount;
    }

    private static decimal Round(decimal value) => Math.Round(value, MoneyScale, MidpointRounding.AwayFromZero);

    /// <summary>Category code ordinal, then rate ascending: a stable output order.</summary>
    private sealed class BucketOrder : IComparer<(string Category, decimal Rate)>
    {
        public static readonly BucketOrder Instance = new();

        public int Compare((string Category, decimal Rate) x, (string Category, decimal Rate) y)
        {
            var byCategory = string.CompareOrdinal(x.Category, y.Category);
            return byCategory != 0 ? byCategory : x.Rate.CompareTo(y.Rate);
        }
    }
}
