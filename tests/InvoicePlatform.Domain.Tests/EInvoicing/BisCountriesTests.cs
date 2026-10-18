using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.EInvoicing;
using InvoicePlatform.Domain.EInvoicing.Peppol;
using InvoicePlatform.Domain.EInvoicing.Pint;

namespace InvoicePlatform.Domain.Tests.EInvoicing;

/// <summary>
/// The UK and Belgium on BIS 3.0, and the Singapore organisation invoicing a
/// UK customer in BIS 3.0 rather than PINT-SG.
/// </summary>
public class BisCountriesTests
{
    private static readonly PeppolBisBilling30Ruleset Bis = new();

    private static InvoiceValidationResult Validate(CanonicalInvoiceDraft draft) => ValidatedCanonicalInvoice.Validate(Bis, draft);

    public static TheoryData<string> Samples => new() { "gb", "be", "sg to gb" };

    [Theory]
    [MemberData(nameof(Samples))]
    public void The_samples_are_valid_bis_3_invoices(string sample)
    {
        var draft = sample switch
        {
            "gb" => SampleInvoices.UnitedKingdom(),
            "be" => SampleInvoices.Belgium(),
            _ => SampleInvoices.SingaporeToUnitedKingdom(),
        };

        Assert.Empty(Validate(draft).Violations);
    }

    [Fact]
    public void A_singapore_gst_number_becomes_a_tax_registration_identifier_in_bis()
    {
        var seller = Validate(SampleInvoices.SingaporeToUnitedKingdom()).Invoice!.Invoice.Seller!;

        Assert.Null(seller.VatIdentifier);
        Assert.Equal("M2-1234567-K", seller.TaxRegistrationId);
    }

    [Fact]
    public void Pint_sg_keeps_the_gst_number_where_it_was()
        => Assert.Equal("M2-1234567-K",
            ValidatedCanonicalInvoice.Validate(new PintSgBilling1Ruleset(), SampleInvoices.Singapore()).Invoice!.Invoice.Seller!.VatIdentifier);

    [Fact]
    public void A_tax_registration_identifier_already_given_is_kept()
    {
        var draft = SampleInvoices.SingaporeToUnitedKingdom();
        draft = draft with { Seller = draft.Seller! with { TaxRegistrationId = "M2-7654321-X" } };

        Assert.Equal("M2-7654321-X", Validate(draft).Invoice!.Invoice.Seller!.TaxRegistrationId);
    }

    [Fact]
    public void A_uk_sellers_vat_number_stays_a_vat_number()
        => Assert.Equal("GB123456789", Validate(SampleInvoices.UnitedKingdom()).Invoice!.Invoice.Seller!.VatIdentifier);

    [Theory]
    [InlineData("NL814912345B01", true)]
    [InlineData("NL 8149.12.345 B01", false)]
    [InlineData("NL81491234B01", false)]
    [InlineData("BE0477472701", true)]
    [InlineData("BE1234567894", true)]   // enterprise numbers starting with 1 exist since 2023
    [InlineData("BE0477472702", false)]  // check digits wrong
    [InlineData("BE477472701", false)]   // nine digits
    [InlineData("GB123456789", true)]
    [InlineData("GB123456789001", true)] // branch trader
    [InlineData("GBGD123", true)]        // government department
    [InlineData("GBHA599", true)]        // health authority
    [InlineData("GB12345678", false)]
    [InlineData("GB 123 4567 89", false)]
    [InlineData("DE123456789", true)]    // other countries: only BR-CO-09's prefix check
    public void Vat_numbers_have_their_countrys_shape(string vat, bool valid)
    {
        var draft = SampleInvoices.UnitedKingdom();
        draft = draft with { Buyer = draft.Buyer! with { VatIdentifier = vat } };

        var shapeViolations = Validate(draft).Violations.Where(v => v.RuleId.StartsWith("XC-VAT-", StringComparison.Ordinal)).ToList();

        if (valid)
        {
            Assert.Empty(shapeViolations);
        }
        else
        {
            Assert.Contains(shapeViolations, v => v.Path == "buyer.vatIdentifier");
        }
    }
}
