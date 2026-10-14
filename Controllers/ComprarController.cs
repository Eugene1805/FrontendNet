using FrontendNet.Models;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrontendNet;

[Authorize(Roles = "Usuario")]
public class ComprarController(ProductoClientService productos, IConfiguration configuration) : Controller
{
    public async Task<IActionResult> Index(string? s)
    {
        List<Producto>? lista = [];
        try
        {
            lista = await productos.GetAsync(s);
        }
        catch (HttpRequestException ex)
        {
            if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return RedirectToAction("Salir", "Auth");
        }
        ViewBag.Url = configuration["UrlWebAPI"];
        ViewBag.search = s;
        return View(lista);
    }
}