using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.Validation;

namespace InvoicePlatform.Domain.Tests;

public class InvoiceCalculationValidatorTests
{
    private readonly InvoiceCalculationValidator _validator = new();

    /// <summary>A consistent invoice: 2 x 125.00 = 250.00, 21% VAT = 52.50, gross 302.50.</summary>
    private static CanonicalInvoiceDraft ConsistentInvoice() => new()
    {
        CurrencyCode = "EUR",
        Lines =
        [
            new CanonicalInvoiceLine
            {
                Description = "Onderhoudscontract",
                Quantity = 2m,
                UnitPrice = 125.00m,
                NetAmount = 250.00m,
                VatCategoryCode = "S",
                VatPercentage = 21m,
            },
        ],
        VatBreakdown =
        [
            new CanonicalVatBreakdownLine
            {
                VatCategoryCode = "S",
                VatPercentage = 21m,
                TaxableAmount = 250.00m,
                TaxAmount = 52.50m,
            },
        ],
        Totals = new CanonicalTotals
        {
            LineExtensionAmount = 250.00m,
            TaxExclusiveAmount = 250.00m,
            TaxAmount = 52.50m,
            TaxInclusiveAmount = 302.50m,
            PayableAmount = 302.50m,
        },
    };

    [Fact]
    public void A_consistent_invoice_produces_no_warnings()
    {
        Assert.Empty(_validator.Validate(ConsistentInvoice()));
    }

    [Fact]
    public void A_wrong_line_net_amount_produces_a_warning()
    {
        var draft = ConsistentInvoice();
        // 2 x 125.00 is 250.00, but the document says 260.00.
        draft = draft with
        {
            Lines = [draft.Lines[0] with { NetAmount = 260.00m }],
        };

        var warning = Assert.Single(_validator.Validate(draft), w => w.Code == "CALC-LINE-NET");

        Assert.Equal("lines[0].netAmount", warning.Path);
        Assert.Equal(260.00m, warning.ExtractedValue);
        Assert.Equal(250.00m, warning.CalculatedValue);
    }

    [Fact]
    public void A_wrong_vat_amount_produces_a_warning()
    {
        var draft = ConsistentInvoice();
        draft = draft with
        {
            VatBreakdown = [draft.VatBreakdown[0] with { TaxAmount = 50.00m }],
        };

        var warning = Assert.Single(_validator.Validate(draft), w => w.Code == "CALC-VAT-AMOUNT");

        Assert.Equal(50.00m, warning.ExtractedValue);
        Assert.Equal(52.50m, warning.CalculatedValue);
    }

    [Fact]
    public void A_wrong_gross_total_produces_a_warning()
    {
        var draft = ConsistentInvoice();
        draft = draft with
        {
            Totals = draft.Totals! with { TaxInclusiveAmount = 300.00m, PayableAmount = 300.00m },
        };

        var warnings = _validator.Validate(draft);

        var gross = Assert.Single(warnings, w => w.Code == "CALC-TOTAL-TAX-INCLUSIVE");
        Assert.Equal(300.00m, gross.ExtractedValue);
        Assert.Equal(302.50m, gross.CalculatedValue);
    }

    [Fact]
    public void A_wrong_payable_amount_produces_a_warning()
    {
        var draft = ConsistentInvoice();
        draft = draft with
        {
            Totals = draft.Totals! with { PayableAmount = 999.99m },
        };

        var warning = Assert.Single(_validator.Validate(draft), w => w.Code == "CALC-TOTAL-PAYABLE");

        Assert.Equal(999.99m, warning.ExtractedValue);
        Assert.Equal(302.50m, warning.CalculatedValue);
    }

    [Fact]
    public void Warnings_do_not_overwrite_the_extracted_values()
    {
        // The whole point: the draft is evidence of what the document said, and
        // the validator reports disagreement without editing it.
        var draft = ConsistentInvoice();
        draft = draft with
        {
            Totals = draft.Totals! with { PayableAmount = 111.11m },
        };

        var before = draft.Totals!.PayableAmount;
        var warnings = _validator.Validate(draft);

        Assert.NotEmpty(warnings);
        Assert.Equal(before, draft.Totals!.PayableAmount);
        Assert.Equal(111.11m, draft.Totals!.PayableAmount);
    }

    [Fact]
    public void Differences_within_one_cent_are_tolerated()
    {
        var draft = ConsistentInvoice();
        // Rounding noise of exactly the tolerance must not be reported.
        draft = draft with
        {
            Totals = draft.Totals! with
            {
                TaxInclusiveAmount = 302.51m,
                PayableAmount = 302.51m,
            },
        };

        Assert.DoesNotContain(
            _validator.Validate(draft),
            w => w.Code == "CALC-TOTAL-TAX-INCLUSIVE");
    }

    [Fact]
    public void Vat_is_calculated_per_bucket_not_per_line()
    {
        // Two lines at the same rate whose individual VAT amounts would each
        // round differently than the bucket total does.
        var draft = new CanonicalInvoiceDraft
        {
            Lines =
            [
                new CanonicalInvoiceLine
                {
                    Quantity = 1m, UnitPrice = 0.05m, NetAmount = 0.05m,
                    VatCategoryCode = "S", VatPercentage = 21m,
                },
                new CanonicalInvoiceLine
                {
                    Quantity = 1m, UnitPrice = 0.05m, NetAmount = 0.05m,
                    VatCategoryCode = "S", VatPercentage = 21m,
                },
            ],
            VatBreakdown =
            [
                new CanonicalVatBreakdownLine
                {
                    VatCategoryCode = "S",
                    VatPercentage = 21m,
                    TaxableAmount = 0.10m,
                    // 0.10 x 21% = 0.021 -> 0.02. Per-line it would be 0.01 + 0.01.
                    TaxAmount = 0.02m,
                },
            ],
            Totals = new CanonicalTotals
            {
                LineExtensionAmount = 0.10m,
                TaxExclusiveAmount = 0.10m,
                TaxAmount = 0.02m,
                TaxInclusiveAmount = 0.12m,
                PayableAmount = 0.12m,
            },
        };

        Assert.Empty(_validator.Validate(draft));
    }

    [Fact]
    public void Document_allowances_are_subtracted_and_charges_added()
    {
        var draft = new CanonicalInvoiceDraft
        {
            Lines =
            [
                new CanonicalInvoiceLine
                {
                    Quantity = 1m, UnitPrice = 100.00m, NetAmount = 100.00m,
                    VatCategoryCode = "S", VatPercentage = 21m,
                },
            ],
            AllowancesAndCharges =
            [
                new CanonicalAllowanceCharge { IsCharge = false, Amount = 10.00m, Reason = "Korting" },
                new CanonicalAllowanceCharge { IsCharge = true, Amount = 5.00m, Reason = "Verzending" },
            ],
            Totals = new CanonicalTotals
            {
                LineExtensionAmount = 100.00m,
                // 100 - 10 + 5
                TaxExclusiveAmount = 95.00m,
            },
        };

        Assert.DoesNotContain(
            _validator.Validate(draft),
            w => w.Code == "CALC-TOTAL-TAX-EXCLUSIVE");
    }

    [Fact]
    public void A_line_that_cannot_be_checked_is_reported_rather_than_assumed_correct()
    {
        var draft = new CanonicalInvoiceDraft
        {
            Lines = [new CanonicalInvoiceLine { Description = "Handwritten item" }],
        };

        Assert.Contains(_validator.Validate(draft), w => w.Code == "CALC-LINE-INCOMPLETE");
    }

    [Fact]
    public void Lines_in_an_undeclared_vat_bucket_are_reported()
    {
        var draft = new CanonicalInvoiceDraft
        {
            Lines =
            [
                new CanonicalInvoiceLine
                {
                    Quantity = 1m, UnitPrice = 10.00m, NetAmount = 10.00m,
                    VatCategoryCode = "S", VatPercentage = 9m,
                },
            ],
            VatBreakdown =
            [
                new CanonicalVatBreakdownLine
                {
                    VatCategoryCode = "S", VatPercentage = 21m,
                    TaxableAmount = 10.00m, TaxAmount = 2.10m,
                },
            ],
        };

        Assert.Contains(_validator.Validate(draft), w => w.Code == "CALC-VAT-MISSING-BUCKET");
    }

    [Fact]
    public void Missing_totals_are_reported_rather_than_skipped()
    {
        var draft = new CanonicalInvoiceDraft
        {
            Lines = [new CanonicalInvoiceLine { Quantity = 1m, UnitPrice = 1m, NetAmount = 1m }],
        };

        Assert.Contains(_validator.Validate(draft), w => w.Code == "CALC-TOTALS-MISSING");
    }
}
