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
        var response = await client.PostAsJsonAsync("api/auth", usuario);
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<AuthUser>();
        return token!;
    }

    public async Task IniciaSesionAsync(IEnumerable<Claim> claims)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No hay un HttpContext activo para iniciar sesión.");
        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity), new AuthenticationProperties());
    }
}
