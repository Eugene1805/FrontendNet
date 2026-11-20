using System.Net;
using FrontendNet.Models;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrontendNet;

[Authorize(Roles = Roles.Buyer)]
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
            return await HandleGetErrorAsync(ex);
        }
    }

    [HttpPost]
    public async Task<IActionResult> AddAsync(int productId, int quantity = 1, string? returnUrl = null)
    {
        try
        {
            await shoppingCart.AddAsync(productId, quantity);
            TempData["Success"] = "Producto agregado al carrito.";
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return await HandleFailureAsync(ex, returnUrl);
        }

        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> UpdateAsync(int productId, int quantity)
    {
        try
        {
            await shoppingCart.UpdateAsync(productId, quantity);
            TempData["Success"] = "Cantidad actualizada.";
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return await HandleFailureAsync(ex, null);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> RemoveAsync(int productId)
    {
        try
        {
            await shoppingCart.RemoveAsync(productId);
            TempData["Success"] = "Producto eliminado del carrito.";
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return await HandleFailureAsync(ex, null);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ClearAsync()
    {
        try
        {
            await shoppingCart.ClearAsync();
            TempData["Success"] = "Carrito vaciado correctamente.";
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return await HandleFailureAsync(ex, null);
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> HandleFailureAsync(Exception ex, string? returnUrl)
    {
        if (GetStatusCode(ex) == HttpStatusCode.Unauthorized)
            return await HandleUnauthorizedAsync();

        TempData["Error"] = GetUserMessage(ex);
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction(nameof(Index));
    }
}
