using System.Security.Claims;
using FrontendNet.Models;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrontendNet;
public class AuthController(AuthClientService auth, ProfileClientService profile, IConfiguration configuration, CloudflareTurnstileService turnstile) : AppController
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
            // SEC-03: Verificación de bot / protección contra fuerza bruta con Cloudflare Turnstile
            string? turnstileResponse = Request.Form["cf-turnstile-response"];
            var esHumano = await turnstile.VerifyTokenAsync(turnstileResponse, HttpContext.Connection.RemoteIpAddress?.ToString());
            if (!esHumano)
            {
                ModelState.AddModelError(string.Empty, "Verificación de seguridad fallida. Por favor, intente de nuevo.");
                return View("Index");
            }

            try
            {
                AuthUser token = await auth.GetTokenAsync(model.Email, model.Password);
                // SEC-02: Normalización de claims para congruencia RBAC entre API y Vistas Razor
                var claims = AuthClientService.NormalizaClaims(token);
                await auth.SignInAsync(claims);
                if (claims.Any(c => c.Type == ClaimTypes.Role && (c.Value == "ADMIN" || c.Value == "Administrador")))
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

    // SEC-01: Logout seguro vía POST con Anti-Forgery Token
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
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

    // Redirección segura para intentos GET accidentales (evita error 405 y neutraliza Logout CSRF)
    [HttpGet]
    [ActionName("Salir")]
    public IActionResult SignOutGet()
    {
        return RedirectToAction("Index", "Home");
    }

}
