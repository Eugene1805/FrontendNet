using FrontendNet.Helpers;
using FrontendNet.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
namespace FrontendNet.Services;

public class AuthClientService(HttpClient client, IHttpContextAccessor httpContextAccessor)
{
    public async Task<AuthUser> GetTokenAsync(string email, string password)
    {
        Login usuario = new() { Email = email, Password = password };
        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync("api/auth", usuario);
        }
        catch (HttpRequestException ex) when (ex is not ApiException)
        {
            throw new ApiException(null, null, null, ex);
        }
        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw await ApiException.DesdeRespuestaAsync(response);
            var token = await response.Content.ReadFromJsonAsync<AuthUser>();
            return token!;
        }
    }

    public async Task SignInAsync(IEnumerable<Claim> claims)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No hay un HttpContext activo para iniciar sesión.");
        var items = claims.ToList();
        var claimsIdentity = new ClaimsIdentity(items, CookieAuthenticationDefaults.AuthenticationScheme);
        var props = new AuthenticationProperties();
        // DD-07: la cookie expira junto con el JWT para evitar sesiones "zombi" (D-14).
        var info = JwtInfo.Read(items.FirstOrDefault(c => c.Type == "jwt")?.Value);
        if (info?.Expira is { } exp)
            props.ExpiresUtc = exp;
        var principal = new ClaimsPrincipal(claimsIdentity);
        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, props);
        // La petición en curso también debe ver el JWT renovado.
        httpContext.User = principal;
    }

    /// <summary>
    /// Normaliza los claims de roles entre el backend (ADMIN, BUYER) y las vistas (Administrador, Usuario)
    /// garantizando consistencia en RBAC y contratos de autorización.
    /// </summary>
    public static List<Claim> NormalizaClaims(AuthUser token)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, token.Email),
            new(ClaimTypes.Name, token.Name),
            new("jwt", token.Jwt)
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
        else if (!string.IsNullOrEmpty(rawRole))
        {
            claims.Add(new Claim(ClaimTypes.Role, rawRole));
        }

        return claims;
    }
}
