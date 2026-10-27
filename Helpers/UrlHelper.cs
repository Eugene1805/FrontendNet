namespace FrontendNet.Helpers;

public static class Url
{
    /// <summary>Codifica un segmento de ruta o valor de query (D-08).</summary>
    public static string Codifica(string? valor) => Uri.EscapeDataString(valor ?? "");
}
