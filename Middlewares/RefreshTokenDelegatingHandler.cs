using System.Net;
using System.Net.Http.Headers;
using FrontendNet.Models;
using FrontendNet.Services;

namespace FrontendNet.Middlewares;

/// <summary>
/// Manages RS256 access token renewal against POST /api/auth/refresh:
/// - Proactive refresh before sending when access token has &lt; 60s remaining.
/// - Reactive refresh on 401 Unauthorized, retrying the cloned request at most ONCE.
/// - Uses per-session single-flight locking because backend refresh tokens are single-use.
/// - On refresh failure or repeated 401, signs out locally (clearing .frontendnet cookie) and throws ApiException(401).
/// </summary>
public class RefreshTokenDelegatingHandler(
    ISessionTokenManager sessionTokens,
    SessionRefreshCoordinator coordinator,
    TimeProvider? timeProvider = null) : DelegatingHandler
{
    public static readonly TimeSpan ProactiveRefreshThreshold = TimeSpan.FromSeconds(60);
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var tokens = await sessionTokens.GetSessionTokensAsync(cancellationToken);
        var refreshedProactively = false;

        // 1) Proactive refresh when < 60s remain on the access token.
        if (tokens != null && NeedsProactiveRefresh(tokens) && !string.IsNullOrWhiteSpace(tokens.RefreshToken))
        {
            var refreshed = await TryRefreshSessionAsync(tokens, cancellationToken);
            if (refreshed == null)
            {
                await sessionTokens.SignOutLocalAsync(cancellationToken);
                throw ApiException.Create(HttpStatusCode.Unauthorized, "Sesión inválida o expirada.");
            }
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed.Jwt);
            tokens = await sessionTokens.GetSessionTokensAsync(cancellationToken) ?? tokens;
            refreshedProactively = true;
        }

        // Buffer content once so both the initial send and an optional 401 retry can read it cleanly.
        byte[]? bufferedBody = null;
        if (request.Content != null)
        {
            bufferedBody = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            var originalContent = request.Content;
            var replacement = new ByteArrayContent(bufferedBody);
            foreach (var header in originalContent.Headers)
            {
                replacement.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            request.Content = replacement;
        }

        var response = await SendCoreAsync(request, cancellationToken);

        // 2) Reactive refresh on 401 Unauthorized (retry at most ONCE).
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var currentTokens = await sessionTokens.GetSessionTokensAsync(cancellationToken) ?? tokens;
            if (!refreshedProactively && currentTokens != null && !string.IsNullOrWhiteSpace(currentTokens.RefreshToken))
            {
                var refreshed = await TryRefreshSessionAsync(currentTokens, cancellationToken);
                if (refreshed != null)
                {
                    response.Dispose();
                    using var retryRequest = CloneRequest(request, bufferedBody, refreshed.Jwt);
                    response = await SendCoreAsync(retryRequest, cancellationToken);
                }
            }

            // Still 401 (refresh failed, no refresh token, or retry also returned 401) -> sign out locally.
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                await sessionTokens.SignOutLocalAsync(cancellationToken);
            }
        }

        if (!response.IsSuccessStatusCode)
        {
            var ex = await ApiException.DesdeRespuestaAsync(response, cancellationToken);
            response.Dispose();
            throw ex;
        }

        return response;
    }

    private bool NeedsProactiveRefresh(SessionTokens tokens)
    {
        if (string.IsNullOrWhiteSpace(tokens.AccessToken)) return true;
        if (tokens.ExpiresAtUtc is not { } exp) return false;
        return exp - _timeProvider.GetUtcNow() < ProactiveRefreshThreshold;
    }

    private async Task<AuthUser?> TryRefreshSessionAsync(SessionTokens tokens, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tokens.RefreshToken)) return null;

        var sessionKey = SessionRefreshCoordinator.BuildSessionKey(tokens.SessionId, tokens.RefreshToken);
        try
        {
            var refreshed = await coordinator.RefreshSingleFlightAsync(
                sessionKey,
                tokens.RefreshToken,
                async ct =>
                {
                    var next = await sessionTokens.RefreshTokenAsync(tokens.RefreshToken, ct);
                    if (string.IsNullOrWhiteSpace(next.Jwt)) return null;
                    await sessionTokens.UpdateSessionTokensAsync(next, ct);
                    return next;
                },
                cancellationToken);

            // Waiters that reused a concurrent refresh result must also ensure their request context
            // has the updated tokens applied (cookie/HttpContext.Items).
            if (refreshed != null)
            {
                var latest = await sessionTokens.GetSessionTokensAsync(cancellationToken);
                if (latest?.AccessToken != refreshed.Jwt)
                {
                    await sessionTokens.UpdateSessionTokensAsync(refreshed, cancellationToken);
                }
            }
            return refreshed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await base.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex is not ApiException)
        {
            throw new ApiException(null, null, null, ex);
        }
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage original, byte[]? bufferedBody, string newJwt)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri)
        {
            Version = original.Version,
            VersionPolicy = original.VersionPolicy
        };

        foreach (var header in original.Headers)
        {
            if (string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase)) continue;
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        clone.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newJwt);

        if (bufferedBody != null && original.Content != null)
        {
            var content = new ByteArrayContent(bufferedBody);
            foreach (var header in original.Content.Headers)
            {
                content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            clone.Content = content;
        }

        foreach (var option in original.Options)
        {
            clone.Options.TryAdd(option.Key, option.Value);
        }

        return clone;
    }
}
