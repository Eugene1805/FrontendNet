using System.Net;
using System.Net.Http.Json;

namespace FrontendNet.Models;

/// <summary>
/// Exception representing a non-success response or transport failure from the backend API.
/// Preserves the HTTP status code, parsed <see cref="ApiError"/> payload, and Retry-After duration.
/// </summary>
public class ApiException : HttpRequestException
{
    public ApiError? Error { get; }
    public TimeSpan? RetryAfter { get; }

    public override string Message => string.IsNullOrWhiteSpace(Error?.Message)
        ? "No ha sido posible realizar la acción. Inténtelo nuevamente."
        : Error!.Message!;

    public ApiException(HttpStatusCode? status, ApiError? error, TimeSpan? retryAfter = null, Exception? inner = null)
        : base(
            error?.Message ?? (status.HasValue
                ? $"The API responded with status {(int)status.Value}."
                : "Unable to reach the API."),
            inner,
            status)
    {
        Error = error;
        RetryAfter = retryAfter;
    }

    public static ApiException Create(HttpStatusCode status, string message) =>
        new(status, new ApiError { Status = (int)status, Message = message });

    /// <summary>Builds an <see cref="ApiException"/> from a non-2xx HTTP response.</summary>
    public static async Task<ApiException> FromResponseAsync(HttpResponseMessage response, CancellationToken ct = default)
    {
        ApiError? error = null;
        try
        {
            if (response.Content.Headers.ContentType?.MediaType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
                error = await response.Content.ReadFromJsonAsync<ApiError>(ct);
        }
        catch (Exception)
        {
            // Ignore unparseable error payloads and fall back to status-based messaging.
        }

        var retryAfter = response.Headers.RetryAfter?.Delta
            ?? (response.Headers.RetryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow : null);

        return new ApiException(response.StatusCode, error, retryAfter);
    }
}
