using System.Text.Json;
using System.Text.Json.Serialization;

namespace FrontendNet.Helpers;

/// <summary>
/// Lee booleanos que el API envía como 0/1 (columnas BOOLEAN de MySQL consultadas con raw:true,
/// p. ej. "protegido" en /api/usuarios) además de true/false.
/// </summary>
public class BoolNumericoConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
    {
        JsonTokenType.True => true,
        JsonTokenType.False => false,
        JsonTokenType.Number => reader.GetInt32() != 0,
        JsonTokenType.String => reader.GetString() is "1" or "true" or "True",
        _ => throw new JsonException("Valor booleano no válido.")
    };

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) => writer.WriteBooleanValue(value);
}
