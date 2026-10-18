using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.EInvoicing;
using InvoicePlatform.Domain.EInvoicing.Peppol;
using InvoicePlatform.Domain.EInvoicing.Pint;
using InvoicePlatform.Domain.Organizations;

namespace InvoicePlatform.Domain.Tests.EInvoicing;

/// <summary>
/// One case per runtime GST rule: start from a valid Singapore invoice, break
/// exactly that rule, and the export is blocked with that rule id on that field.
/// </summary>
public class PintSgBilling1RulesetTests
{
    private static readonly PintSgBilling1Ruleset Ruleset = new();

    private static InvoiceValidationResult Validate(CanonicalInvoiceDraft draft)
        => ValidatedCanonicalInvoice.Validate(Ruleset, draft);

    public static TheoryData<string> Samples => new() { "sgd", "usd", "credit note" };

    [Theory]
    [MemberData(nameof(Samples))]
    public void The_singapore_samples_are_valid(string sample)
    {
        var draft = sample switch
        {
            "sgd" => SampleInvoices.Singapore(),
            "usd" => SampleInvoices.SingaporeInUsDollars(),
            _ => SampleInvoices.SingaporeCreditNote(),
        };

        Assert.Empty(Validate(draft).Violations);
    }

    [Fact]
    public void Identifiers_are_those_of_pint_sg()
    {
        Assert.Equal("urn:peppol:pint:billing-1@sg-1", Ruleset.CustomizationId);
        Assert.Equal("urn:peppol:bis:billing", Ruleset.ProfileId);
        Assert.Equal("GST", Ruleset.Ubl.TaxSchemeId);
    }

    [Fact]
    public void A_european_invoice_is_not_a_pint_sg_invoice()
    {
        var violations = Validate(SampleInvoices.Valid()).Violations;

        Assert.Contains(violations, v => v.RuleId == "BR-CL-18" && v.Path == "lines[0].vatCategoryCode");
    }

    [Fact]
    public void A_singapore_invoice_is_not_a_bis_3_invoice()
    {
        var violations = ValidatedCanonicalInvoice.Validate(new PeppolBisBilling30Ruleset(), SampleInvoices.Singapore()).Violations;

        Assert.Contains(violations, v => v.RuleId == "BR-CL-18");
    }

    [Fact]
    public void A_foreign_currency_invoice_gets_sgd_as_tax_currency_and_sgd_totals()
    {
        var result = Validate(SampleInvoices.SingaporeInUsDollars());

        var invoice = result.Invoice!.Invoice;
        Assert.Equal("SGD", invoice.TaxCurrencyCode);
        // 1500.00 + 779.80 = 2279.80 USD; GST 9% = 205.18 USD.
        Assert.Equal(2279.80m, invoice.Totals!.TaxExclusiveAmount);
        Assert.Equal(205.18m, invoice.Totals.TaxAmount);
        Assert.Equal(3060.63m, invoice.Totals.TaxExclusiveAmountInTaxCurrency); // 2279.80 x 1.3425 = 3060.6315
        Assert.Equal(275.45m, invoice.Totals.TaxAmountInTaxCurrency);            // 205.18 x 1.3425 = 275.454...
        Assert.Equal(3336.08m, invoice.Totals.TaxInclusiveAmountInTaxCurrency);  // their sum
    }

    [Fact]
    public void An_sgd_invoice_carries_no_tax_currency()
        => Assert.Null(Validate(SampleInvoices.Singapore()).Invoice!.Invoice.TaxCurrencyCode);

    public static TheoryData<string, string, Func<CanonicalInvoiceDraft, CanonicalInvoiceDraft>> Breaks => new()
    {
        { "BR-105-GST-SG", "seller.vatIdentifier", d => d with { Seller = d.Seller! with { VatIdentifier = null } } },
        { "BR-106-GST-SG", "seller.address.postalZone", d => d with { Seller = d.Seller! with { Address = d.Seller.Address! with { PostalZone = null } } } },
        { "BR-107-GST-SG", "buyer.address.streetName", d => d with { Buyer = d.Buyer! with { Address = d.Buyer.Address! with { StreetName = null } } } },
        { "BR-112-GST-SG", "seller.legalRegistrationId", d => d with { Seller = d.Seller! with { LegalRegistrationId = null } } },
        { "BR-111-GST-SG", "note", d => d with { TypeCode = "381", Note = null, PrecedingInvoiceReference = "SG-1" } },
        { "BR-113-GST-SG", "taxCurrencyRate", d => d with { CurrencyCode = "USD", TaxCurrencyRate = null } },
        { "BR-CO-25-SG", "dueDate", d => d with { DueDate = null } },
        { "BR-CL-18", "lines[0].vatCategoryCode", d => d with { Lines = [d.Lines[0] with { VatCategoryCode = "S" }, d.Lines[1]] } },
        { "BR-NG-12-GST-SG", "lines", d => d with { Lines = [d.Lines[0] with { VatCategoryCode = "NG", VatPercentage = null }, d.Lines[1]] } },
        { "BR-NG-02-GST-SG", "seller.vatIdentifier", d => d with { Lines = [.. d.Lines.Select(l => l with { VatCategoryCode = "NG", VatPercentage = null })] } },
        { "XC-SG-0195", "buyer.electronicAddress", d => d with { Buyer = d.Buyer! with { ElectronicAddress = "200212345Z" } } },
        { "BR-CL-16", "payment.paymentMeansCode", d => d with { Payment = d.Payment! with { PaymentMeansCode = "Z99" } } },
        { "PEPPOL-EN16931-R003", "buyerReference", d => d with { BuyerReference = null } },
    };

    [Theory]
    [MemberData(nameof(Breaks))]
    public void Breaking_a_rule_blocks_the_export(string ruleId, string path, Func<CanonicalInvoiceDraft, CanonicalInvoiceDraft> breakIt)
    {
        var result = Validate(breakIt(SampleInvoices.Singapore()));

        Assert.Null(result.Invoice);
        Assert.Contains(result.Violations, v => v.RuleId == ruleId && v.Path == path);
    }

    [Theory]
    [InlineData("Z01")]
    [InlineData("Z02")]
    public void Giro_and_paynow_are_payment_means_in_singapore(string code)
    {
        var draft = SampleInvoices.Singapore() with { Payment = SampleInvoices.Singapore().Payment! with { PaymentMeansCode = code, Iban = null } };

        Assert.DoesNotContain(Validate(draft).Violations, v => v.Path.StartsWith("payment", StringComparison.Ordinal));
    }

    [Fact]
    public void Gst_numbers_need_no_country_prefix()
        => Assert.DoesNotContain(Validate(SampleInvoices.Singapore()).Violations, v => v.RuleId == "BR-CO-09");

    [Theory]
    [InlineData("SG", PintSgBilling1Ruleset.RulesetId)]
    [InlineData("GB", PeppolBisBilling30Ruleset.RulesetId)]
    [InlineData("NL", PeppolBisBilling30Ruleset.RulesetId)]
    [InlineData("BE", PeppolBisBilling30Ruleset.RulesetId)]
    public void Each_country_has_a_default_export_format(string country, string ruleset)
    {
        var registry = new InvoiceRulesetRegistry();

        Assert.Equal(ruleset, registry.Resolve(null, null, Countries.Find(country)!.DefaultRulesetId)!.Id);
    }

    [Fact]
    public void The_organisation_default_wins_over_the_country_default()
    {
        var registry = new InvoiceRulesetRegistry();

        Assert.Equal(PeppolBisBilling30Ruleset.RulesetId,
            registry.Resolve(null, PeppolBisBilling30Ruleset.RulesetId, PintSgBilling1Ruleset.RulesetId)!.Id);
    }
}

public class DocumentUuidsTests
{
    [Fact]
    public void The_uuid_is_stable_well_formed_and_differs_per_organisation()
    {
        var first = DocumentUuids.For("org-a", "inv-mvd6eahnof6v");

        Assert.Equal(first, DocumentUuids.For("org-a", "inv-mvd6eahnof6v"));
        Assert.NotEqual(first, DocumentUuids.For("org-b", "inv-mvd6eahnof6v"));
        Assert.Matches("^[0-9a-f]{8}-[0-9a-f]{4}-5[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$", first);
    }
}
