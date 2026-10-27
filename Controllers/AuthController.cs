using System.Security.Claims;
using FrontendNet.Models;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrontendNet;
public class AuthController(AuthClientService auth, PerfilClientService perfil, IConfiguration configuration) : AppController
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
            catch (Exception ex) when (EsErrorApi(ex))
            {
                // 401 = credenciales inválidas (no se redirige a Salir). 429 = rate limiting del API.
                if ((ex as HttpRequestException)?.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    ModelState.AddModelError(string.Empty, "Credenciales inválidas. Inténtelo nuevamente.");
                else
                    ModelState.AddModelError(string.Empty, MensajePara(ex));
            }
        }
        return View();
    }

    [Authorize(Roles = "Administrador,Usuario")]
    public async Task<IActionResult> SalirAsync()
    {
        // DD-08: si el API ya soporta revocación, se invalida el JWT antes de borrar la cookie.
        if (configuration.GetValue<bool>("Features:LogoutApi"))
        {
            try { await perfil.RevocaSesionAsync(); }
            catch (Exception ex) when (EsErrorApi(ex)) { /* se cierra la sesión local de todos modos */ }
        }
        HttpContext.Session.Clear();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Auth");
    }

}
