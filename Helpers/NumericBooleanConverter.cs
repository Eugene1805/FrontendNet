using System.Text.Json;
using System.Text.Json.Serialization;

namespace FrontendNet.Helpers;

/// <summary>
/// Deserializes booleans represented as JSON booleans, integers (0/1), or strings ("1"/"true").
/// </summary>
public class NumericBooleanConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
    {
        JsonTokenType.True => true,
        JsonTokenType.False => false,
        JsonTokenType.Number => reader.GetInt32() != 0,
        JsonTokenType.String => reader.GetString() is "1" or "true" or "True",
        _ => throw new JsonException("Invalid boolean value.")
    };

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) =>
        writer.WriteBooleanValue(value);
}
