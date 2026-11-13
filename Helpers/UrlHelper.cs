namespace FrontendNet.Helpers;

public static class Url
{
    /// <summary>Encode un segmento de ruta o valor de query (D-08).</summary>
    public static string Encode(string? valor) => Uri.EscapeDataString(valor ?? "");
}
