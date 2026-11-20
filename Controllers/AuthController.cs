using System.Net;
using FrontendNet.Models;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrontendNet;

public class AuthController(
    AuthClientService auth,
    ProfileClientService profile,
    CloudflareTurnstileService turnstile) : AppController
{
    [AllowAnonymous]
    public IActionResult Index() => View("Index");

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> IndexAsync(Login model)
    {
        if (!ModelState.IsValid)
            return View("Index", model);

        string? turnstileResponse = Request.Form["cf-turnstile-response"];
        var isHuman = await turnstile.VerifyTokenAsync(
            turnstileResponse,
            HttpContext.Connection.RemoteIpAddress?.ToString());

        if (!isHuman)
        {
            ModelState.AddModelError(string.Empty, "Verificación de seguridad fallida. Por favor, intente de nuevo.");
            return View("Index", model);
        }

        try
        {
            var token = await auth.LoginAsync(model.Email, model.Password);
            await auth.SignInAsync(token);

            return token.Profile?.ToUpperInvariant() switch
            {
                Roles.Admin => RedirectToAction("Index", "Orders"),
                Roles.Seller when string.Equals(token.SellerStatus, SellerStatuses.Approved, StringComparison.OrdinalIgnoreCase) =>
                    RedirectToAction("Index", "Products"),
                Roles.Seller => RedirectToAction("Index", "Profile"),
                Roles.Buyer => RedirectToAction("Index", "Shopping"),
                _ => RedirectToAction("Index", "Home")
            };
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            if (GetStatusCode(ex) == HttpStatusCode.Unauthorized)
                ModelState.AddModelError(string.Empty, "Credenciales inválidas. Inténtelo nuevamente.");
            else
                ModelState.AddModelError(string.Empty, GetUserMessage(ex));
        }

        return View("Index", model);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LogoutAsync()
    {
        try
        {
            await profile.LogoutAsync();
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            // Local cookie and session state must still be cleared even if API revocation fails.
        }

        await auth.SignOutLocalAsync();
        return RedirectToAction("Index", "Auth");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Logout() => RedirectToAction("Index", "Home");

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register() => View("Register", new RegisterRequestModel());

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterAsync(RegisterRequestModel model)
    {
        if (!ModelState.IsValid)
            return View("Register", model);

        string? turnstileResponse = Request.Form["cf-turnstile-response"];
        var isHuman = await turnstile.VerifyTokenAsync(
            turnstileResponse,
            HttpContext.Connection.RemoteIpAddress?.ToString());

        if (!isHuman)
        {
            ModelState.AddModelError(string.Empty, "Verificación de seguridad fallida. Por favor, intente de nuevo.");
            return View("Register", model);
        }

        try
        {
            var message = await auth.RegisterAsync(model);
            TempData["PendingVerificationEmail"] = model.Email.Trim();
            TempData["Success"] = string.IsNullOrWhiteSpace(message)
                ? "Hemos enviado un código de verificación a tu correo electrónico."
                : $"{message} Revisa tu correo electrónico para obtener el código de 6 dígitos.";

            return RedirectToAction(nameof(VerifyRegistration), new { email = model.Email.Trim() });
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            if (GetStatusCode(ex) == HttpStatusCode.Conflict)
                ModelState.AddModelError(nameof(model.Email), GetUserMessage(ex));
            else
                ModelState.AddModelError(string.Empty, GetUserMessage(ex));
        }

        return View("Register", model);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult VerifyRegistration(string? email = null)
    {
        var resolvedEmail = !string.IsNullOrWhiteSpace(email)
            ? email.Trim()
            : TempData["PendingVerificationEmail"] as string ?? string.Empty;

        return View("VerifyRegistration", new VerifyRegistrationModel { Email = resolvedEmail });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyRegistrationAsync(VerifyRegistrationModel model)
    {
        if (!ModelState.IsValid)
            return View("VerifyRegistration", model);

        try
        {
            var result = await auth.VerifyRegistrationAsync(model.Email, model.Code);
            TempData["Success"] = string.Equals(result.Profile, Roles.Seller, StringComparison.OrdinalIgnoreCase)
                ? "Cuenta de vendedor verificada. Un administrador debe aprobar tu tienda antes de que puedas publicar productos."
                : "Cuenta verificada correctamente. Ya puedes iniciar sesión.";

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            ModelState.AddModelError(string.Empty, GetUserMessage(ex));
        }

        return View("VerifyRegistration", model);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ForgotPassword() => View("ForgotPassword", new ForgotPasswordModel());

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPasswordAsync(ForgotPasswordModel model)
    {
        if (!ModelState.IsValid)
            return View("ForgotPassword", model);

        try
        {
            var message = await auth.ForgotPasswordAsync(model.Email);
            TempData["PendingResetEmail"] = model.Email.Trim();
            TempData["Success"] = string.IsNullOrWhiteSpace(message)
                ? "Si existe una cuenta asociada, recibirás un código de recuperación."
                : message;

            return RedirectToAction(nameof(ResetPassword), new { email = model.Email.Trim() });
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            ModelState.AddModelError(string.Empty, GetUserMessage(ex));
        }

        return View("ForgotPassword", model);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ResetPassword(string? email = null)
    {
        var resolvedEmail = !string.IsNullOrWhiteSpace(email)
            ? email.Trim()
            : TempData["PendingResetEmail"] as string ?? string.Empty;

        return View("ResetPassword", new ResetPasswordModel { Email = resolvedEmail });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPasswordAsync(ResetPasswordModel model)
    {
        if (!ModelState.IsValid)
            return View("ResetPassword", model);

        try
        {
            await auth.ResetPasswordAsync(model.Email, model.Code, model.NewPassword);
            TempData["Success"] = "Contraseña restablecida correctamente. Inicia sesión con tu nueva contraseña.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            ModelState.AddModelError(string.Empty, GetUserMessage(ex));
        }

        return View("ResetPassword", model);
    }
}
