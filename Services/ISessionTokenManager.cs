using System.Security.Claims;
using FrontendNet.Models;

namespace FrontendNet.Services;

/// <summary>
/// Manages RS256 access tokens and rotating refresh tokens per session with single-flight concurrency control.
/// </summary>
public interface ISessionTokenManager
{
    List<Claim> StoreSession(AuthUser authUser, DateTimeOffset? existingAbsoluteExpiresUtc = null);

    string? GetCurrentAccessToken(ClaimsPrincipal? user);

    Task<string?> EnsureValidAccessTokenAsync(
        HttpContext httpContext,
        Func<string, CancellationToken, Task<AuthUser?>> refreshFunc,
        CancellationToken ct = default);

    Task<string?> RefreshAfterUnauthorizedAsync(
        HttpContext httpContext,
        string failedJwt,
        Func<string, CancellationToken, Task<AuthUser?>> refreshFunc,
        CancellationToken ct = default);

    Task UpdateUserNameAsync(HttpContext httpContext, string newName);

    void RemoveSession(ClaimsPrincipal? user);

    void RemoveSession(string? sessionId);
}
