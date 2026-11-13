using FrontendNet.Services;
using Microsoft.AspNetCore.Mvc;

namespace FrontendNet.ViewComponents;

/// <summary>Contador de artículos del shoppingCart para el menú. Si el shoppingCart no responde, no se muestra nada (el menú no debe fallar).</summary>
[ViewComponent(Name = "CarritoResumen")]
public class ShoppingCartSummaryViewComponent(IShoppingCartService shoppingCart) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        try
        {
            return View(await shoppingCart.ContarAsync());
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException or TaskCanceledException)
        {
            return Content(string.Empty);
        }
    }
}
