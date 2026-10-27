using FrontendNet.Helpers;
using FrontendNet.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
namespace FrontendNet.Services;

public class AuthClientService(HttpClient client, IHttpContextAccessor httpContextAccessor)
{
    public async Task<AuthUser> ObtenTokenAsync(string email, string password)
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

    public async Task IniciaSesionAsync(IEnumerable<Claim> claims)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No hay un HttpContext activo para iniciar sesión.");
        var lista = claims.ToList();
        var claimsIdentity = new ClaimsIdentity(lista, CookieAuthenticationDefaults.AuthenticationScheme);
        var props = new AuthenticationProperties();
        // DD-07: la cookie expira junto con el JWT para evitar sesiones "zombi" (D-14).
        var info = JwtInfo.Lee(lista.FirstOrDefault(c => c.Type == "jwt")?.Value);
        if (info?.Expira is { } exp)
            props.ExpiresUtc = exp;
        var principal = new ClaimsPrincipal(claimsIdentity);
        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, props);
        // La petición en curso también debe ver el JWT renovado.
        httpContext.User = principal;
    }
}
