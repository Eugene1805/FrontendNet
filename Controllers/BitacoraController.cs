using FrontendNet.Models;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrontendNet;

[Authorize(Roles = "Administrador")]
public class BitacoraController(BitacoraClientService bitacora) : Controller
{
    public async Task<IActionResult> IndexAsync()
    {
        List<Bitacora>? lista = null;
        try
        {
            lista = await bitacora.GetAsync();
        }
        catch (HttpRequestException ex)
        {
            if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return RedirectToAction("Salir", "Auth");
            }
        }
        return View(lista);
    }
}