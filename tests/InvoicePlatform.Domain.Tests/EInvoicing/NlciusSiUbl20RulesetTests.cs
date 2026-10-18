using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.EInvoicing;
using InvoicePlatform.Domain.EInvoicing.Nlcius;

namespace InvoicePlatform.Domain.Tests.EInvoicing;

/// <summary>
/// NLCIUS as SI-UBL 2.0: the Dutch samples are valid, and each runtime BR-NL
/// rule blocks the export when exactly that rule is broken.
/// </summary>
public class NlciusSiUbl20RulesetTests
{
    private static readonly NlciusSiUbl20Ruleset Ruleset = new();

    private static InvoiceValidationResult Validate(CanonicalInvoiceDraft draft) => ValidatedCanonicalInvoice.Validate(Ruleset, draft);

    [Fact]
    public void The_dutch_samples_are_valid()
    {
        Assert.Empty(Validate(SampleInvoices.Valid()).Violations);
        Assert.Empty(Validate(SampleInvoices.CreditNote()).Violations);
    }

    [Fact]
    public void Identifiers_are_those_of_nlcius()
    {
        Assert.Equal("urn:cen.eu:en16931:2017#compliant#urn:fdc:nen.nl:nlcius:v1.0", Ruleset.CustomizationId);
        Assert.Equal("VAT", Ruleset.Ubl.TaxSchemeId);
    }

    [Fact]
    public void Fields_nlcius_advises_against_are_left_out_of_the_file()
    {
        var draft = SampleInvoices.Valid();
        draft = draft with { Payment = draft.Payment! with { PaymentMeansText = "Overboeking" } };

        var payment = Validate(draft).Invoice!.Invoice.Payment!;

        Assert.Null(payment.AccountName);
        Assert.Null(payment.PaymentMeansText);
        Assert.Equal("NL91ABNA0417164300", payment.Iban);
    }

    public static TheoryData<string, string, Func<CanonicalInvoiceDraft, CanonicalInvoiceDraft>> Breaks => new()
    {
        { "BR-NL-1", "seller.legalRegistrationId", d => d with { Seller = d.Seller! with { LegalRegistrationId = null } } },
        { "BR-NL-1", "seller.legalRegistrationScheme", d => d with { Seller = d.Seller! with { LegalRegistrationScheme = "0208" } } },
        { "BR-NL-2", "buyerReference", d => d with { BuyerReference = null, PurchaseOrderReference = null } },
        { "BR-NL-3", "seller.address.postalZone", d => d with { Seller = d.Seller! with { Address = d.Seller.Address! with { PostalZone = null } } } },
        { "BR-NL-4", "buyer.address.streetName", d => d with { Buyer = d.Buyer! with { Address = d.Buyer.Address! with { StreetName = null } } } },
        { "BR-NL-7", "typeCode", d => d with { TypeCode = "386" } },
        { "BR-NL-9", "precedingInvoiceReference", d => d with { TypeCode = "384", PrecedingInvoiceReference = null } },
        { "BR-NL-10", "buyer.legalRegistrationId", d => d with { Buyer = d.Buyer! with { LegalRegistrationId = null, LegalRegistrationScheme = null } } },
        { "BR-NL-11", "payment.paymentMeansCode", d => d with { Payment = null } },
        { "BR-NL-12", "payment.paymentMeansCode", d => d with { Payment = d.Payment! with { PaymentMeansCode = "1" } } },
        { "XC-VAT-NL", "buyer.vatIdentifier", d => d with { Buyer = d.Buyer! with { VatIdentifier = "NL81491234B01" } } },
    };

    [Theory]
    [MemberData(nameof(Breaks))]
    public void Breaking_a_rule_blocks_the_export(string ruleId, string path, Func<CanonicalInvoiceDraft, CanonicalInvoiceDraft> breakIt)
    {
        var result = Validate(breakIt(SampleInvoices.Valid()));

        Assert.Null(result.Invoice);
        Assert.Contains(result.Violations, v => v.RuleId == ruleId && v.Path == path);
    }

    [Fact]
    public void A_purchase_order_number_is_enough_without_a_buyer_reference()
        => Assert.DoesNotContain(
            Validate(SampleInvoices.Valid() with { BuyerReference = null, PurchaseOrderReference = "PO-1" }).Violations,
            v => v.RuleId == "BR-NL-2");

    [Fact]
    public void The_dutch_rules_do_not_apply_to_a_seller_outside_the_netherlands()
        => Assert.DoesNotContain(Validate(SampleInvoices.Belgium()).Violations, v => v.RuleId.StartsWith("BR-NL-", StringComparison.Ordinal));
}
