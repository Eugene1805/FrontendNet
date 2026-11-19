using FrontendNet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrontendNet;

[Authorize(Roles = "BUYER,Usuario")]
public class ShoppingCartController(IShoppingCartService shoppingCart) : AppController
{
    public async Task<IActionResult> Index()
    {
        try
        {
            return View("Index", await shoppingCart.GetAsync());
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return ErrorGet(ex);
        }
    }

    // Las acciones que modifican datos son POST y todos los POST validan antiforgery (filtro global).
    [HttpPost]
    [ActionName("Agregar")]
    public async Task<IActionResult> Add(int productId, int cantidad = 1, string? returnUrl = null)
    {
        try
        {
            await shoppingCart.AgregarAsync(productId, cantidad);
            TempData["Exito"] = "Producto agregado al carrito.";
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return Falla(ex, returnUrl);
        }
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ActionName("Actualizar")]
    public async Task<IActionResult> Update(int productId, int cantidad)
    {
        try
        {
            await shoppingCart.ActualizarAsync(productId, cantidad);
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return Falla(ex, null);
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ActionName("Quitar")]
    public async Task<IActionResult> Remove(int productId)
    {
        try
        {
            await shoppingCart.QuitarAsync(productId);
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return Falla(ex, null);
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ActionName("Vaciar")]
    public async Task<IActionResult> Clear()
    {
        try
        {
            await shoppingCart.VaciarAsync();
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return Falla(ex, null);
        }
        return RedirectToAction(nameof(Index));
    }

    private IActionResult Falla(Exception ex, string? returnUrl)
    {
        // Aquí un 404 (producto inexistente) o 400 (cantidad inválida) se informa en la página, no como error.
        if ((ex as HttpRequestException)?.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            return RedirectToLoginWithReturnUrl();
        TempData["Error"] = GetMessageFor(ex);
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction(nameof(Index));
    }
}
