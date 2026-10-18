using System.Globalization;
using InvoicePlatform.Domain.Calculation;
using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Domain.Tests.Calculation;

/// <summary>
/// The same examples, with the same numbers, are in apps/web/lib/totals.test.ts:
/// the review screen and the exported file must agree to the cent.
/// </summary>
public class InvoiceTotalsCalculatorTests
{
    private static decimal D(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);

    private static CanonicalInvoiceLine Line(string qty, string price, string category, string? rate, string? baseQty = null)
        => new()
        {
            Quantity = D(qty),
            UnitPrice = D(price),
            BaseQuantity = baseQty is null ? null : D(baseQty),
            VatCategoryCode = category,
            VatPercentage = rate is null ? null : D(rate),
        };

    [Fact]
    public void Example_1_two_rates_vat_per_category_not_per_line()
    {
        var result = InvoiceTotalsCalculator.Apply(new CanonicalInvoiceDraft
        {
            Lines = [Line("1", "1250.00", "S", "21"), Line("7.5", "89.50", "S", "21"), Line("2", "34.95", "S", "9")],
        });

        Assert.Equal([1250.00m, 671.25m, 69.90m], result.Lines.Select(l => l.NetAmount!.Value));

        // Ordered by category code, then rate ascending.
        Assert.Equal(2, result.VatBreakdown.Count);
        Assert.Equal((9m, 69.90m, 6.29m), Bucket(result.VatBreakdown[0]));
        Assert.Equal((21m, 1921.25m, 403.46m), Bucket(result.VatBreakdown[1]));

        Assert.Equal(1991.15m, result.Totals!.LineExtensionAmount);
        Assert.Equal(1991.15m, result.Totals.TaxExclusiveAmount);
        Assert.Equal(409.75m, result.Totals.TaxAmount);
        Assert.Equal(2400.90m, result.Totals.TaxInclusiveAmount);
        Assert.Equal(2400.90m, result.Totals.PayableAmount);
        Assert.Null(result.Totals.AllowanceTotalAmount);
        Assert.Null(result.Totals.ChargeTotalAmount);
    }

    [Fact]
    public void Example_2_rounding_half_away_from_zero_and_base_quantity()
    {
        // 3 x 0.335 = 1.005 -> 1.01 ; 10 x 12.50 per 100 = 1.25
        var result = InvoiceTotalsCalculator.Apply(new CanonicalInvoiceDraft
        {
            Lines = [Line("3", "0.335", "S", "21"), Line("10", "12.50", "S", "21", baseQty: "100")],
        });

        Assert.Equal([1.01m, 1.25m], result.Lines.Select(l => l.NetAmount!.Value));
        Assert.Equal(2.26m, result.Totals!.TaxExclusiveAmount);
        Assert.Equal(0.47m, result.Totals.TaxAmount); // 2.26 x 21% = 0.4746
    }

    [Fact]
    public void Example_3_allowances_and_charges_on_line_and_document()
    {
        var line = Line("1", "100.00", "S", "21") with
        {
            AllowancesAndCharges = [new CanonicalAllowanceCharge { IsCharge = false, Amount = 10.00m, Reason = "Korting" }],
        };

        var result = InvoiceTotalsCalculator.Apply(new CanonicalInvoiceDraft
        {
            Lines = [line],
            AllowancesAndCharges =
            [
                new CanonicalAllowanceCharge { IsCharge = true, Amount = 5.00m, VatCategoryCode = "S", VatPercentage = 21m, Reason = "Vracht" },
                new CanonicalAllowanceCharge { IsCharge = false, Amount = 2.50m, VatCategoryCode = "S", VatPercentage = 21m, Reason = "Klant" },
            ],
            Totals = new CanonicalTotals { PrepaidAmount = 20.00m, RoundingAmount = 0.01m },
        });

        Assert.Equal(90.00m, result.Lines[0].NetAmount);
        Assert.Equal(90.00m, result.Totals!.LineExtensionAmount);
        Assert.Equal(2.50m, result.Totals.AllowanceTotalAmount);
        Assert.Equal(5.00m, result.Totals.ChargeTotalAmount);
        Assert.Equal(92.50m, result.Totals.TaxExclusiveAmount);
        Assert.Equal((21m, 92.50m, 19.43m), Bucket(Assert.Single(result.VatBreakdown))); // 19.425
        Assert.Equal(19.43m, result.Totals.TaxAmount);
        Assert.Equal(111.93m, result.Totals.TaxInclusiveAmount);
        Assert.Equal(20.00m, result.Totals.PrepaidAmount);
        Assert.Equal(0.01m, result.Totals.RoundingAmount);
        Assert.Equal(91.94m, result.Totals.PayableAmount); // 111.93 - 20.00 + 0.01
    }

    [Fact]
    public void Example_4_exempt_category_keeps_its_exemption_reason()
    {
        var result = InvoiceTotalsCalculator.Apply(new CanonicalInvoiceDraft
        {
            Lines = [Line("2", "50.00", "E", null)],
            VatBreakdown = [new CanonicalVatBreakdownLine { VatCategoryCode = "E", VatPercentage = 0m, ExemptionReason = "Vrijgesteld" }],
        });

        var bucket = Assert.Single(result.VatBreakdown);
        Assert.Equal("E", bucket.VatCategoryCode);
        Assert.Equal((0m, 100.00m, 0.00m), Bucket(bucket));
        Assert.Equal("Vrijgesteld", bucket.ExemptionReason);
    }

    [Fact]
    public void A_line_without_quantity_or_price_keeps_its_net_amount()
    {
        var result = InvoiceTotalsCalculator.Apply(new CanonicalInvoiceDraft
        {
            Lines = [new CanonicalInvoiceLine { NetAmount = 42.00m, VatCategoryCode = "S", VatPercentage = 21m }],
        });

        Assert.Equal(42.00m, result.Totals!.LineExtensionAmount);
        Assert.Equal(8.82m, result.Totals.TaxAmount);
    }

    [Fact]
    public void The_input_draft_is_not_mutated()
    {
        var draft = new CanonicalInvoiceDraft { Lines = [Line("1", "1.00", "S", "21")] };

        _ = InvoiceTotalsCalculator.Apply(draft);

        Assert.Null(draft.Lines[0].NetAmount);
        Assert.Null(draft.Totals);
    }

    private static (decimal Rate, decimal Taxable, decimal Tax) Bucket(CanonicalVatBreakdownLine line)
        => (line.VatPercentage!.Value, line.TaxableAmount!.Value, line.TaxAmount!.Value);
}
