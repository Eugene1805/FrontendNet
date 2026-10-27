using FrontendNet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrontendNet;

[Authorize(Roles = "Usuario")]
public class CarritoController(ICarritoService carrito) : AppController
{
    public async Task<IActionResult> Index()
    {
        try
        {
            return View(await carrito.GetAsync());
        }
        catch (Exception ex) when (EsErrorApi(ex))
        {
            return ErrorGet(ex);
        }
    }

    // Las acciones que modifican datos son POST y todos los POST validan antiforgery (filtro global).
    [HttpPost]
    public async Task<IActionResult> Agregar(int productoid, int cantidad = 1, string? returnUrl = null)
    {
        try
        {
            await carrito.AgregarAsync(productoid, cantidad);
            TempData["Exito"] = "Producto agregado al carrito.";
        }
        catch (Exception ex) when (EsErrorApi(ex))
        {
            return Falla(ex, returnUrl);
        }
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Actualizar(int productoid, int cantidad)
    {
        try
        {
            await carrito.ActualizarAsync(productoid, cantidad);
        }
        catch (Exception ex) when (EsErrorApi(ex))
        {
            return Falla(ex, null);
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Quitar(int productoid)
    {
        try
        {
            await carrito.QuitarAsync(productoid);
        }
        catch (Exception ex) when (EsErrorApi(ex))
        {
            return Falla(ex, null);
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Vaciar()
    {
        try
        {
            await carrito.VaciarAsync();
        }
        catch (Exception ex) when (EsErrorApi(ex))
        {
            return Falla(ex, null);
        }
        return RedirectToAction(nameof(Index));
    }

    private IActionResult Falla(Exception ex, string? returnUrl)
    {
        // Aquí un 404 (producto inexistente) o 400 (cantidad inválida) se informa en la página, no como error.
        if ((ex as HttpRequestException)?.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            return RedirectToAction("Salir", "Auth");
        TempData["Error"] = MensajePara(ex);
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction(nameof(Index));
    }
}
