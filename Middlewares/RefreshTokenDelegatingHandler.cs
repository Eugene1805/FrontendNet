using System.Net;
using System.Net.Http.Headers;
using FrontendNet.Models;
using FrontendNet.Services;

namespace FrontendNet.Middlewares;

/// <summary>
/// Performs proactive and reactive single-flight session refresh via POST /api/auth/refresh,
/// retries a 401 request once after rotation, and converts non-2xx responses or transport errors into <see cref="ApiException"/>.
/// </summary>
public class RefreshTokenDelegatingHandler(
    AuthClientService authClient,
    ISessionTokenManager tokenManager,
    IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var isAuthenticated = httpContext?.User?.Identity?.IsAuthenticated == true;

        string? usedJwt = null;
        if (isAuthenticated && httpContext is not null)
        {
            usedJwt = await tokenManager.EnsureValidAccessTokenAsync(
                httpContext,
                authClient.TryRefreshAsync,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(usedJwt))
                throw ApiException.Create(HttpStatusCode.Unauthorized, "Sesión inválida o expirada.");

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", usedJwt);
        }

        byte[]? bufferedBody = null;
        HttpContentHeaders? bufferedHeaders = null;
        if (isAuthenticated && request.Content is not null)
        {
            bufferedBody = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            bufferedHeaders = request.Content.Headers;
            request.Content = CloneByteContent(bufferedBody, bufferedHeaders);
        }

        HttpResponseMessage response;
        try
        {
            response = await base.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex is not ApiException)
        {
            throw new ApiException(null, null, null, ex);
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized &&
            isAuthenticated &&
            httpContext is not null &&
            !string.IsNullOrWhiteSpace(usedJwt))
        {
            var refreshedJwt = await tokenManager.RefreshAfterUnauthorizedAsync(
                httpContext,
                usedJwt,
                authClient.TryRefreshAsync,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(refreshedJwt))
            {
                response.Dispose();
                using var retryRequest = CloneRequest(request, refreshedJwt, bufferedBody, bufferedHeaders);
                try
                {
                    response = await base.SendAsync(retryRequest, cancellationToken);
                }
                catch (HttpRequestException ex) when (ex is not ApiException)
                {
                    throw new ApiException(null, null, null, ex);
                }
            }
        }

        if (!response.IsSuccessStatusCode)
        {
            var ex = await ApiException.FromResponseAsync(response, cancellationToken);
            response.Dispose();
            throw ex;
        }

        return response;
    }

    private static HttpRequestMessage CloneRequest(
        HttpRequestMessage original,
        string newJwt,
        byte[]? bufferedBody,
        HttpContentHeaders? bufferedHeaders)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri)
        {
            Version = original.Version,
            VersionPolicy = original.VersionPolicy
        };

        foreach (var header in original.Headers)
        {
            if (!string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase))
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        clone.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newJwt);

        foreach (var option in original.Options)
            clone.Options.TryAdd(option.Key, option.Value);

        if (bufferedBody is not null)
            clone.Content = CloneByteContent(bufferedBody, bufferedHeaders);

        return clone;
    }

    private static ByteArrayContent CloneByteContent(byte[] bytes, HttpContentHeaders? sourceHeaders)
    {
        var content = new ByteArrayContent(bytes);
        if (sourceHeaders is not null)
        {
            foreach (var header in sourceHeaders)
                content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        return content;
    }
}
