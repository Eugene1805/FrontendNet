using FrontendNet.Models;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FrontendNet;

/// <summary>
/// User: checkout, sus orders y cancelar. ADMIN: todos los orders y cambio de estado.
/// El API (o la simulación) decide qué orders puede ver cada rol; un pedido ajeno responde 404.
/// </summary>
[Authorize(Roles = "BUYER,ADMIN")]
public class OrdersController(IOrdersService orders, IShoppingCartService shoppingCart) : AppController
{
    public async Task<IActionResult> Index(string? estado, int page = 1)
    {
        try
        {
            var items = await orders.GetAsync(estado);
            ViewBag.Estado = estado;
            ViewBag.Estados = new SelectList(OrderStatus.All, estado);
            return View("Index", Paginate(items, page));
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return ErrorGet(ex);
        }
    }

    [ActionName("Detalle")]
    public async Task<IActionResult> Details(string id)
    {
        try
        {
            var pedido = await orders.GetOrderAsync(id);
            if (pedido == null) return NotFound();
            return View("Details", pedido);
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return ErrorGet(ex);
        }
    }

    [Authorize(Roles = "BUYER")]
    public async Task<IActionResult> Checkout()
    {
        try
        {
            var actual = await shoppingCart.GetAsync();
            if (actual.Items.Count == 0)
            {
                TempData["Error"] = "El carrito está vacío.";
                return RedirectToAction("Index", "ShoppingCart");
            }
            ViewBag.ShoppingCart = actual;
            return View("Checkout", new NewOrder());
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return ErrorGet(ex);
        }
    }

    [HttpPost]
    [Authorize(Roles = "BUYER")]
    public async Task<IActionResult> CheckoutAsync(NewOrder model)
    {
        // El único método de pago disponible es el simulado (DD-04); el cliente no decide otra cosa.
        model.MetodoPago = "simulado";
        try
        {
            if (ModelState.IsValid)
            {
                var pedido = await orders.CrearAsync(model);
                TempData["Exito"] = $"Pedido #{pedido.OrderId} creado correctamente.";
                return RedirectToAction("Detalle", new { id = pedido.OrderId });
            }
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            var result = HandleApiError(ex);
            if (result != null) return result;
        }
        try
        {
            ViewBag.ShoppingCart = await shoppingCart.GetAsync();
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return ErrorGet(ex);
        }
        return View("Checkout", model);
    }

    [HttpPost]
    [Authorize(Roles = "BUYER")]
    [ActionName("Cancelar")]
    public async Task<IActionResult> Cancel(string id)
    {
        try
        {
            await orders.CancelAsync(id, "Cancelado por el comprador.");
            TempData["Exito"] = "Pedido cancelado.";
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            var result = HandleApiError(ex);
            if (result != null) return result;
            TempData["Error"] = GetMessageFor(ex);
        }
        return RedirectToAction("Detalle", new { id });
    }

    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    [ActionName("Estado")]
    public async Task<IActionResult> Status(string id, string status)
    {
        try
        {
            await orders.ChangeStatusAsync(id, status);
            TempData["Exito"] = $"Estado actualizado a {status}.";
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            var result = HandleApiError(ex);
            if (result != null) return result;
            TempData["Error"] = GetMessageFor(ex);
        }
        return RedirectToAction("Detalle", new { id });
    }
}
