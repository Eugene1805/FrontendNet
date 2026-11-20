using System.Text.Json.Serialization;

namespace FrontendNet.Services;

public class CloudflareTurnstileService(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<CloudflareTurnstileService> logger)
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
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            logger.LogDebug("Cloudflare Turnstile skipped: 'Cloudflare:TurnstileSecretKey' is not configured.");
            return true;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            logger.LogWarning("Cloudflare Turnstile token is missing or empty.");
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
                parameters["remoteip"] = remoteIp;

            using var content = new FormUrlEncodedContent(parameters);
            using var response = await httpClient.PostAsync(
                "https://challenges.cloudflare.com/turnstile/v0/siteverify",
                content);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to reach Cloudflare Turnstile: HTTP {StatusCode}", response.StatusCode);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<SiteVerifyResponse>();
            if (result?.Success == true)
                return true;

            logger.LogWarning("Cloudflare Turnstile verification failed. Errors: {Errors}",
                string.Join(", ", result?.ErrorCodes ?? []));
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception while verifying Cloudflare Turnstile token.");
            return false;
        }
    }
}
