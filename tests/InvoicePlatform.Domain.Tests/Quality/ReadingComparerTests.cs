using InvoicePlatform.Domain.Calculation;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.Quality;

namespace InvoicePlatform.Domain.Tests.Quality;

public class ReadingComparerTests
{
    /// <summary>A small invoice whose printed figures are its own arithmetic.</summary>
    private static CanonicalInvoiceDraft Invoice() => InvoiceTotalsCalculator.Apply(new CanonicalInvoiceDraft
    {
        TypeCode = "380",
        InvoiceNumber = "F-2026-041",
        IssueDate = "2026-10-01",
        CurrencyCode = "EUR",
        BuyerReference = "PO-17",
        Seller = new CanonicalParty
        {
            Name = "Green IT Solutions B.V.",
            VatIdentifier = "NL857231409B01",
            Address = new CanonicalAddress { StreetName = "Keizersgracht 62", PostalZone = "1015 CS", CityName = "Amsterdam", CountryCode = "NL" },
        },
        Buyer = new CanonicalParty { Name = "Van Dijk Techniek B.V.", VatIdentifier = "NL814912345B01" },
        Payment = new CanonicalPaymentDetails { PaymentMeansCode = "30", Iban = "NL91ABNA0417164300" },
        Lines =
        [
            new CanonicalInvoiceLine { Description = "Onderhoud", Quantity = 1m, UnitPrice = 100m, VatCategoryCode = "S", VatPercentage = 21m },
            new CanonicalInvoiceLine { Description = "Licentie", Quantity = 2m, UnitPrice = 50m, VatCategoryCode = "S", VatPercentage = 21m },
        ],
    });

    private static FieldComparison Field(ReadingComparison result, string path)
        => Assert.Single(result.Fields, f => f.Path == path);

    [Fact]
    public void A_reading_the_reviewer_did_not_change_is_perfect()
    {
        var result = ReadingComparer.Compare(Invoice(), Invoice());

        Assert.True(result.Perfect);
        Assert.Equal(result.Counted, result.Correct);
        Assert.Empty(result.NotCorrect);
    }

    [Fact]
    public void Fields_empty_on_both_sides_are_not_counted()
    {
        var result = ReadingComparer.Compare(Invoice(), Invoice());

        Assert.DoesNotContain(result.Fields, f => f.Path == "dueDate");
        Assert.DoesNotContain(result.Fields, f => f.Path == "buyer.address.cityName");
        Assert.DoesNotContain(result.Fields, f => f.Path == "note");
    }

    [Fact]
    public void Amounts_compare_as_values_text_without_case_or_extra_spaces_and_identifiers_without_spaces()
    {
        var read = Invoice() with
        {
            InvoiceNumber = "  f-2026-041 ",
            Seller = Invoice().Seller! with { Name = "Green  IT\tSolutions b.v.", Address = Invoice().Seller!.Address! with { PostalZone = "1015CS" } },
            Payment = Invoice().Payment! with { Iban = "nl91 abna 0417 1643 00" },
            Lines = [Invoice().Lines[0] with { UnitPrice = 100.00m, VatPercentage = 21.0m }, Invoice().Lines[1]],
        };

        var result = ReadingComparer.Compare(read, Invoice());

        Assert.True(result.Perfect, string.Join(", ", result.NotCorrect.Select(f => f.Path)));
    }

    [Fact]
    public void Dates_and_codes_must_match_exactly()
    {
        var read = Invoice() with
        {
            IssueDate = "2026-10-1",
            Seller = Invoice().Seller! with { Address = Invoice().Seller!.Address! with { CountryCode = "nl" } },
        };

        var result = ReadingComparer.Compare(read, Invoice());

        Assert.Equal(FieldOutcome.Corrected, Field(result, "issueDate").Outcome);
        Assert.Equal(FieldOutcome.Corrected, Field(result, "seller.address.countryCode").Outcome);
    }

    [Fact]
    public void Each_outcome_is_told_apart()
    {
        var read = Invoice() with
        {
            InvoiceNumber = "F-2026-014", // corrected
            BuyerReference = null, // missed
            PurchaseOrderReference = "PO-99", // extra
        };

        var result = ReadingComparer.Compare(read, Invoice());

        Assert.Equal(FieldOutcome.Corrected, Field(result, "invoiceNumber").Outcome);
        Assert.Equal(FieldOutcome.Missed, Field(result, "buyerReference").Outcome);
        Assert.Equal(FieldOutcome.Extra, Field(result, "purchaseOrderReference").Outcome);
        Assert.Equal(FieldOutcome.Correct, Field(result, "seller.vatIdentifier").Outcome);
        Assert.Equal((1, 1, 1), (result.Corrected, result.Missed, result.Extra));
        Assert.False(result.Perfect);
    }

    [Fact]
    public void Lines_are_compared_by_position()
    {
        var read = Invoice() with
        {
            Lines = [Invoice().Lines[0], Invoice().Lines[1] with { Quantity = 3m, Description = null }],
        };

        var result = ReadingComparer.Compare(read, Invoice());

        Assert.Equal(FieldOutcome.Correct, Field(result, "lines.count").Outcome);
        Assert.Equal(FieldOutcome.Correct, Field(result, "lines[0].quantity").Outcome);
        Assert.Equal(FieldOutcome.Corrected, Field(result, "lines[1].quantity").Outcome);
        Assert.Equal(FieldOutcome.Missed, Field(result, "lines[1].description").Outcome);
        Assert.Equal("lines[].quantity", Field(result, "lines[1].quantity").Field);
    }

    [Fact]
    public void A_line_only_one_side_has_is_missed_or_extra_field_by_field()
    {
        var oneLine = Invoice() with { Lines = [Invoice().Lines[0]] };

        var missedALine = ReadingComparer.Compare(oneLine, Invoice());
        var inventedALine = ReadingComparer.Compare(Invoice(), oneLine);

        Assert.Equal(FieldOutcome.Corrected, Field(missedALine, "lines.count").Outcome);
        Assert.All(missedALine.Fields.Where(f => f.Path.StartsWith("lines[1].", StringComparison.Ordinal)), f => Assert.Equal(FieldOutcome.Missed, f.Outcome));
        Assert.Equal(6, missedALine.Fields.Count(f => f.Path.StartsWith("lines[1].", StringComparison.Ordinal)));
        Assert.All(inventedALine.Fields.Where(f => f.Path.StartsWith("lines[1].", StringComparison.Ordinal)), f => Assert.Equal(FieldOutcome.Extra, f.Outcome));
    }

    [Fact]
    public void No_lines_read_is_a_missed_line_count()
    {
        var result = ReadingComparer.Compare(Invoice() with { Lines = [] }, Invoice());

        Assert.Equal(FieldOutcome.Missed, Field(result, "lines.count").Outcome);
    }

    [Fact]
    public void Calculated_figures_are_compared_with_the_arithmetic_of_the_booked_lines()
    {
        // The review screen shows calculated totals and cannot edit them, so the booked draft
        // still carries the model's figures. The model read 999.00 to pay; the lines add up to 242.00.
        var read = Invoice() with { Totals = Invoice().Totals! with { PayableAmount = 999.00m } };
        var booked = read;

        var result = ReadingComparer.Compare(read, booked);

        Assert.Equal(FieldOutcome.Corrected, Field(result, "totals.payableAmount").Outcome);
        Assert.Equal(FieldOutcome.Correct, Field(result, "totals.taxAmount").Outcome);
    }

    [Fact]
    public void A_line_net_is_compared_with_quantity_times_price()
    {
        var read = Invoice() with { Lines = [Invoice().Lines[0] with { NetAmount = 10m }, Invoice().Lines[1]] };

        var result = ReadingComparer.Compare(read, read);

        Assert.Equal(FieldOutcome.Corrected, Field(result, "lines[0].netAmount").Outcome);
        Assert.Equal(FieldOutcome.Correct, Field(result, "lines[1].netAmount").Outcome);
    }

    [Fact]
    public void Totals_the_model_did_not_read_are_missed()
    {
        var read = Invoice() with { Totals = null, VatBreakdown = [] };

        var result = ReadingComparer.Compare(read, read);

        Assert.Equal(FieldOutcome.Missed, Field(result, "totals.payableAmount").Outcome);
        Assert.Equal(FieldOutcome.Missed, Field(result, "vatBreakdown[S/21].taxAmount").Outcome);
    }

    [Fact]
    public void The_vat_breakdown_is_matched_by_category_and_rate_not_order_or_scale()
    {
        var two = InvoiceTotalsCalculator.Apply(Invoice() with
        {
            Lines = [Invoice().Lines[0], Invoice().Lines[1] with { VatPercentage = 9m }],
        });
        var read = two with
        {
            VatBreakdown = [.. two.VatBreakdown.Reverse().Select(b => b with { VatPercentage = b.VatPercentage + 0.00m })],
        };

        var result = ReadingComparer.Compare(read, two);

        Assert.Equal(FieldOutcome.Correct, Field(result, "vatBreakdown[S/9].taxAmount").Outcome);
        Assert.Equal(FieldOutcome.Correct, Field(result, "vatBreakdown[S/21].taxableAmount").Outcome);
        Assert.Equal("vatBreakdown[].taxAmount", Field(result, "vatBreakdown[S/9].taxAmount").Field);
    }

    [Fact]
    public void A_field_xchange_filled_before_review_is_neither_the_models_miss_nor_its_credit()
    {
        var booked = Invoice() with { Seller = Invoice().Seller! with { ElectronicAddress = "12345678", ElectronicAddressScheme = "0106" } };
        var read = Invoice();

        var unfilled = ReadingComparer.Compare(read, booked);
        var filled = ReadingComparer.Compare(read, booked, filledBeforeReview: booked);

        Assert.Equal(FieldOutcome.Missed, Field(unfilled, "seller.electronicAddress").Outcome);
        Assert.DoesNotContain(filled.Fields, f => f.Path.StartsWith("seller.electronicAddress", StringComparison.Ordinal));
        Assert.True(filled.Perfect);
    }

    [Fact]
    public void A_value_the_model_read_counts_even_where_xchange_would_have_filled_it()
    {
        var booked = Invoice();
        var read = Invoice() with { Seller = Invoice().Seller! with { VatIdentifier = "NL000000000B00" } };

        var result = ReadingComparer.Compare(read, booked, filledBeforeReview: booked);

        Assert.Equal(FieldOutcome.Corrected, Field(result, "seller.vatIdentifier").Outcome);
    }

    [Fact]
    public void Every_counted_field_is_in_the_documented_list()
    {
        var everything = Invoice() with
        {
            DueDate = "2026-10-31",
            PurchaseOrderReference = "PO",
            PrecedingInvoiceReference = "F-1",
            Buyer = Invoice().Seller! with { LegalRegistrationId = "1", LegalRegistrationScheme = "0106", ElectronicAddress = "1", ElectronicAddressScheme = "0106" },
            Payment = Invoice().Payment! with { Bic = "ABNANL2A", PaymentReference = "123" },
        };

        var result = ReadingComparer.Compare(everything, Invoice());

        Assert.All(result.Fields, f => Assert.Contains(f.Field, ReadingComparer.FieldNames));
        Assert.Equal(ReadingComparer.FieldNames.Count, ReadingComparer.FieldNames.Distinct().Count());
    }
}
