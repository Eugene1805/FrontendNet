using System.Security.Claims;
using FrontendNet.Helpers;
using FrontendNet.Models;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrontendNet;

[Authorize]
public class PerfilController(PerfilClientService perfil, AuthClientService auth, IConfiguration configuration) : AppController
{
    private bool Editable => configuration.GetValue<bool>("Features:PerfilEditable");

    public async Task<IActionResult> IndexAsync()
    {
        // D-11: nunca se muestra el JWT completo; solo sus claims informativos.
        var info = JwtInfo.Lee(User.FindFirstValue("jwt"));
        var modelo = new PerfilSesion
        {
            Email = User.FindFirstValue(ClaimTypes.Email) ?? "",
            Nombre = User.FindFirstValue(ClaimTypes.Name) ?? "",
            Rol = User.FindFirstValue(ClaimTypes.Role) ?? "",
            ExpiraUtc = info?.Expira,
            Emisor = info?.Emisor,
            Audiencia = info?.Audiencia
        };
        try
        {
            modelo.TiempoRestante = await perfil.ObtenTiempoAsync();
        }
        catch (Exception ex) when (EsErrorApi(ex))
        {
            // Sin el tiempo el resto del perfil sigue siendo útil (el API responde 404 si no pudo calcularlo).
            if ((ex as HttpRequestException)?.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return RedirectToAction("Salir", "Auth");
        }
        ViewBag.Editable = Editable;
        return View(modelo);
    }

    public IActionResult Editar()
    {
        if (!Editable) return NotFound();
        return View(new PerfilNombre { Nombre = User.FindFirstValue(ClaimTypes.Name) ?? "" });
    }

    [HttpPost]
    public async Task<IActionResult> EditarAsync(PerfilNombre model)
    {
        if (!Editable) return NotFound();
        if (ModelState.IsValid)
        {
            try
            {
                await perfil.PutNombreAsync(model.Nombre);
                // El API reemplaza el JWT (Set-Authorization); aquí se actualiza el claim Name de la cookie.
                var claims = User.Claims.Where(c => c.Type != ClaimTypes.Name).ToList();
                claims.Add(new Claim(ClaimTypes.Name, model.Nombre));
                await auth.IniciaSesionAsync(claims);
                TempData["Exito"] = "Nombre actualizado.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (EsErrorApi(ex))
            {
                var salida = ManejaErrorApi(ex, "Nombre");
                if (salida != null) return salida;
            }
        }
        return View(model);
    }

    public IActionResult CambiarPassword()
    {
        if (!Editable) return NotFound();
        return View(new CambioPassword());
    }

    [HttpPost]
    public async Task<IActionResult> CambiarPasswordAsync(CambioPassword model)
    {
        if (!Editable) return NotFound();
        if (ModelState.IsValid)
        {
            try
            {
                await perfil.PutPasswordAsync(model);
                TempData["Exito"] = "Contraseña actualizada.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (EsErrorApi(ex))
            {
                // Aquí un 401 significa "contraseña actual incorrecta" (contrato 5.3), no sesión caducada.
                if ((ex as HttpRequestException)?.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    ModelState.AddModelError(nameof(model.PasswordActual), "La contraseña actual es incorrecta.");
                else
                {
                    var salida = ManejaErrorApi(ex);
                    if (salida != null) return salida;
                }
            }
        }
        // Nunca se devuelven las contraseñas escritas al formulario.
        return View(new CambioPassword());
    }
}
