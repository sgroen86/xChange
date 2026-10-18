using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.Learning;
using InvoicePlatform.Domain.Tests.EInvoicing;

namespace InvoicePlatform.Domain.Tests.Learning;

public class CorrectionMemoryTests
{
    private static readonly DateTimeOffset Monday = DateTimeOffset.Parse("2026-10-19T09:00:00Z");
    private static readonly DateTimeOffset Tuesday = Monday.AddDays(1);

    /// <summary>The Singapore organisation's invoice to a UK customer, as the model might misread it.</summary>
    private static CanonicalInvoiceDraft ReadByModel() => SampleInvoices.SingaporeToUnitedKingdom() with
    {
        Seller = SampleInvoices.SingaporeToUnitedKingdom().Seller! with { VatIdentifier = null },
        BuyerReference = null,
        Lines = [SampleInvoices.Singapore().Lines[0] with { VatCategoryCode = "SR", VatPercentage = 9m, UnitCode = "C62" }],
        VatBreakdown = [],
    };

    /// <summary>What the reviewer booked.</summary>
    private static CanonicalInvoiceDraft Booked() => SampleInvoices.SingaporeToUnitedKingdom();

    [Fact]
    public void Own_details_are_learned_to_apply_and_header_fields_as_hints()
    {
        var learned = CorrectionMemory.Learn(ReadByModel(), Booked(), Monday);

        Assert.Contains(learned, c => c is { Field: "seller.vatIdentifier", From: null, To: "M2-1234567-K", Use: CorrectionUse.Apply, Scope: "organization" });
        Assert.Contains(learned, c => c is { Field: "buyerReference", To: "PO-4502", Use: CorrectionUse.Hint });
        Assert.Contains(learned, c => c is { Field: "lines.unitCode", From: "C62", To: "HUR", Context: "IT consulting", Use: CorrectionUse.Hint });
    }

    [Fact]
    public void A_tax_change_is_learned_for_that_customer_with_its_exemption_reason()
    {
        var learned = CorrectionMemory.Learn(ReadByModel(), Booked(), Monday);

        Assert.Contains(learned, c => c is { Field: "lines.tax", From: "SR|9", To: "AE|0", Scope: "buyer:GB987654321", Use: CorrectionUse.Apply });
        Assert.Contains(learned, c => c is { Field: "exemptionReason:AE", To: "Reverse charge", Scope: "buyer:GB987654321" });
    }

    [Fact]
    public void Nothing_is_learned_from_an_invoice_that_was_not_corrected()
        => Assert.Empty(CorrectionMemory.Learn(Booked(), Booked(), Monday));

    [Fact]
    public void The_next_invoice_to_the_same_customer_gets_the_corrections_back()
    {
        var memory = CorrectionMemory.Merge([], CorrectionMemory.Learn(ReadByModel(), Booked(), Monday));

        var result = CorrectionMemory.Apply(ReadByModel(), memory);

        Assert.Equal("M2-1234567-K", result.Draft.Seller!.VatIdentifier);
        Assert.Equal(("AE", 0m), (result.Draft.Lines[0].VatCategoryCode, result.Draft.Lines[0].VatPercentage));
        Assert.Equal("Reverse charge", Assert.Single(result.Draft.VatBreakdown).ExemptionReason);
        Assert.All(result.Notices, n => Assert.Equal("LEARNED", n.Code));
        // Hints are never applied by code: the buyer reference stays what the model read.
        Assert.Null(result.Draft.BuyerReference);
    }

    [Fact]
    public void A_customers_tax_correction_does_not_reach_another_customer()
    {
        var memory = CorrectionMemory.Merge([], CorrectionMemory.Learn(ReadByModel(), Booked(), Monday));
        var otherCustomer = ReadByModel() with { Buyer = ReadByModel().Buyer! with { VatIdentifier = "GB111222333" } };

        var result = CorrectionMemory.Apply(otherCustomer, memory);

        Assert.Equal("SR", result.Draft.Lines[0].VatCategoryCode);
        // The organisation's own details still apply.
        Assert.Equal("M2-1234567-K", result.Draft.Seller!.VatIdentifier);
    }

    [Fact]
    public void A_correction_is_applied_only_where_the_model_read_the_same_thing()
    {
        var memory = CorrectionMemory.Merge([], CorrectionMemory.Learn(ReadByModel(), Booked(), Monday));
        var readDifferently = ReadByModel() with { Seller = ReadByModel().Seller! with { VatIdentifier = "M2-7654321-X" } };

        Assert.Equal("M2-7654321-X", CorrectionMemory.Apply(readDifferently, memory).Draft.Seller!.VatIdentifier);
    }

    [Fact]
    public void A_repeat_counts_up_instead_of_duplicating()
    {
        var first = CorrectionMemory.Merge([], CorrectionMemory.Learn(ReadByModel(), Booked(), Monday));
        var second = CorrectionMemory.Merge(first, CorrectionMemory.Learn(ReadByModel(), Booked(), Tuesday));

        Assert.Equal(first.Count, second.Count);
        var vat = Assert.Single(second, c => c.Field == "seller.vatIdentifier");
        Assert.Equal((2, Tuesday), (vat.Count, vat.LastSeen));
    }

    [Fact]
    public void An_applied_correction_the_reviewer_undid_is_reported_for_forgetting()
    {
        var memory = CorrectionMemory.Merge([], CorrectionMemory.Learn(ReadByModel(), Booked(), Monday));
        var applied = CorrectionMemory.Apply(ReadByModel(), memory).Applied;
        var bookedWithSrAgain = Booked() with { Lines = [Booked().Lines[0] with { VatCategoryCode = "SR", VatPercentage = 9m }] };

        var undone = CorrectionMemory.Undone(applied, bookedWithSrAgain);

        Assert.Contains(applied.Single(c => c.Field == "lines.tax").Id, undone);
        Assert.DoesNotContain(applied.Single(c => c.Field == "seller.vatIdentifier").Id, undone);
    }

    [Fact]
    public void Hints_carry_what_was_read_and_what_was_entered()
    {
        var memory = CorrectionMemory.Merge([], CorrectionMemory.Learn(ReadByModel(), Booked(), Monday));

        var hints = CorrectionMemory.Hints(memory);

        Assert.Contains(hints, h => h is { Field: "buyerReference", YouRead: null, ReviewerEntered: "PO-4502" });
        Assert.DoesNotContain(hints, h => h.Field.StartsWith("seller", StringComparison.Ordinal));
    }

    [Fact]
    public void Memory_keeps_the_most_recent_corrections()
    {
        var many = Enumerable.Range(0, CorrectionMemory.Capacity + 10)
            .Select(i => new LearnedCorrection($"id{i}", "organization", "buyerReference", null, $"R{i}", null, CorrectionUse.Hint, 1, Monday.AddMinutes(i)));

        var memory = CorrectionMemory.Merge([], many);

        Assert.Equal(CorrectionMemory.Capacity, memory.Count);
        Assert.DoesNotContain(memory, c => c.Id == "id0");
    }
}
