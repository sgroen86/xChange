using System.Xml.Linq;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.Serialization;

namespace InvoicePlatform.Domain.Tests;

/// <summary>
/// These tests exist to prove the serializer is a pure function of the draft:
/// no model output reaches the XML, and no sample data is baked into it.
/// </summary>
public class CanonicalInvoiceXmlSerializerTests
{
    private readonly CanonicalInvoiceXmlSerializer _serializer = new();

    private static XDocument Parse(string xml) => XDocument.Parse(xml);

    private static string? Value(XDocument document, string localName)
        => document.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == localName)
            ?.Value;

    [Fact]
    public void Xml_values_come_from_the_draft()
    {
        var draft = new CanonicalInvoiceDraft
        {
            TypeCode = "380",
            InvoiceNumber = "2026-000451",
            IssueDate = "2026-03-04",
            DueDate = "2026-04-03",
            CurrencyCode = "EUR",
            Seller = new CanonicalParty
            {
                Name = "Van Dijk Techniek B.V.",
                VatIdentifier = "NL814912345B01",
                Address = new CanonicalAddress { CityName = "Utrecht", CountryCode = "NL" },
            },
            Buyer = new CanonicalParty { Name = "Green IT Solutions B.V." },
            Lines =
            [
                new CanonicalInvoiceLine
                {
                    Description = "Onderhoudscontract",
                    Quantity = 2m,
                    UnitCode = "C62",
                    UnitPrice = 125.00m,
                    NetAmount = 250.00m,
                    VatCategoryCode = "S",
                    VatPercentage = 21m,
                },
            ],
            Totals = new CanonicalTotals
            {
                TaxExclusiveAmount = 250.00m,
                TaxAmount = 52.50m,
                TaxInclusiveAmount = 302.50m,
                PayableAmount = 302.50m,
            },
        };

        var document = Parse(_serializer.Serialize(draft));

        Assert.Equal("380", Value(document, "TypeCode"));
        Assert.Equal("2026-000451", Value(document, "InvoiceNumber"));
        Assert.Equal("2026-03-04", Value(document, "IssueDate"));
        Assert.Equal("2026-04-03", Value(document, "DueDate"));
        Assert.Equal("EUR", Value(document, "CurrencyCode"));
        Assert.Equal("Van Dijk Techniek B.V.", Value(document, "Name"));
        Assert.Equal("NL814912345B01", Value(document, "VatIdentifier"));
        Assert.Equal("Onderhoudscontract", Value(document, "Description"));
        Assert.Equal("302.50", Value(document, "PayableAmount"));
    }

    [Fact]
    public void Changing_the_draft_changes_the_xml()
    {
        // The strongest available statement that the output is derived, not fixed.
        var first = _serializer.Serialize(new CanonicalInvoiceDraft { InvoiceNumber = "AAA-1" });
        var second = _serializer.Serialize(new CanonicalInvoiceDraft { InvoiceNumber = "BBB-2" });

        Assert.Contains("AAA-1", first, StringComparison.Ordinal);
        Assert.DoesNotContain("BBB-2", first, StringComparison.Ordinal);
        Assert.Contains("BBB-2", second, StringComparison.Ordinal);
        Assert.DoesNotContain("AAA-1", second, StringComparison.Ordinal);
    }

    [Fact]
    public void Xml_contains_no_hardcoded_mock_invoice_values()
    {
        // An empty draft must produce a document with no data in it at all. If a
        // sample value were ever hardcoded in the serializer, it would show up
        // here even though the caller supplied nothing.
        var xml = _serializer.Serialize(new CanonicalInvoiceDraft());

        string[] mockValues =
        [
            // Values from the frontend mock and from the test above.
            "F-00060", "Van Dijk Techniek", "Green IT Solutions", "Onderhoudscontract",
            "NL814912345B01", "NL857231409B01", "Keizersgracht", "Industrieweg",
            "1250.00", "89.50", "34.95", "302.50", "2026-08-14", "2026-09-13",
            "EUR", "380", "mock",
        ];

        foreach (var value in mockValues)
        {
            Assert.DoesNotContain(value, xml, StringComparison.OrdinalIgnoreCase);
        }

        var document = Parse(xml);
        Assert.Equal("CanonicalInvoice", document.Root!.Name.LocalName);
        Assert.Empty(document.Root.Elements());
    }

    [Fact]
    public void Special_xml_characters_are_escaped()
    {
        var draft = new CanonicalInvoiceDraft
        {
            InvoiceNumber = "A&B <script>alert(\"x\")</script>",
            Note = "5 > 3 && 2 < 4 -- it's \"quoted\"",
            Seller = new CanonicalParty { Name = "Smith & Sons </Name><Injected>bad</Injected>" },
        };

        var xml = _serializer.Serialize(draft);

        // The raw markup must not survive into the document.
        Assert.DoesNotContain("<script>", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("<Injected>", xml, StringComparison.Ordinal);
        Assert.Contains("&amp;", xml, StringComparison.Ordinal);
        Assert.Contains("&lt;", xml, StringComparison.Ordinal);

        // And it must still parse, with the original text recovered intact.
        var document = Parse(xml);
        Assert.Equal("A&B <script>alert(\"x\")</script>", Value(document, "InvoiceNumber"));
        Assert.Equal("5 > 3 && 2 < 4 -- it's \"quoted\"", Value(document, "Note"));
        Assert.Equal("Smith & Sons </Name><Injected>bad</Injected>", Value(document, "Name"));
        Assert.Null(document.Descendants().FirstOrDefault(e => e.Name.LocalName == "Injected"));
    }

    [Fact]
    public void Decimal_values_retain_their_precision()
    {
        var draft = new CanonicalInvoiceDraft
        {
            Lines =
            [
                new CanonicalInvoiceLine
                {
                    // Trailing zeros are significant on an invoice and must survive.
                    UnitPrice = 10.50m,
                    Quantity = 3.000m,
                    NetAmount = 31.50m,
                },
            ],
            Totals = new CanonicalTotals
            {
                // A value that cannot be represented exactly as a double.
                TaxExclusiveAmount = 1234.10m,
                TaxAmount = 0.005m,
                // More precision than money normally carries.
                PayableAmount = 1234.105000m,
                RoundingAmount = -0.01m,
            },
        };

        var document = Parse(_serializer.Serialize(draft));

        Assert.Equal("10.50", Value(document, "UnitPrice"));
        Assert.Equal("3.000", Value(document, "Quantity"));
        Assert.Equal("1234.10", Value(document, "TaxExclusiveAmount"));
        Assert.Equal("0.005", Value(document, "TaxAmount"));
        Assert.Equal("1234.105000", Value(document, "PayableAmount"));
        Assert.Equal("-0.01", Value(document, "RoundingAmount"));
    }

    [Fact]
    public void Decimal_values_are_written_with_an_invariant_decimal_separator()
    {
        // Guards against a comma separator leaking in under a Dutch or German
        // locale, which would silently produce an invalid document.
        var original = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("nl-NL");

            var xml = _serializer.Serialize(new CanonicalInvoiceDraft
            {
                Totals = new CanonicalTotals { PayableAmount = 1234.56m },
            });

            Assert.Contains("1234.56", xml, StringComparison.Ordinal);
            Assert.DoesNotContain("1234,56", xml, StringComparison.Ordinal);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }

    [Fact]
    public void Missing_optional_fields_do_not_break_serialization()
    {
        // Nothing but a single almost-empty line: every other field is null.
        var draft = new CanonicalInvoiceDraft
        {
            Lines = [new CanonicalInvoiceLine { Description = "Only a description" }],
        };

        var xml = _serializer.Serialize(draft);
        var document = Parse(xml);

        Assert.Equal("Only a description", Value(document, "Description"));

        // Absent fields are omitted rather than emitted empty, so a consumer can
        // tell "not on the invoice" from "present and blank".
        Assert.Null(Value(document, "InvoiceNumber"));
        Assert.Null(Value(document, "Totals"));
        Assert.Null(Value(document, "UnitPrice"));
        Assert.DoesNotContain(document.Descendants(), e => e.Name.LocalName == "Seller");
    }

    [Fact]
    public void A_completely_empty_draft_still_produces_a_valid_document()
    {
        var xml = _serializer.Serialize(new CanonicalInvoiceDraft());

        var document = Parse(xml);
        Assert.Equal(
            CanonicalInvoiceXmlSerializer.Namespace,
            document.Root!.Name.NamespaceName);
    }

    [Fact]
    public void Serialization_is_deterministic()
    {
        var draft = new CanonicalInvoiceDraft
        {
            InvoiceNumber = "DET-1",
            Lines = [new CanonicalInvoiceLine { NetAmount = 1.23m }],
        };

        Assert.Equal(_serializer.Serialize(draft), _serializer.Serialize(draft));
    }
}
