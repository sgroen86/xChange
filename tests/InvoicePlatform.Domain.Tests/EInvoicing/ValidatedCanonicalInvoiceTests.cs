using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.EInvoicing;

namespace InvoicePlatform.Domain.Tests.EInvoicing;

public class ValidatedCanonicalInvoiceTests
{
    [Fact]
    public void An_incomplete_invoice_does_not_validate()
    {
        var result = ValidatedCanonicalInvoice.Validate(new TestRuleset(), new CanonicalInvoiceDraft());

        Assert.Null(result.Invoice);
        Assert.Equal("T-1", Assert.Single(result.Violations).RuleId);
    }

    [Fact]
    public void A_complete_invoice_validates_carries_its_ruleset_and_calculated_totals()
    {
        var result = ValidatedCanonicalInvoice.Validate(new TestRuleset(), new CanonicalInvoiceDraft
        {
            InvoiceNumber = "F-1",
            Lines = [new CanonicalInvoiceLine { Quantity = 2m, UnitPrice = 5m, VatCategoryCode = "S", VatPercentage = 21m }],
        });

        Assert.NotNull(result.Invoice);
        Assert.Empty(result.Violations);
        Assert.Equal("test", result.Invoice!.Ruleset.Id);
        Assert.Equal(12.10m, result.Invoice.Invoice.Totals!.PayableAmount);
    }

    [Fact]
    public void Rules_see_the_calculated_invoice_not_the_raw_draft()
    {
        var ruleset = new TestRuleset { RequireTotals = true };

        var result = ValidatedCanonicalInvoice.Validate(ruleset, new CanonicalInvoiceDraft { InvoiceNumber = "F-1" });

        Assert.Empty(result.Violations);
    }

    internal sealed class TestRuleset(string id = "test") : IInvoiceRuleset
    {
        public bool RequireTotals { get; init; }

        public string Id => id;
        public string DisplayName => "Test " + id;
        public string RulesRelease => "1";
        public InvoiceSyntax Syntax => InvoiceSyntax.Ubl21;
        public string CustomizationId => "urn:test:" + id;
        public string ProfileId => "urn:test:profile";
        public string TaxName => "btw";
        public IReadOnlyList<TaxCategory> TaxCategories => [new("S", "standaard")];
        public string? TaxCurrency => null;
        public UblDialect Ubl => new("VAT", new HashSet<string> { "O" }, false, false);
        public CanonicalInvoiceDraft Prepare(CanonicalInvoiceDraft draft) => draft;

        public IReadOnlyList<RuleViolation> Check(CanonicalInvoiceDraft invoice)
        {
            var violations = new List<RuleViolation>();
            if (invoice.InvoiceNumber is null)
            {
                violations.Add(new RuleViolation("T-1", "BT-1", "invoiceNumber", "Factuurnummer ontbreekt."));
            }

            if (RequireTotals && invoice.Totals is null)
            {
                violations.Add(new RuleViolation("T-2", null, "totals", "Totalen ontbreken."));
            }

            return violations;
        }
    }
}
