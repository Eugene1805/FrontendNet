using System.Security.Claims;
using FrontendNet.Helpers;
using FrontendNet.Models;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrontendNet;

[Authorize]
public class ProfileController(ProfileClientService profile, AuthClientService auth, IConfiguration configuration) : AppController
{
    private bool Editable => configuration.GetValue<bool>("Features:PerfilEditable");

    public async Task<IActionResult> IndexAsync()
    {
        // D-11: nunca se muestra el JWT completo; solo sus claims informativos.
        var tokens = await auth.GetSessionTokensAsync();
        var info = JwtInfo.Read(tokens?.AccessToken);
        var model = new ProfileSession
        {
            Email = User.FindFirstValue(ClaimTypes.Email) ?? "",
            Name = User.FindFirstValue(ClaimTypes.Name) ?? "",
            Profile = User.FindFirstValue(ClaimTypes.Role) ?? "",
            ExpiraUtc = info?.Expira,
            Emisor = info?.Emisor,
            Audiencia = info?.Audiencia
        };
        try
        {
            model.RemainingTime = await profile.GetRemainingTimeAsync();
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            // Sin el tiempo el resto del profile sigue siendo útil (el API responde 404 si no pudo calcularlo).
            if ((ex as HttpRequestException)?.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return RedirectToLoginWithReturnUrl();
        }
        ViewBag.Editable = Editable;
        return View("Index", model);
    }

    [ActionName("Editar")]
    public IActionResult Edit()
    {
        if (!Editable) return NotFound();
        return View("Edit", new ProfileName { Name = User.FindFirstValue(ClaimTypes.Name) ?? "" });
    }

    [HttpPost]
    [ActionName("Editar")]
    public async Task<IActionResult> EditAsync(ProfileName model)
    {
        if (!Editable) return NotFound();
        if (ModelState.IsValid)
        {
            try
            {
                await profile.UpdateNameAsync(model.Name);
                // El API reemplaza el JWT (Set-Authorization); aquí se actualiza el claim Name de la cookie.
                var claims = User.Claims.Where(c => c.Type != ClaimTypes.Name).ToList();
                claims.Add(new Claim(ClaimTypes.Name, model.Name));
                await auth.SignInAsync(claims);
                TempData["Exito"] = "Nombre actualizado.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (IsApiError(ex))
            {
                var result = HandleApiError(ex, "Nombre");
                if (result != null) return result;
            }
        }
        return View("Edit", model);
    }

    [ActionName("CambiarPassword")]
    public IActionResult ChangePassword()
    {
        if (!Editable) return NotFound();
        return View("ChangePassword", new ChangePassword());
    }

    [HttpPost]
    [ActionName("CambiarPassword")]
    public async Task<IActionResult> ChangePasswordAsync(ChangePassword model)
    {
        if (!Editable) return NotFound();
        if (ModelState.IsValid)
        {
            try
            {
                await profile.PutPasswordAsync(model);
                TempData["Exito"] = "Contraseña actualizada.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (IsApiError(ex))
            {
                // Aquí un 401 significa "contraseña actual incorrecta" (contrato 5.3), no sesión caducada.
                if ((ex as HttpRequestException)?.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    ModelState.AddModelError(nameof(model.CurrentPassword), "La contraseña actual es incorrecta.");
                else
                {
                    var result = HandleApiError(ex);
                    if (result != null) return result;
                }
            }
        }
        // Nunca se devuelven las contraseñas escritas al formulario.
        return View("ChangePassword", new ChangePassword());
    }
}
