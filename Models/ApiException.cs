using System.Net;
using System.Net.Http.Json;

namespace FrontendNet.Models;

/// <summary>
/// Error returned by the API (or a network failure). Preserves the { message, errors[] } body
/// y es una HttpRequestException, por lo que sigue exponiendo StatusCode.
/// </summary>
public class ApiException : HttpRequestException
{
    public ApiError? Error { get; }
    public TimeSpan? RetryAfter { get; }
    public override string Message => string.IsNullOrWhiteSpace(Error?.Message)
        ? "No ha sido posible realizar la acción. Inténtelo nuevamente."
        : Error!.Message!;

    public ApiException(HttpStatusCode? status, ApiError? error, TimeSpan? retryAfter = null, Exception? inner = null)
        : base(error?.Message ?? (status.HasValue ? $"El API respondió {(int)status.Value}." : "No fue posible comunicarse con el API."), inner, status)
    {
        Error = error;
        RetryAfter = retryAfter;
    }

    public static ApiException Create(HttpStatusCode status, string message) =>
        new(status, new ApiError { Status = (int)status, Message = message });

    /// <summary>Construye la excepción leyendo el cuerpo de una respuesta no exitosa.</summary>
    public static async Task<ApiException> DesdeRespuestaAsync(HttpResponseMessage response, CancellationToken ct = default)
    {
        ApiError? error = null;
        try
        {
            if (response.Content.Headers.ContentType?.MediaType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
                error = await response.Content.ReadFromJsonAsync<ApiError>(ct);
        }
        catch (Exception)
        {
            // Cuerpo no legible: se usa el mensaje genérico según el código de estado.
        }
        var retry = response.Headers.RetryAfter?.Delta
            ?? (response.Headers.RetryAfter?.Date is { } fecha ? fecha - DateTimeOffset.UtcNow : null);
        return new ApiException(response.StatusCode, error, retry);
    }
}
