using System.Security.Claims;
using FrontendNet.Models;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrontendNet;
public class AuthController(AuthClientService auth) : Controller
{
    [AllowAnonymous]
    public IActionResult Index()
    {
        return View();
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
                AuthUser token = await auth.ObtenTokenAsync(model.Email, model.Password);
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Email, token.Email),
                    new Claim(ClaimTypes.Name, token.Nombre),
                    new Claim("jwt", token.Jwt),
                    new Claim(ClaimTypes.Role, token.Rol)
                };
                await auth.IniciaSesionAsync(claims);
                if (token.Rol == "Administrador")
                {
                    return RedirectToAction("Index", "Productos");
                }
                else
                {
                    return RedirectToAction("Index", "Home");
                }
            }
            catch (HttpRequestException ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    ModelState.AddModelError(string.Empty, "Credenciales inválidas. Inténtelo nuevamente.");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Ocurrió un error al procesar la solicitud. Inténtelo nuevamente.");
                }
            }
        }
        return View();
    }

    [Authorize(Roles = "Administrador,Usuario")]
    public async Task<IActionResult> SalirAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Auth");
    }

}
