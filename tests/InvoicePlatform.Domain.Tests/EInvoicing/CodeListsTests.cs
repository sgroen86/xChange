using InvoicePlatform.Domain.EInvoicing;

namespace InvoicePlatform.Domain.Tests.EInvoicing;

public class CodeListsTests
{
    [Theory]
    [InlineData("380", true, false)]
    [InlineData("381", false, true)]
    [InlineData("389", false, false)] // on the code list page, but not in P0100's test
    [InlineData("999", false, false)]
    [InlineData(null, false, false)]
    public void Type_codes_follow_the_peppol_subsets(string? code, bool invoice, bool creditNote)
    {
        Assert.Equal(invoice, CodeLists.IsInvoiceType(code));
        Assert.Equal(creditNote, CodeLists.IsCreditNoteType(code));
    }

    [Theory]
    [InlineData("0106", true)]
    [InlineData("0208", true)]
    [InlineData("9944", true)]
    [InlineData("0217", false)] // removed from EAS in 3.0.21
    [InlineData("0000", false)]
    [InlineData("", false)]
    public void Electronic_address_schemes_follow_the_3_0_21_list(string code, bool valid)
        => Assert.Equal(valid, CodeLists.IsElectronicAddressScheme(code));

    [Theory]
    [InlineData("EUR", true)]
    [InlineData("eur", false)]
    [InlineData("EURO", false)]
    public void Currency_codes_are_three_capitals(string code, bool valid)
        => Assert.Equal(valid, CodeLists.IsCurrencyCode(code));

    [Theory]
    [InlineData("NL", true)]
    [InlineData("NLD", false)]
    [InlineData("nl", false)]
    public void Country_codes_are_two_capitals(string code, bool valid)
        => Assert.Equal(valid, CodeLists.IsCountryCode(code));

    [Theory]
    [InlineData("C62", true)]
    [InlineData("HUR", true)]
    [InlineData("stuks", false)]
    public void Unit_codes_have_the_rec_20_shape(string code, bool valid)
        => Assert.Equal(valid, CodeLists.IsUnitCode(code));

    [Theory]
    [InlineData("30", true)]
    [InlineData("58", true)]
    [InlineData("97", true)]
    [InlineData("ZZZ", true)]
    [InlineData("98", false)]
    [InlineData("030", false)]
    [InlineData("0", false)]
    public void Payment_means_codes_follow_uncl_4461(string code, bool valid)
        => Assert.Equal(valid, CodeLists.IsPaymentMeansCode(code));
}
