using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace FrontendNet.Services;

public class CloudflareTurnstileService(HttpClient httpClient, IConfiguration configuration, ILogger<CloudflareTurnstileService> logger)
{
    private sealed class SiteVerifyResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("error-codes")]
        public List<string> ErrorCodes { get; set; } = [];
    }

    public async Task<bool> VerifyTokenAsync(string? token, string? remoteIp = null)
    {
        var secretKey = configuration["Cloudflare:TurnstileSecretKey"];
        // Si no se ha configurado secret key (p. ej. ambiente local/desarrollo), se omite con aviso
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            logger.LogDebug("Cloudflare Turnstile omitido: 'Cloudflare:TurnstileSecretKey' no está configurado.");
            return true;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            logger.LogWarning("Cloudflare Turnstile token ausente o vacío.");
            return false;
        }

        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["secret"] = secretKey,
                ["response"] = token
            };

            if (!string.IsNullOrWhiteSpace(remoteIp))
            {
                parameters["remoteip"] = remoteIp;
            }

            using var content = new FormUrlEncodedContent(parameters);
            using var response = await httpClient.PostAsync("https://challenges.cloudflare.com/turnstile/v0/siteverify", content);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Error al contactar Cloudflare Turnstile: HTTP {StatusCode}", response.StatusCode);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<SiteVerifyResponse>();
            if (result?.Success == true)
            {
                return true;
            }

            logger.LogWarning("Cloudflare Turnstile falló. Errores: {Errors}", string.Join(", ", result?.ErrorCodes ?? []));
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Excepción no controlada durante validación de Cloudflare Turnstile.");
            return false;
        }
    }
}
