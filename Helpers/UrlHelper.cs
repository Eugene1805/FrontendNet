namespace FrontendNet.Helpers;

/// <summary>Encodes route segments and query string values safely.</summary>
public static class UrlHelper
{
    public static string Encode(string? value) => Uri.EscapeDataString(value ?? string.Empty);
}
