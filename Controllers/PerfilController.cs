using System.Security.Claims;
using FrontendNet.Models;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrontendNet;

[Authorize]
public class PerfilController(PerfilClientService perfil) : Controller
{
    public async Task<IActionResult> IndexAsync()
    {
        AuthUser? usuario = null;
        try
        {
            ViewBag.timeRemaining = await perfil.ObtenTiempoAsync();
            // Obtiene el perfil del usuario
            usuario = new AuthUser
            {
                Email = User.FindFirstValue(ClaimTypes.Email)!,
                Nombre = User.FindFirstValue(ClaimTypes.Name)!,
                Rol = User.FindFirstValue(ClaimTypes.Role)!,
                Jwt = User.FindFirstValue("jwt")!
            };
        }
        catch (HttpRequestException ex)
        {
            if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return RedirectToAction("Salir", "Auth");
        }
        return View(usuario);
    }
}