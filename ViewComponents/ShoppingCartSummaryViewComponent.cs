using FrontendNet.Services;
using Microsoft.AspNetCore.Mvc;

namespace FrontendNet.ViewComponents;

/// <summary>Cart item count badge in the navigation bar. Degrades gracefully if the API fails.</summary>
[ViewComponent(Name = "ShoppingCartSummary")]
public class ShoppingCartSummaryViewComponent(IShoppingCartService shoppingCart) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        try
        {
            return View(await shoppingCart.GetCountAsync());
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException or TaskCanceledException)
        {
            return Content(string.Empty);
        }
    }
}
