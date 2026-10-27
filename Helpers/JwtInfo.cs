using System.Text;
using System.Text.Json;

namespace FrontendNet.Helpers;

/// <summary>
/// Lee claims informativos (exp, iss, aud) de un JWT SIN validar la firma.
/// Solo sirve para mostrar/alinear tiempos; la autoridad es el API.
/// </summary>
public record JwtInfo(DateTimeOffset? Expira, string? Emisor, string? Audiencia)
{
    public static JwtInfo? Lee(string? jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt)) return null;
        var partes = jwt.Split('.');
        if (partes.Length < 2) return null;
        try
        {
            var b64 = partes[1].Replace('-', '+').Replace('_', '/');
            b64 = b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '=');
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(b64)));
            var raiz = doc.RootElement;
            DateTimeOffset? exp = raiz.TryGetProperty("exp", out var e) && e.TryGetInt64(out var segs)
                ? DateTimeOffset.FromUnixTimeSeconds(segs) : null;
            string? Texto(string nombre) => raiz.TryGetProperty(nombre, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            return new JwtInfo(exp, Texto("iss"), Texto("aud"));
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }
    }
}
