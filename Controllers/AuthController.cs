using System.Security.Claims;
using FrontendNet.Models;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrontendNet;
public class AuthController(AuthClientService auth, ProfileClientService profile, IConfiguration configuration) : AppController
{
    [AllowAnonymous]
    public IActionResult Index()
    {
        return View("Index");
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> IndexAsync(Login model)
    {
        if (ModelState.IsValid)
        {
            try
            {
                AuthUser token = await auth.GetTokenAsync(model.Email, model.Password);
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Email, token.Email),
                    new Claim(ClaimTypes.Name, token.Name),
                    new Claim("jwt", token.Jwt),
                    new Claim(ClaimTypes.Role, token.Profile ?? "")
                };
                await auth.SignInAsync(claims);
                if (token.Profile == "ADMIN")
                {
                    return RedirectToAction("Index", "Products");
                }
                else
                {
                    return RedirectToAction("Index", "Home");
                }
            }
            catch (Exception ex) when (IsApiError(ex))
            {
                // 401 = credenciales inválidas (no se redirige a Salir). 429 = rate limiting del API.
                if ((ex as HttpRequestException)?.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    ModelState.AddModelError(string.Empty, "Credenciales inválidas. Inténtelo nuevamente.");
                else
                    ModelState.AddModelError(string.Empty, GetMessageFor(ex));
            }
        }
        return View("Index");
    }

    [Authorize(Roles = "ADMIN,BUYER")]
    [ActionName("Salir")]
    public async Task<IActionResult> SignOutAsync()
    {
        // DD-08: si el API ya soporta revocación, se invalida el JWT antes de borrar la cookie.
        if (configuration.GetValue<bool>("Features:LogoutApi"))
        {
            try { await profile.RevocaSesionAsync(); }
            catch (Exception ex) when (IsApiError(ex)) { /* se cierra la sesión local de todos modos */ }
        }
        HttpContext.Session.Clear();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Auth");
    }

}
