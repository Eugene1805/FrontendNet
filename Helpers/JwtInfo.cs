using System.Text;
using System.Text.Json;

namespace FrontendNet.Helpers;

/// <summary>
/// Reads informational claims (exp, iss, aud, sid, sub) from a JWT without verifying its signature.
/// Used solely for display and session timing; the backend API remains authoritative.
/// </summary>
public record JwtInfo(
    DateTimeOffset? ExpiresUtc,
    string? Issuer,
    string? Audience,
    string? SessionId,
    string? Subject)
{
    public static JwtInfo? Read(string? jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt)) return null;
        var parts = jwt.Split('.');
        if (parts.Length < 2) return null;

        try
        {
            var base64 = parts[1].Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(base64)));
            var root = doc.RootElement;

            DateTimeOffset? expiresUtc = root.TryGetProperty("exp", out var expProp) && expProp.TryGetInt64(out var seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                : null;

            string? ReadString(string name) =>
                root.TryGetProperty(name, out var val) && val.ValueKind == JsonValueKind.String
                    ? val.GetString()
                    : null;

            return new JwtInfo(
                expiresUtc,
                ReadString("iss"),
                ReadString("aud"),
                ReadString("sid"),
                ReadString("sub"));
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }
    }
}
