using System.Globalization;
using System.Security.Claims;
using FrontendNet.Helpers;
using FrontendNet.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace FrontendNet.Services;

public class AuthClientService(
    HttpClient client,
    IHttpContextAccessor httpContextAccessor,
    SessionRefreshCoordinator? refreshCoordinator = null,
    TimeProvider? timeProvider = null) : ISessionTokenManager
{
    public const string AccessTokenName = "access_token";
    public const string RefreshTokenName = "refresh_token";
    public const string ExpiresAtTokenName = "expires_at";
    public const string SessionCreatedItemKey = "session_created_utc";
    private const string HttpContextTokensItemKey = "__AuthSessionTokens";

    public static readonly TimeSpan IdleSessionLifetime = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan AbsoluteSessionLifetime = TimeSpan.FromHours(8);

    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<AuthUser> GetTokenAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        Login usuario = new() { Email = email, Password = password };
        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync("api/auth", usuario, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex is not ApiException)
        {
            throw new ApiException(null, null, null, ex);
        }
        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw await ApiException.DesdeRespuestaAsync(response, cancellationToken);
            var token = await response.Content.ReadFromJsonAsync<AuthUser>(cancellationToken);
            return token!;
        }
    }

    public async Task<AuthUser> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync("api/auth/refresh", new { refreshToken }, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex is not ApiException)
        {
            throw new ApiException(null, null, null, ex);
        }
        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw await ApiException.DesdeRespuestaAsync(response, cancellationToken);
            var token = await response.Content.ReadFromJsonAsync<AuthUser>(cancellationToken);
            return token ?? throw ApiException.Create(System.Net.HttpStatusCode.Unauthorized, "Sesión inválida o expirada.");
        }
    }

    public async Task SignInAsync(AuthUser token, CancellationToken cancellationToken = default)
    {
        var claims = NormalizaClaims(token);
        await SignInInternalAsync(claims, token, existingSessionCreatedUtc: null);
    }

    public async Task UpdateSessionTokensAsync(AuthUser refreshed, CancellationToken cancellationToken = default)
    {
        var existing = await GetSessionTokensAsync(cancellationToken);
        var claims = NormalizaClaims(refreshed);
        await SignInInternalAsync(claims, refreshed, existing?.SessionCreatedUtc);
    }

    /// <summary>
    /// Updates user claims (e.g., profile name edit) while preserving the latest session tokens
    /// (including any token rotated earlier in the same HTTP request) and absolute deadline.
    /// </summary>
    public async Task SignInAsync(IEnumerable<Claim> claims)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No hay un HttpContext activo para iniciar sesión.");
        var snapshot = await GetSessionTokensAsync();
        var now = _timeProvider.GetUtcNow();
        var createdUtc = snapshot?.SessionCreatedUtc ?? now;

        var props = new AuthenticationProperties
        {
            IssuedUtc = now,
            ExpiresUtc = ComputeCookieExpiry(now, createdUtc)
        };
        props.Items[SessionCreatedItemKey] = createdUtc.ToString("O", CultureInfo.InvariantCulture);

        var authTokens = new List<AuthenticationToken>();
        if (!string.IsNullOrWhiteSpace(snapshot?.AccessToken))
        {
            authTokens.Add(new AuthenticationToken { Name = AccessTokenName, Value = snapshot.AccessToken });
        }
        if (!string.IsNullOrWhiteSpace(snapshot?.RefreshToken))
        {
            authTokens.Add(new AuthenticationToken { Name = RefreshTokenName, Value = snapshot.RefreshToken });
        }
        if (snapshot?.ExpiresAtUtc is { } exp)
        {
            authTokens.Add(new AuthenticationToken { Name = ExpiresAtTokenName, Value = exp.ToString("O", CultureInfo.InvariantCulture) });
        }
        if (authTokens.Count > 0)
        {
            props.StoreTokens(authTokens);
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims.ToList(), CookieAuthenticationDefaults.AuthenticationScheme));
        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, props);
        httpContext.User = principal;
        if (snapshot != null)
        {
            httpContext.Items[HttpContextTokensItemKey] = snapshot with { SessionCreatedUtc = createdUtc };
        }
    }

    public async Task<SessionTokens?> GetSessionTokensAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext == null) return null;

        if (httpContext.Items.TryGetValue(HttpContextTokensItemKey, out var cached) && cached is SessionTokens snapshot)
            return snapshot;

        var authResult = await httpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!authResult.Succeeded || authResult.Properties == null)
            return null;

        var props = authResult.Properties;
        var accessToken = props.GetTokenValue(AccessTokenName);
        var refreshToken = props.GetTokenValue(RefreshTokenName);
        var expiresAtRaw = props.GetTokenValue(ExpiresAtTokenName);
        DateTimeOffset? expiresAt = DateTimeOffset.TryParse(expiresAtRaw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedExp)
            ? parsedExp
            : JwtInfo.Read(accessToken)?.Expira;
        var sessionId = JwtInfo.Read(accessToken)?.SessionId;
        var createdUtc = ParseCreatedUtc(props);

        var tokens = new SessionTokens(accessToken, refreshToken, expiresAt, sessionId, createdUtc);
        httpContext.Items[HttpContextTokensItemKey] = tokens;
        return tokens;
    }

    public async Task SignOutLocalAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext == null) return;

        var current = await GetSessionTokensAsync(cancellationToken);
        if (refreshCoordinator != null && !string.IsNullOrWhiteSpace(current?.RefreshToken))
        {
            refreshCoordinator.Clear(SessionRefreshCoordinator.BuildSessionKey(current.SessionId, current.RefreshToken));
        }

        httpContext.Items.Remove(HttpContextTokensItemKey);
        if (httpContext.Features.Get<Microsoft.AspNetCore.Http.Features.ISessionFeature>() != null)
        {
            httpContext.Session.Clear();
        }
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
    }

    private async Task SignInInternalAsync(List<Claim> claims, AuthUser token, DateTimeOffset? existingSessionCreatedUtc)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No hay un HttpContext activo para iniciar sesión.");

        var now = _timeProvider.GetUtcNow();
        var sessionCreatedUtc = existingSessionCreatedUtc ?? now;
        var jwtInfo = JwtInfo.Read(token.Jwt);
        var accessExpiresUtc = jwtInfo?.Expira ?? now.AddSeconds(token.ExpiresIn > 0 ? token.ExpiresIn : 300);

        var props = new AuthenticationProperties
        {
            IssuedUtc = now,
            ExpiresUtc = ComputeCookieExpiry(now, sessionCreatedUtc)
        };
        props.Items[SessionCreatedItemKey] = sessionCreatedUtc.ToString("O", CultureInfo.InvariantCulture);

        var authTokens = new List<AuthenticationToken>
        {
            new() { Name = AccessTokenName, Value = token.Jwt },
            new() { Name = ExpiresAtTokenName, Value = accessExpiresUtc.ToString("O", CultureInfo.InvariantCulture) }
        };
        if (!string.IsNullOrWhiteSpace(token.RefreshToken))
        {
            authTokens.Add(new AuthenticationToken { Name = RefreshTokenName, Value = token.RefreshToken });
        }
        props.StoreTokens(authTokens);

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, props);
        httpContext.User = principal;
        httpContext.Items[HttpContextTokensItemKey] = new SessionTokens(
            token.Jwt, token.RefreshToken, accessExpiresUtc, jwtInfo?.SessionId, sessionCreatedUtc);
    }

    public static DateTimeOffset ComputeCookieExpiry(DateTimeOffset now, DateTimeOffset sessionCreatedUtc)
    {
        var idleDeadline = now + IdleSessionLifetime;
        var absoluteDeadline = sessionCreatedUtc + AbsoluteSessionLifetime;
        return idleDeadline <= absoluteDeadline ? idleDeadline : absoluteDeadline;
    }

    public static DateTimeOffset? ParseCreatedUtc(AuthenticationProperties? props)
    {
        if (props?.Items.TryGetValue(SessionCreatedItemKey, out var raw) == true &&
            DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            return parsed;
        }
        return null;
    }

    /// <summary>
    /// Normaliza los claims de roles entre el backend (ADMIN, BUYER, SELLER) y las vistas (Administrador, Usuario, Vendedor)
    /// garantizando consistencia en RBAC y contratos de autorización. Los tokens se almacenan cifrados en AuthenticationProperties.
    /// </summary>
    public static List<Claim> NormalizaClaims(AuthUser token)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, token.Email),
            new(ClaimTypes.Name, token.Name)
        };

        var rawRole = (token.Profile ?? token.Role ?? "").Trim();
        if (rawRole.Equals("ADMIN", StringComparison.OrdinalIgnoreCase) ||
            rawRole.Equals("Administrador", StringComparison.OrdinalIgnoreCase))
        {
            claims.Add(new Claim(ClaimTypes.Role, "ADMIN"));
            claims.Add(new Claim(ClaimTypes.Role, "Administrador"));
        }
        else if (rawRole.Equals("BUYER", StringComparison.OrdinalIgnoreCase) ||
                 rawRole.Equals("Usuario", StringComparison.OrdinalIgnoreCase))
        {
            claims.Add(new Claim(ClaimTypes.Role, "BUYER"));
            claims.Add(new Claim(ClaimTypes.Role, "Usuario"));
        }
        else if (rawRole.Equals("SELLER", StringComparison.OrdinalIgnoreCase) ||
                 rawRole.Equals("Vendedor", StringComparison.OrdinalIgnoreCase))
        {
            claims.Add(new Claim(ClaimTypes.Role, "SELLER"));
            claims.Add(new Claim(ClaimTypes.Role, "Vendedor"));
        }
        // Perfiles no reconocidos se ignoran explícitamente para no emitir claims de rol arbitrarios.

        return claims;
    }
}
