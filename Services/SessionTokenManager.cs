using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Claims;
using FrontendNet.Helpers;
using FrontendNet.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace FrontendNet.Services;

public sealed class SessionTokenManager : ISessionTokenManager
{
    public const string SessionIdClaimType = "sid";
    public const string JwtClaimType = "jwt";
    public const string RefreshTokenClaimType = "refresh_token";
    public const string AbsoluteExpirationClaimType = "session_abs_exp";
    public const string SellerStatusClaimType = "seller_status";

    private static readonly TimeSpan ProactiveRefreshWindow = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DefaultAbsoluteSessionDuration = TimeSpan.FromHours(8);

    private sealed class SessionState
    {
        public string SessionId { get; }
        public string Jwt { get; set; }
        public string RefreshToken { get; set; }
        public DateTimeOffset AccessExpiresUtc { get; set; }
        public DateTimeOffset AbsoluteExpiresUtc { get; }
        public SemaphoreSlim SyncLock { get; } = new(1, 1);

        public SessionState(
            string sessionId,
            string jwt,
            string refreshToken,
            DateTimeOffset accessExpiresUtc,
            DateTimeOffset absoluteExpiresUtc)
        {
            SessionId = sessionId;
            Jwt = jwt;
            RefreshToken = refreshToken;
            AccessExpiresUtc = accessExpiresUtc;
            AbsoluteExpiresUtc = absoluteExpiresUtc;
        }
    }

    private readonly ConcurrentDictionary<string, SessionState> _sessions = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;

    public SessionTokenManager() : this(TimeProvider.System)
    {
    }

    public SessionTokenManager(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public List<Claim> StoreSession(AuthUser authUser, DateTimeOffset? existingAbsoluteExpiresUtc = null)
    {
        var now = _timeProvider.GetUtcNow();
        var jwtInfo = JwtInfo.Read(authUser.Jwt);
        var sessionId = !string.IsNullOrWhiteSpace(jwtInfo?.SessionId)
            ? jwtInfo.SessionId
            : Guid.NewGuid().ToString("N");
        var accessExpiresUtc = jwtInfo?.ExpiresUtc
            ?? now.AddSeconds(authUser.ExpiresIn > 0 ? authUser.ExpiresIn : 300);
        var absoluteExpiresUtc = existingAbsoluteExpiresUtc ?? now.Add(DefaultAbsoluteSessionDuration);
        var refreshToken = authUser.RefreshToken ?? string.Empty;

        _sessions.AddOrUpdate(
            sessionId,
            _ => new SessionState(sessionId, authUser.Jwt, refreshToken, accessExpiresUtc, absoluteExpiresUtc),
            (_, existing) =>
            {
                existing.Jwt = authUser.Jwt;
                existing.RefreshToken = refreshToken;
                existing.AccessExpiresUtc = accessExpiresUtc;
                return existing;
            });

        return BuildClaims(authUser, sessionId, absoluteExpiresUtc);
    }

    public string? GetCurrentAccessToken(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true) return null;
        var state = GetOrHydrateState(user);
        return state?.Jwt ?? user.FindFirstValue(JwtClaimType);
    }

    public async Task<string?> EnsureValidAccessTokenAsync(
        HttpContext httpContext,
        Func<string, CancellationToken, Task<AuthUser?>> refreshFunc,
        CancellationToken ct = default)
    {
        var user = httpContext.User;
        if (user?.Identity?.IsAuthenticated != true) return null;

        var state = GetOrHydrateState(user);
        if (state is null)
            return user.FindFirstValue(JwtClaimType);

        var now = _timeProvider.GetUtcNow();
        if (now >= state.AbsoluteExpiresUtc)
        {
            await ClearExpiredSessionAsync(httpContext, state.SessionId);
            return null;
        }

        if (state.AccessExpiresUtc > now.Add(ProactiveRefreshWindow))
            return state.Jwt;

        if (string.IsNullOrWhiteSpace(state.RefreshToken))
            return state.Jwt;

        await state.SyncLock.WaitAsync(ct);
        try
        {
            now = _timeProvider.GetUtcNow();
            if (now >= state.AbsoluteExpiresUtc)
            {
                await ClearExpiredSessionAsync(httpContext, state.SessionId);
                return null;
            }

            if (state.AccessExpiresUtc > now.Add(ProactiveRefreshWindow))
                return state.Jwt;

            return await ExecuteRefreshLockedAsync(httpContext, state, refreshFunc, ct);
        }
        finally
        {
            state.SyncLock.Release();
        }
    }

    public async Task<string?> RefreshAfterUnauthorizedAsync(
        HttpContext httpContext,
        string failedJwt,
        Func<string, CancellationToken, Task<AuthUser?>> refreshFunc,
        CancellationToken ct = default)
    {
        var user = httpContext.User;
        if (user?.Identity?.IsAuthenticated != true) return null;

        var state = GetOrHydrateState(user);
        if (state is null || string.IsNullOrWhiteSpace(state.RefreshToken))
        {
            await ClearExpiredSessionAsync(httpContext, state?.SessionId);
            return null;
        }

        await state.SyncLock.WaitAsync(ct);
        try
        {
            var now = _timeProvider.GetUtcNow();
            if (now >= state.AbsoluteExpiresUtc)
            {
                await ClearExpiredSessionAsync(httpContext, state.SessionId);
                return null;
            }

            // Single-flight check: another concurrent call already rotated the token.
            if (!string.Equals(state.Jwt, failedJwt, StringComparison.Ordinal))
            {
                SyncUserClaims(httpContext, state);
                return state.Jwt;
            }

            return await ExecuteRefreshLockedAsync(httpContext, state, refreshFunc, ct);
        }
        finally
        {
            state.SyncLock.Release();
        }
    }

    public async Task UpdateUserNameAsync(HttpContext httpContext, string newName)
    {
        if (httpContext.User?.Identity?.IsAuthenticated != true) return;

        var claims = httpContext.User.Claims.Where(c => c.Type != ClaimTypes.Name).ToList();
        claims.Add(new Claim(ClaimTypes.Name, newName));
        await PersistClaimsAsync(httpContext, claims);
    }

    public async Task UpdateSellerStatusAsync(HttpContext httpContext, string? sellerStatus)
    {
        if (httpContext.User?.Identity?.IsAuthenticated != true) return;

        var claims = httpContext.User.Claims.Where(c => c.Type != SellerStatusClaimType).ToList();
        if (!string.IsNullOrWhiteSpace(sellerStatus))
            claims.Add(new Claim(SellerStatusClaimType, sellerStatus));
        await PersistClaimsAsync(httpContext, claims);
    }

    public void RemoveSession(ClaimsPrincipal? user)
    {
        var sessionId = user?.FindFirstValue(SessionIdClaimType);
        RemoveSession(sessionId);
    }

    public void RemoveSession(string? sessionId)
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
            _sessions.TryRemove(sessionId, out _);
    }

    private async Task<string?> ExecuteRefreshLockedAsync(
        HttpContext httpContext,
        SessionState state,
        Func<string, CancellationToken, Task<AuthUser?>> refreshFunc,
        CancellationToken ct)
    {
        var refreshed = await refreshFunc(state.RefreshToken, ct);
        if (refreshed is null || string.IsNullOrWhiteSpace(refreshed.Jwt))
        {
            await ClearExpiredSessionAsync(httpContext, state.SessionId);
            return null;
        }

        var now = _timeProvider.GetUtcNow();
        var jwtInfo = JwtInfo.Read(refreshed.Jwt);
        state.Jwt = refreshed.Jwt;
        state.RefreshToken = refreshed.RefreshToken ?? string.Empty;
        state.AccessExpiresUtc = jwtInfo?.ExpiresUtc
            ?? now.AddSeconds(refreshed.ExpiresIn > 0 ? refreshed.ExpiresIn : 300);

        var updatedClaims = BuildClaims(refreshed, state.SessionId, state.AbsoluteExpiresUtc);
        await PersistClaimsAsync(httpContext, updatedClaims);
        return state.Jwt;
    }

    private SessionState? GetOrHydrateState(ClaimsPrincipal user)
    {
        var sessionId = user.FindFirstValue(SessionIdClaimType);
        if (string.IsNullOrWhiteSpace(sessionId))
            return null;

        if (_sessions.TryGetValue(sessionId, out var existing))
            return existing;

        var jwt = user.FindFirstValue(JwtClaimType);
        if (string.IsNullOrWhiteSpace(jwt))
            return null;

        var refreshToken = user.FindFirstValue(RefreshTokenClaimType) ?? string.Empty;
        var now = _timeProvider.GetUtcNow();
        var jwtInfo = JwtInfo.Read(jwt);
        var accessExpiresUtc = jwtInfo?.ExpiresUtc ?? now.AddMinutes(5);
        var absExpStr = user.FindFirstValue(AbsoluteExpirationClaimType);
        var absoluteExpiresUtc = long.TryParse(absExpStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var absSeconds)
            ? DateTimeOffset.FromUnixTimeSeconds(absSeconds)
            : now.Add(DefaultAbsoluteSessionDuration);

        return _sessions.GetOrAdd(
            sessionId,
            _ => new SessionState(sessionId, jwt, refreshToken, accessExpiresUtc, absoluteExpiresUtc));
    }

    private async Task ClearExpiredSessionAsync(HttpContext httpContext, string? sessionId)
    {
        RemoveSession(sessionId);
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        if (!httpContext.Response.HasStarted)
            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    private static void SyncUserClaims(HttpContext httpContext, SessionState state)
    {
        var claims = httpContext.User.Claims
            .Where(c => c.Type != JwtClaimType && c.Type != RefreshTokenClaimType)
            .ToList();
        claims.Add(new Claim(JwtClaimType, state.Jwt));
        claims.Add(new Claim(RefreshTokenClaimType, state.RefreshToken));
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    private static async Task PersistClaimsAsync(HttpContext httpContext, List<Claim> claims)
    {
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        httpContext.User = principal;

        if (!httpContext.Response.HasStarted)
        {
            await httpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties { IsPersistent = false });
        }
    }

    public static List<Claim> BuildClaims(AuthUser authUser, string sessionId, DateTimeOffset absoluteExpiresUtc)
    {
        var normalizedProfile = (authUser.Profile ?? string.Empty).Trim().ToUpperInvariant();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, authUser.Id ?? string.Empty),
            new(ClaimTypes.Email, authUser.Email),
            new(ClaimTypes.Name, authUser.Name),
            new(ClaimTypes.Role, normalizedProfile),
            new(SessionIdClaimType, sessionId),
            new(JwtClaimType, authUser.Jwt),
            new(RefreshTokenClaimType, authUser.RefreshToken ?? string.Empty),
            new(AbsoluteExpirationClaimType, absoluteExpiresUtc.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture))
        };

        if (!string.IsNullOrWhiteSpace(authUser.SellerStatus))
            claims.Add(new Claim(SellerStatusClaimType, authUser.SellerStatus.Trim().ToUpperInvariant()));

        return claims;
    }
}
