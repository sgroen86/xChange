using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InvoicePlatform.Contracts.Json;

/// <summary>
/// Puts amounts on the wire as strings (CLAUDE.md, hard rule 3): a JSON number
/// becomes a JavaScript float in the browser, a string stays exact. Reads
/// either form, so a client that still sends numbers keeps working.
///
/// Registered once in the API; System.Text.Json applies a converter for a
/// value type to its nullable form as well, so decimal? is covered.
/// </summary>
public sealed class DecimalStringJsonConverter : JsonConverter<decimal>
{
    /// <summary>Plain "-12.50": no thousands separators, no exponent.</summary>
    private const NumberStyles Style = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetDecimal();
        }

        if (reader.TokenType == JsonTokenType.String
            && decimal.TryParse(reader.GetString(), Style, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        throw new JsonException("Expected a decimal number, as a string such as \"10.50\".");
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
}
