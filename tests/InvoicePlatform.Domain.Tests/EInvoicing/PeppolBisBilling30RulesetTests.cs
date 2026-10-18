using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.EInvoicing;
using InvoicePlatform.Domain.EInvoicing.Peppol;

namespace InvoicePlatform.Domain.Tests.EInvoicing;

/// <summary>
/// One case per runtime rule: start from a valid invoice, break exactly that
/// rule, and the export is blocked with that rule id on that field.
/// </summary>
public class PeppolBisBilling30RulesetTests
{
    private static readonly PeppolBisBilling30Ruleset Ruleset = new();

    private static IReadOnlyList<RuleViolation> Violations(CanonicalInvoiceDraft draft)
        => ValidatedCanonicalInvoice.Validate(Ruleset, draft).Violations;

    public static TheoryData<string> Samples => new() { "valid", "credit note", "mixed" };

    [Theory]
    [MemberData(nameof(Samples))]
    public void The_samples_are_valid(string sample)
    {
        var draft = sample switch
        {
            "valid" => SampleInvoices.Valid(),
            "credit note" => SampleInvoices.CreditNote(),
            _ => SampleInvoices.MixedVatWithAllowances(),
        };

        Assert.Empty(Violations(draft));
    }

    [Fact]
    public void Identifiers_are_those_of_bis_billing_3_without_a_point_release()
    {
        Assert.Equal("urn:cen.eu:en16931:2017#compliant#urn:fdc:peppol.eu:2017:poacc:billing:3.0", Ruleset.CustomizationId);
        Assert.Equal("urn:fdc:peppol.eu:2017:poacc:billing:01:1.0", Ruleset.ProfileId);
        Assert.Equal("3.0.21", Ruleset.RulesRelease);
    }

    [Fact]
    public void The_system_default_is_peppol_bis_billing_3()
        => Assert.Equal(PeppolBisBilling30Ruleset.RulesetId, new InvoiceRulesetRegistry().Default.Id);

    [Fact]
    public void Violations_come_in_a_stable_order_without_duplicates()
    {
        var draft = SampleInvoices.Valid() with { InvoiceNumber = null, Seller = null };

        var violations = Violations(draft);

        Assert.Equal(violations.OrderBy(v => v.Path, StringComparer.Ordinal).ThenBy(v => v.RuleId, StringComparer.Ordinal), violations);
        Assert.Equal(violations.Count, violations.Select(v => (v.RuleId, v.Path)).Distinct().Count());
    }

    public static TheoryData<string, string, Func<CanonicalInvoiceDraft, CanonicalInvoiceDraft>> Breaks => new()
    {
        // Header
        { "BR-02", "invoiceNumber", d => d with { InvoiceNumber = " " } },
        { "BR-03", "issueDate", d => d with { IssueDate = null } },
        { "BR-04", "typeCode", d => d with { TypeCode = null } },
        { "BR-05", "currencyCode", d => d with { CurrencyCode = null } },
        { "BR-CL-04", "currencyCode", d => d with { CurrencyCode = "eur" } },
        { "PEPPOL-EN16931-P0100", "typeCode", d => d with { TypeCode = "389" } },
        { "PEPPOL-EN16931-P0112", "typeCode", d => d with { TypeCode = "326" } },
        { "PEPPOL-EN16931-F001", "issueDate", d => d with { IssueDate = "01-10-2026" } },
        { "PEPPOL-EN16931-F001", "dueDate", d => d with { DueDate = "2026-02-30" } },
        { "PEPPOL-EN16931-R003", "buyerReference", d => d with { BuyerReference = null, PurchaseOrderReference = null } },

        // Parties
        { "BR-06", "seller.name", d => d with { Seller = d.Seller! with { Name = null } } },
        { "BR-07", "buyer.name", d => d with { Buyer = d.Buyer! with { Name = "" } } },
        { "BR-09", "seller.address.countryCode", d => d with { Seller = d.Seller! with { Address = d.Seller.Address! with { CountryCode = null } } } },
        { "BR-11", "buyer.address.countryCode", d => d with { Buyer = d.Buyer! with { Address = null } } },
        { "BR-CL-14", "buyer.address.countryCode", d => d with { Buyer = d.Buyer! with { Address = d.Buyer.Address! with { CountryCode = "NLD" } } } },
        { "BR-CO-26", "seller.vatIdentifier", d => d with { Seller = d.Seller! with { VatIdentifier = null, LegalRegistrationId = null } } },
        { "BR-CO-09", "buyer.vatIdentifier", d => d with { Buyer = d.Buyer! with { VatIdentifier = "814912345B01" } } },
        { "BR-62", "seller.electronicAddressScheme", d => d with { Seller = d.Seller! with { ElectronicAddressScheme = null } } },
        { "BR-63", "buyer.electronicAddressScheme", d => d with { Buyer = d.Buyer! with { ElectronicAddressScheme = null } } },
        { "BR-CL-25", "buyer.electronicAddressScheme", d => d with { Buyer = d.Buyer! with { ElectronicAddressScheme = "0217" } } },
        { "BR-CL-11", "buyer.legalRegistrationScheme", d => d with { Buyer = d.Buyer! with { LegalRegistrationScheme = "KVK" } } },
        { "PEPPOL-EN16931-R010", "buyer.electronicAddress", d => d with { Buyer = d.Buyer! with { ElectronicAddress = null } } },
        { "PEPPOL-EN16931-R020", "seller.electronicAddress", d => d with { Seller = d.Seller! with { ElectronicAddress = null } } },

        // Lines
        { "BR-16", "lines", d => d with { Lines = [] } },
        { "BR-22", "lines[0].quantity", d => WithLine(d, l => l with { Quantity = null, NetAmount = 1250.00m }) },
        { "BR-23", "lines[0].unitCode", d => WithLine(d, l => l with { UnitCode = null }) },
        { "BR-CL-23", "lines[0].unitCode", d => WithLine(d, l => l with { UnitCode = "stuks" }) },
        { "BR-25", "lines[0].description", d => WithLine(d, l => l with { Description = null }) },
        { "BR-26", "lines[0].unitPrice", d => WithLine(d, l => l with { UnitPrice = null, NetAmount = 1250.00m }) },
        { "BR-27", "lines[0].unitPrice", d => WithLine(d, l => l with { UnitPrice = -1m }) },
        { "BR-DEC-23", "lines[0].netAmount", d => WithLine(d, l => l with { Quantity = null, NetAmount = 10.005m }) },
        { "BR-CO-04", "lines[0].vatCategoryCode", d => WithLine(d, l => l with { VatCategoryCode = null }) },
        { "BR-CL-18", "lines[0].vatCategoryCode", d => WithLine(d, l => l with { VatCategoryCode = "X" }) },
        { "PEPPOL-EN16931-R121", "lines[0].baseQuantity", d => WithLine(d, l => l with { BaseQuantity = -1m }) },
        { "BR-41", "lines[0].allowancesAndCharges[0].amount", d => WithLine(d, l => l with { AllowancesAndCharges = [new() { Reason = "Korting" }] }) },
        { "BR-44", "lines[0].allowancesAndCharges[0].reason", d => WithLine(d, l => l with { AllowancesAndCharges = [new() { IsCharge = true, Amount = 1m }] }) },
        { "BR-DEC-24", "lines[0].allowancesAndCharges[0].amount", d => WithLine(d, l => l with { AllowancesAndCharges = [new() { Amount = 1.001m, Reason = "K" }] }) },
        { "PEPPOL-EN16931-R041", "lines[0].allowancesAndCharges[0].baseAmount", d => WithLine(d, l => l with { AllowancesAndCharges = [new() { Amount = 1m, Percentage = 10m, Reason = "K" }] }) },
        { "PEPPOL-EN16931-R042", "lines[0].allowancesAndCharges[0].percentage", d => WithLine(d, l => l with { AllowancesAndCharges = [new() { Amount = 1m, BaseAmount = 10m, Reason = "K" }] }) },
        { "PEPPOL-EN16931-R040", "lines[0].allowancesAndCharges[0].amount", d => WithLine(d, l => l with { AllowancesAndCharges = [new() { Amount = 5m, BaseAmount = 10m, Percentage = 10m, Reason = "K" }] }) },

        // Document allowances and charges
        { "BR-31", "allowancesAndCharges[0].amount", d => d with { AllowancesAndCharges = [new() { Reason = "K", VatCategoryCode = "S", VatPercentage = 21m }] } },
        { "BR-37", "allowancesAndCharges[0].vatCategoryCode", d => d with { AllowancesAndCharges = [new() { IsCharge = true, Amount = 5m, Reason = "Vracht" }] } },
        { "BR-33", "allowancesAndCharges[0].reason", d => d with { AllowancesAndCharges = [new() { Amount = 5m, VatCategoryCode = "S", VatPercentage = 21m }] } },
        { "BR-DEC-05", "allowancesAndCharges[0].amount", d => d with { AllowancesAndCharges = [new() { IsCharge = true, Amount = 5.555m, Reason = "V", VatCategoryCode = "S", VatPercentage = 21m }] } },
        { "BR-S-07", "allowancesAndCharges[0].vatPercentage", d => d with { AllowancesAndCharges = [new() { IsCharge = true, Amount = 5m, Reason = "V", VatCategoryCode = "S" }] } },

        // VAT categories
        { "BR-S-05", "lines[0].vatPercentage", d => WithLine(d, l => l with { VatPercentage = 0m }) },
        { "BR-Z-05", "lines[0].vatPercentage", d => WithLine(d, l => l with { VatCategoryCode = "Z", VatPercentage = 21m }) },
        { "BR-S-02", "seller.vatIdentifier", d => d with { Seller = d.Seller! with { VatIdentifier = null } } },
        { "BR-AE-02", "buyer.vatIdentifier", d => Exempt(d, "AE") with { Buyer = d.Buyer! with { VatIdentifier = null, LegalRegistrationId = null } } },
        { "BR-G-02", "seller.vatIdentifier", d => Exempt(d, "G") with { Seller = d.Seller! with { VatIdentifier = null } } },
        { "BR-O-02", "seller.vatIdentifier", d => Exempt(d, "O") },
        { "BR-O-12", "lines", d => d with { Lines = [d.Lines[0], d.Lines[1] with { VatCategoryCode = "O", VatPercentage = null }] } },
        { "BR-E-10", "vatBreakdown[0].exemptionReason", d => WithLine(d, l => l with { VatCategoryCode = "E", VatPercentage = 0m }) with { Lines = [d.Lines[0] with { VatCategoryCode = "E", VatPercentage = 0m }] } },
        { "BR-S-10", "vatBreakdown[0].exemptionReason", d => d with { VatBreakdown = [new() { VatCategoryCode = "S", VatPercentage = 9m, ExemptionReason = "Onterecht" }] } },
        { "XC-VAT-UNSUPPORTED", "lines[0].vatCategoryCode", d => WithLine(d, l => l with { VatCategoryCode = "K", VatPercentage = 0m }) },

        // Payment
        { "BR-49", "payment.paymentMeansCode", d => d with { Payment = d.Payment! with { PaymentMeansCode = null } } },
        { "BR-CL-16", "payment.paymentMeansCode", d => d with { Payment = d.Payment! with { PaymentMeansCode = "99" } } },
        { "BR-61", "payment.iban", d => d with { Payment = d.Payment! with { Iban = null } } },
        { "PEPPOL-EN16931-R061", "payment.paymentMeansCode", d => d with { Payment = d.Payment! with { PaymentMeansCode = "59" } } },
        { "BR-DEC-16", "totals.prepaidAmount", d => d with { Totals = new CanonicalTotals { PrepaidAmount = 0.001m } } },

        // Dutch rules
        { "NL-R-001", "precedingInvoiceReference", d => d with { TypeCode = "381", PrecedingInvoiceReference = null } },
        { "NL-R-002", "seller.address.postalZone", d => d with { Seller = d.Seller! with { Address = d.Seller.Address! with { PostalZone = null } } } },
        { "NL-R-003", "seller.legalRegistrationScheme", d => d with { Seller = d.Seller! with { LegalRegistrationScheme = "0088" } } },
        { "NL-R-004", "buyer.address.streetName", d => d with { Buyer = d.Buyer! with { Address = d.Buyer.Address! with { StreetName = null } } } },
        { "NL-R-005", "buyer.legalRegistrationScheme", d => d with { Buyer = d.Buyer! with { LegalRegistrationScheme = null } } },
        { "NL-R-007", "payment.paymentMeansCode", d => d with { Payment = null } },
        { "NL-R-008", "payment.paymentMeansCode", d => d with { Payment = d.Payment! with { PaymentMeansCode = "31" } } },
    };

    [Theory]
    [MemberData(nameof(Breaks))]
    public void Breaking_a_rule_blocks_the_export(string ruleId, string path, Func<CanonicalInvoiceDraft, CanonicalInvoiceDraft> breakIt)
    {
        var result = ValidatedCanonicalInvoice.Validate(Ruleset, breakIt(SampleInvoices.Valid()));

        Assert.Null(result.Invoice);
        Assert.Contains(result.Violations, v => v.RuleId == ruleId && v.Path == path);
        Assert.All(result.Violations, v => Assert.False(string.IsNullOrWhiteSpace(v.Message)));
    }

    [Fact]
    public void Dutch_rules_do_not_apply_to_a_foreign_seller()
    {
        var draft = SampleInvoices.Valid() with
        {
            Seller = SampleInvoices.Valid().Seller! with
            {
                VatIdentifier = "BE0123456749",
                LegalRegistrationScheme = "0208",
                Address = new CanonicalAddress { CityName = "Brussel", CountryCode = "BE" },
            },
            Payment = null,
        };

        Assert.DoesNotContain(Violations(draft), v => v.RuleId.StartsWith("NL-", StringComparison.Ordinal));
    }

    [Fact]
    public void Nothing_to_pay_needs_no_payment_means()
    {
        var draft = SampleInvoices.Valid() with { Payment = null, Totals = new CanonicalTotals { PrepaidAmount = 2400.90m } };

        Assert.DoesNotContain(Violations(draft), v => v.RuleId == "NL-R-007");
    }

    private static CanonicalInvoiceDraft WithLine(CanonicalInvoiceDraft draft, Func<CanonicalInvoiceLine, CanonicalInvoiceLine> change)
        => draft with { Lines = [change(draft.Lines[0]), .. draft.Lines.Skip(1)] };

    /// <summary>All lines in one zero-rate category, with an exemption reason, so only the rule under test fires.</summary>
    private static CanonicalInvoiceDraft Exempt(CanonicalInvoiceDraft draft, string category)
        => draft with
        {
            Lines = [.. draft.Lines.Select(l => l with { VatCategoryCode = category, VatPercentage = category == "O" ? null : 0m })],
            VatBreakdown = [new CanonicalVatBreakdownLine { VatCategoryCode = category, VatPercentage = 0m, ExemptionReason = "Reden" }],
        };
}
