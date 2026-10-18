using System.Globalization;
using System.Text.Json;
using InvoicePlatform.Contracts.Json;
using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Integration.Tests;

/// <summary>
/// Amounts cross the wire as strings (CLAUDE.md, hard rule 3). A JSON number
/// becomes a JavaScript float in the browser; a string stays exact.
/// </summary>
public class DecimalWireFormatTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new DecimalStringJsonConverter() },
    };

    [Fact]
    public void Decimals_are_written_as_strings_keeping_trailing_zeros()
    {
        var json = JsonSerializer.Serialize(new CanonicalInvoiceLine { UnitPrice = 10.50m, Quantity = null }, Options);

        Assert.Contains("\"unitPrice\":\"10.50\"", json, StringComparison.Ordinal);
        Assert.Contains("\"quantity\":null", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{\"unitPrice\":\"89.5000\"}", "89.5000")]
    [InlineData("{\"unitPrice\":89.50}", "89.50")]
    [InlineData("{\"unitPrice\":\"-0.01\"}", "-0.01")]
    public void Decimals_are_read_from_strings_or_numbers(string json, string expected)
    {
        var line = JsonSerializer.Deserialize<CanonicalInvoiceLine>(json, Options)!;

        Assert.Equal(expected, line.UnitPrice!.Value.ToString(CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("{\"unitPrice\":\"\"}")]
    [InlineData("{\"unitPrice\":\"1,5\"}")]
    [InlineData("{\"unitPrice\":\"1e3\"}")]
    public void Malformed_decimal_strings_are_rejected(string json)
        => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CanonicalInvoiceLine>(json, Options));
}
