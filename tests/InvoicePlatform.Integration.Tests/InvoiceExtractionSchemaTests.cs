using System.Text.Json;
using InvoicePlatform.Infrastructure.Anthropic;

namespace InvoicePlatform.Integration.Tests;

/// <summary>
/// The API refuses a schema whose compiled grammar is too large, and only says
/// so when a request is sent. The single schema before the split had 82 fields
/// (every $ref written out) and was accepted; neither half may grow past it.
/// </summary>
public class InvoiceExtractionSchemaTests
{
    private const int AcceptedFieldCount = 82;

    [Fact]
    public void Neither_schema_is_larger_than_the_one_the_api_accepted()
    {
        Assert.InRange(Count(InvoiceExtractionSchema.Header()), 1, AcceptedFieldCount);
        Assert.InRange(Count(InvoiceExtractionSchema.Lines()), 1, AcceptedFieldCount);
    }

    [Fact]
    public void The_header_has_no_lines_and_the_lines_nothing_but_lines()
    {
        var header = Properties(InvoiceExtractionSchema.Header());
        var lines = Properties(InvoiceExtractionSchema.Lines());

        Assert.DoesNotContain("lines", header);
        Assert.Contains("seller", header);
        Assert.Contains("totals", header);
        Assert.Contains("evidence", header);
        Assert.Equal(["lines", "evidence"], lines);
    }

    [Fact]
    public void The_fields_that_came_back_are_asked_for()
    {
        var header = Paths(InvoiceExtractionSchema.Header());
        var lines = Paths(InvoiceExtractionSchema.Lines());

        foreach (var side in new[] { "seller", "buyer" })
        {
            Assert.Contains($"{side}.contactName", header);
            Assert.Contains($"{side}.contactEmail", header);
            Assert.Contains($"{side}.contactPhone", header);
            Assert.Contains($"{side}.address.additionalStreetName", header);
        }

        Assert.Contains("payment.bic", header);
        Assert.Contains("payment.accountName", header);
        Assert.Contains("lines.sellerItemIdentifier", lines);
        Assert.Contains("lines.baseQuantity", lines);
        Assert.DoesNotContain("lines.itemName", lines);
    }

    [Fact]
    public void Every_object_requires_all_its_properties_and_allows_no_others()
    {
        foreach (var schema in new[] { InvoiceExtractionSchema.Header(), InvoiceExtractionSchema.Lines() })
        {
            AssertStrict(JsonSerializer.SerializeToElement(schema));
        }
    }

    private static void AssertStrict(JsonElement node)
    {
        if (node.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (node.TryGetProperty("properties", out var properties))
        {
            var declared = properties.EnumerateObject().Select(p => p.Name).Order().ToList();
            var required = node.GetProperty("required").EnumerateArray().Select(r => r.GetString()!).Order().ToList();
            Assert.Equal(declared, required);
            Assert.False(node.GetProperty("additionalProperties").GetBoolean());
        }

        foreach (var child in node.EnumerateObject())
        {
            AssertStrict(child.Value);
        }
    }

    private static List<string> Properties(IReadOnlyDictionary<string, JsonElement> schema)
        => schema["properties"].EnumerateObject().Select(p => p.Name).ToList();

    /// <summary>Every field as a dotted path, with $ref written out; array items add no segment.</summary>
    private static List<string> Paths(IReadOnlyDictionary<string, JsonElement> schema)
    {
        var root = JsonSerializer.SerializeToElement(schema);
        var paths = new List<string>();
        Walk(root, root, "", paths);
        return paths;
    }

    private static void Walk(JsonElement root, JsonElement node, string prefix, List<string> paths)
    {
        node = Resolve(root, node);
        if (node.TryGetProperty("properties", out var properties))
        {
            foreach (var property in properties.EnumerateObject())
            {
                var path = prefix.Length == 0 ? property.Name : $"{prefix}.{property.Name}";
                paths.Add(path);
                Walk(root, property.Value, path, paths);
            }
        }

        if (node.TryGetProperty("items", out var items))
        {
            Walk(root, items, prefix, paths);
        }
    }

    private static int Count(IReadOnlyDictionary<string, JsonElement> schema) => Paths(schema).Count;

    private static JsonElement Resolve(JsonElement root, JsonElement node)
        => node.TryGetProperty("$ref", out var reference)
            ? Resolve(root, root.GetProperty("$defs").GetProperty(reference.GetString()!["#/$defs/".Length..]))
            : node;
}
