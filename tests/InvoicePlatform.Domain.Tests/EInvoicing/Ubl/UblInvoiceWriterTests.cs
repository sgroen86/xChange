using System.Runtime.CompilerServices;
using System.Xml.Linq;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.EInvoicing;
using InvoicePlatform.Domain.EInvoicing.Peppol;
using InvoicePlatform.Domain.EInvoicing.Ubl;

namespace InvoicePlatform.Domain.Tests.EInvoicing.Ubl;

/// <summary>
/// The writer's output must equal the reference files in Expected/. CI
/// validates those files with the official PEPPOL BIS 3.0.21 rules
/// (tools/peppol-validation), so together the two prove the writer produces
/// valid documents for these invoices.
///
/// After a deliberate change to the writer, regenerate the files with
/// XCHANGE_UPDATE_REFERENCE_FILES=1 dotnet test, review the diff, and run the
/// validator on them before committing.
/// </summary>
public class UblInvoiceWriterTests
{
    private static readonly XNamespace Cbc = UblInvoiceWriter.Cbc;
    private static readonly XNamespace Cac = UblInvoiceWriter.Cac;

    private readonly UblInvoiceWriter _writer = new();

    private string Write(CanonicalInvoiceDraft draft)
    {
        var result = ValidatedCanonicalInvoice.Validate(new PeppolBisBilling30Ruleset(), draft);
        Assert.Empty(result.Violations);
        return _writer.Write(result.Invoice!);
    }

    public static TheoryData<string> ReferenceFiles => new()
    {
        "invoice-nl-standard.xml",
        "creditnote-nl.xml",
        "invoice-nl-mixed-vat-allowances.xml",
    };

    private static CanonicalInvoiceDraft SampleFor(string file) => file switch
    {
        "invoice-nl-standard.xml" => SampleInvoices.Valid(),
        "creditnote-nl.xml" => SampleInvoices.CreditNote(),
        "invoice-nl-mixed-vat-allowances.xml" => SampleInvoices.MixedVatWithAllowances(),
        _ => throw new ArgumentOutOfRangeException(nameof(file)),
    };

    [Theory]
    [MemberData(nameof(ReferenceFiles))]
    public void Output_matches_the_reference_file(string file)
    {
        var actual = Write(SampleFor(file));

        if (Environment.GetEnvironmentVariable("XCHANGE_UPDATE_REFERENCE_FILES") == "1")
        {
            File.WriteAllText(Path.Combine(SourceDirectory(), "Expected", file), actual);
        }

        var expected = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "EInvoicing", "Ubl", "Expected", file));
        Assert.Equal(expected.ReplaceLineEndings("\n"), actual);
    }

    [Fact]
    public void Every_amount_has_the_invoice_currency_and_two_decimals()
    {
        var document = XDocument.Parse(Write(SampleInvoices.MixedVatWithAllowances()));

        var amounts = document.Descendants()
            .Where(e => e.Name.Namespace == Cbc && e.Name.LocalName.EndsWith("Amount", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(amounts);
        Assert.All(amounts, amount =>
        {
            Assert.Equal("EUR", (string?)amount.Attribute("currencyID"));
            if (amount.Name.LocalName != "PriceAmount")
            {
                Assert.Matches(@"^-?\d+\.\d{2}$", amount.Value);
            }
        });
    }

    [Fact]
    public void A_price_keeps_its_own_precision()
    {
        var draft = SampleInvoices.Valid() with
        {
            Lines = [SampleInvoices.Valid().Lines[0] with { Quantity = 3m, UnitPrice = 0.3350m }],
        };

        var document = XDocument.Parse(Write(draft));

        Assert.Equal("0.3350", document.Descendants(Cbc + "PriceAmount").Single().Value);
        Assert.Equal("1.01", document.Descendants(Cac + "InvoiceLine").Single().Element(Cbc + "LineExtensionAmount")!.Value);
    }

    [Fact]
    public void Special_characters_are_escaped()
    {
        var draft = SampleInvoices.Valid() with { Note = "A & B <C> \"D\"" };

        var document = XDocument.Parse(Write(draft));

        Assert.Equal("A & B <C> \"D\"", document.Root!.Element(Cbc + "Note")!.Value);
    }

    [Fact]
    public void Absent_fields_are_omitted_not_defaulted()
    {
        var draft = SampleInvoices.Valid() with { Note = "  ", DueDate = null, BuyerReference = null, PurchaseOrderReference = "INK-1" };

        var document = XDocument.Parse(Write(draft));
        var root = document.Root!;

        Assert.Null(root.Element(Cbc + "Note"));
        Assert.Null(root.Element(Cbc + "DueDate"));
        Assert.Null(root.Element(Cbc + "BuyerReference"));
        Assert.Equal("INK-1", root.Element(Cac + "OrderReference")!.Element(Cbc + "ID")!.Value);
        Assert.DoesNotContain(document.Descendants(), e => !e.HasElements && string.IsNullOrWhiteSpace(e.Value));
    }

    [Fact]
    public void A_credit_note_uses_the_credit_note_vocabulary()
    {
        var document = XDocument.Parse(Write(SampleInvoices.CreditNote() with { DueDate = "2026-11-15" }));
        var root = document.Root!;

        Assert.Equal("CreditNote", root.Name.LocalName);
        Assert.Equal(UblInvoiceWriter.CreditNoteNamespace, root.Name.NamespaceName);
        Assert.Equal("381", root.Element(Cbc + "CreditNoteTypeCode")!.Value);
        Assert.Null(root.Element(Cbc + "DueDate"));
        Assert.Equal("2026-11-15", root.Element(Cac + "PaymentMeans")!.Element(Cbc + "PaymentDueDate")!.Value);
        Assert.NotNull(document.Descendants(Cac + "CreditNoteLine").Single().Element(Cbc + "CreditedQuantity"));
        Assert.Equal("F-2026-041", root.Element(Cac + "BillingReference")!.Descendants(Cbc + "ID").Single().Value);
    }

    [Fact]
    public void Identifiers_come_from_the_ruleset()
    {
        var root = XDocument.Parse(Write(SampleInvoices.Valid())).Root!;
        var ruleset = new PeppolBisBilling30Ruleset();

        Assert.Equal(ruleset.CustomizationId, root.Element(Cbc + "CustomizationID")!.Value);
        Assert.Equal(ruleset.ProfileId, root.Element(Cbc + "ProfileID")!.Value);
    }

    [Fact]
    public void Category_o_carries_no_rate()
    {
        var draft = SampleInvoices.Valid() with
        {
            Seller = SampleInvoices.Valid().Seller! with { VatIdentifier = null },
            Buyer = SampleInvoices.Valid().Buyer! with { VatIdentifier = null },
            Lines = [.. SampleInvoices.Valid().Lines.Select(l => l with { VatCategoryCode = "O", VatPercentage = null })],
            VatBreakdown = [new CanonicalVatBreakdownLine { VatCategoryCode = "O", ExemptionReason = "Niet onderworpen aan btw" }],
        };

        var document = XDocument.Parse(Write(draft));

        Assert.Empty(document.Descendants(Cbc + "Percent"));
    }

    private static string SourceDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
}
