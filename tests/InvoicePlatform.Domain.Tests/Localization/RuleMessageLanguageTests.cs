using InvoicePlatform.Domain.EInvoicing;
using InvoicePlatform.Domain.EInvoicing.Peppol;
using InvoicePlatform.Domain.Localization;
using InvoicePlatform.Domain.Organizations;
using InvoicePlatform.Domain.Tests.EInvoicing;

namespace InvoicePlatform.Domain.Tests.Localization;

/// <summary>Only the text of a rule follows the language; the rule id, the field and the outcome do not.</summary>
public class RuleMessageLanguageTests
{
    private static readonly PeppolBisBilling30Ruleset Ruleset = new();

    private static RuleViolation MissingInvoiceNumber()
        => Assert.Single(
            ValidatedCanonicalInvoice.Validate(Ruleset, SampleInvoices.Valid() with { InvoiceNumber = " " }).Violations,
            v => v.RuleId == "BR-02");

    [Fact]
    public void The_same_rule_speaks_english_under_an_english_scope()
    {
        var dutch = MissingInvoiceNumber();

        RuleViolation english;
        using (Texts.Use(Language.En))
        {
            english = MissingInvoiceNumber();
        }

        Assert.Equal("Factuurnummer ontbreekt.", dutch.Message);
        Assert.Equal("The invoice number is missing.", english.Message);
        Assert.Equal((dutch.RuleId, dutch.BusinessTerm, dutch.Path), (english.RuleId, english.BusinessTerm, english.Path));
    }

    [Fact]
    public void Screen_names_follow_the_language_but_codes_do_not()
    {
        var dutch = (Ruleset.DisplayName, Ruleset.TaxName, Ruleset.TaxCategories, Countries.Find("BE")!.Name);

        using (Texts.Use(Language.En))
        {
            Assert.Equal("PEPPOL BIS Billing 3.0 (UK, Belgium, Netherlands)", Ruleset.DisplayName);
            Assert.Equal("VAT", Ruleset.TaxName);
            Assert.Equal("Belgium", Countries.Find("BE")!.Name);
            Assert.Equal(dutch.TaxCategories.Select(c => c.Code), Ruleset.TaxCategories.Select(c => c.Code));
            Assert.Equal("reverse charge", Ruleset.TaxCategories.Single(c => c.Code == "AE").Label);
        }

        Assert.Equal(("PEPPOL BIS Billing 3.0 (VK, België, Nederland)", "btw", "België"), (dutch.DisplayName, dutch.TaxName, dutch.Name));
        Assert.Equal("btw verlegd", dutch.TaxCategories.Single(c => c.Code == "AE").Label);
    }
}
