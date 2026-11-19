using System.Security.Claims;
using FrontendNet.Models;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrontendNet;

public class AuthController(AuthClientService auth, ProfileClientService profile, CloudflareTurnstileService turnstile) : AppController
{
    [AllowAnonymous]
    public IActionResult Index(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : null;
        return View("Index");
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> IndexAsync(Login model, string? returnUrl = null)
    {
        var safeReturnUrl = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : null;
        ViewData["ReturnUrl"] = safeReturnUrl;

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
                await auth.SignInAsync(token);

                if (safeReturnUrl != null)
                {
                    return LocalRedirect(safeReturnUrl);
                }

                var claims = AuthClientService.NormalizaClaims(token);
                if (claims.Any(c => c.Type == ClaimTypes.Role && (c.Value == "ADMIN" || c.Value == "Administrador")))
                {
                    return RedirectToAction("Index", "Products");
                }
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex) when (IsApiError(ex))
            {
                if ((ex as HttpRequestException)?.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    ModelState.AddModelError(string.Empty, "Credenciales inválidas. Inténtelo nuevamente.");
                else
                    ModelState.AddModelError(string.Empty, GetMessageFor(ex));
            }
        }
        return View("Index");
    }

    // SEC-01: Logout seguro exclusivamente vía POST con Anti-Forgery Token.
    // Revoca la sesión en Redis (POST /api/auth/logout) y elimina la cookie local .frontendnet.
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    [ActionName("Salir")]
    public async Task<IActionResult> SignOutAsync()
    {
        try
        {
            await profile.RevocaSesionAsync();
        }
        catch (Exception)
        {
            // Si el API no responde o la sesión ya expiró/es inválida, nunca falla el cierre local.
        }

        await auth.SignOutLocalAsync();
        return RedirectToAction("Index", "Auth");
    }
}
