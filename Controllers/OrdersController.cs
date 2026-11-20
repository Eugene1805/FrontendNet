using System.Net;
using FrontendNet.Helpers;
using FrontendNet.Models;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FrontendNet;

[Authorize(Roles = $"{Roles.Buyer},{Roles.Seller},{Roles.Admin}")]
public class OrdersController(
    IOrdersService orders,
    IShoppingCartService shoppingCart,
    AddressesClientService addressesClient) : AppController
{
    public record OrderStatusOption(string Value, string Text);

    private static readonly OrderStatusOption[] StatusOptions = OrderStatus.All
        .Select(s => new OrderStatusOption(s, DisplayFormatter.OrderStatusLabel(s)))
        .ToArray();

    public async Task<IActionResult> Index(string? status, int page = 1)
    {
        if (IsUnapprovedSeller())
            return View("~/Views/Products/SellerPending.cshtml");

        try
        {
            var items = await orders.GetAsync(status);
            ViewBag.SelectedStatus = status;
            ViewBag.Statuses = new SelectList(StatusOptions, "Value", "Text", status);
            return View("Index", Paginate(items, page));
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return await HandleGetErrorAsync(ex);
        }
    }

    public async Task<IActionResult> Details(string id)
    {
        if (IsUnapprovedSeller())
            return View("~/Views/Products/SellerPending.cshtml");

        try
        {
            var order = await orders.GetOrderAsync(id);
            if (order is null) return NotFound();
            return View("Details", order);
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return await HandleGetErrorAsync(ex);
        }
    }

    [Authorize(Roles = Roles.Buyer)]
    public async Task<IActionResult> Checkout(string? addressId = null)
    {
        try
        {
            var cart = await shoppingCart.GetAsync();
            if (cart.Items.Count == 0)
            {
                TempData["Error"] = "El carrito está vacío.";
                return RedirectToAction("Index", "ShoppingCart");
            }

            var addresses = await addressesClient.GetAsync();
            var selectedAddressId = !string.IsNullOrWhiteSpace(addressId) && addresses.Any(a => a.Id == addressId)
                ? addressId
                : addresses.FirstOrDefault(a => a.IsDefault)?.Id ?? addresses.FirstOrDefault()?.Id;

            var viewModel = new CheckoutViewModel
            {
                Cart = cart,
                Addresses = addresses,
                SelectedAddressId = selectedAddressId
            };

            if (!string.IsNullOrWhiteSpace(selectedAddressId))
            {
                try
                {
                    viewModel.Quote = await orders.GetQuoteAsync(selectedAddressId);
                }
                catch (Exception ex) when (IsApiError(ex))
                {
                    if (GetStatusCode(ex) == HttpStatusCode.Unauthorized)
                        return await HandleUnauthorizedAsync();

                    viewModel.QuoteError = GetUserMessage(ex);
                }
            }

            return View("Checkout", viewModel);
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return await HandleGetErrorAsync(ex);
        }
    }

    [HttpPost]
    [Authorize(Roles = Roles.Buyer)]
    public async Task<IActionResult> ConfirmCheckoutAsync(string quote, string? addressId = null)
    {
        if (string.IsNullOrWhiteSpace(quote))
        {
            TempData["Error"] = "Debe generar una cotización válida antes de confirmar el pedido.";
            return RedirectToAction(nameof(Checkout), new { addressId });
        }

        try
        {
            var result = await orders.ConfirmCheckoutAsync(quote);
            if (result.Orders.Count == 1)
            {
                var createdOrder = result.Orders[0];
                TempData["Success"] = $"Pedido #{createdOrder.Id} creado correctamente.";
                return RedirectToAction(nameof(Details), new { id = createdOrder.Id });
            }

            var idsSummary = string.Join(", ", result.Orders.Select(o => $"#{o.Id}"));
            TempData["Success"] = $"Se crearon {result.Orders.Count} pedidos correctamente ({idsSummary}).";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            if (GetStatusCode(ex) == HttpStatusCode.Unauthorized)
                return await HandleUnauthorizedAsync();

            TempData["Error"] = GetUserMessage(ex);
            return RedirectToAction(nameof(Checkout), new { addressId });
        }
    }

    [HttpPost]
    [Authorize(Roles = Roles.Buyer)]
    public async Task<IActionResult> CancelAsync(string id, string? reason)
    {
        var trimmedReason = reason?.Trim() ?? string.Empty;
        if (trimmedReason.Length is < 1 or > 255)
        {
            TempData["Error"] = "Indique el motivo de cancelación (entre 1 y 255 caracteres).";
            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            await orders.CancelAsync(id, trimmedReason);
            TempData["Success"] = "Pedido cancelado correctamente.";
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            var result = await HandleApiErrorAsync(ex);
            if (result is not null) return result;
            TempData["Error"] = GetUserMessage(ex);
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.Seller},{Roles.Admin}")]
    public async Task<IActionResult> UpdateStatusAsync(string id, string status, string? reason = null)
    {
        if (IsUnapprovedSeller())
            return View("~/Views/Products/SellerPending.cshtml");

        var normalizedStatus = status?.Trim().ToUpperInvariant() ?? string.Empty;
        var trimmedReason = reason?.Trim();

        if (normalizedStatus == OrderStatus.Canceled && string.IsNullOrWhiteSpace(trimmedReason))
        {
            TempData["Error"] = "Debe indicar un motivo para cancelar el pedido.";
            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            await orders.UpdateStatusAsync(id, normalizedStatus, trimmedReason);
            TempData["Success"] = $"Estado actualizado a {DisplayFormatter.OrderStatusLabel(normalizedStatus)}.";
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            var result = await HandleApiErrorAsync(ex);
            if (result is not null) return result;
            TempData["Error"] = GetUserMessage(ex);
        }

        return RedirectToAction(nameof(Details), new { id });
    }
}
