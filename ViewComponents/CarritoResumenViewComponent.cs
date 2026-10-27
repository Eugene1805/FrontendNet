using FrontendNet.Services;
using Microsoft.AspNetCore.Mvc;

namespace FrontendNet.ViewComponents;

/// <summary>Contador de artículos del carrito para el menú. Si el carrito no responde, no se muestra nada (el menú no debe fallar).</summary>
public class CarritoResumenViewComponent(ICarritoService carrito) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        try
        {
            return View(await carrito.ContarAsync());
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException or TaskCanceledException)
        {
            return Content(string.Empty);
        }
    }
}
