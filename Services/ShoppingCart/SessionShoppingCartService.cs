using System.Net;
using System.Security.Claims;
using System.Text.Json;
using FrontendNet.Models;
namespace FrontendNet.Services;

/// <summary>
/// SIMULACIÓN del shoppingCart: guarda solo { productoId, cantidad } en la sesión del servidor web
/// (nunca precios) y resuelve título y precio contra el API en cada lectura, de modo que el cliente no puede manipularlos.
/// </summary>
public class SessionShoppingCartService(IHttpContextAccessor accessor, ProductClientService products) : IShoppingCartService
{
    public const int MaximumQuantity = 99;

    private HttpContext Contexto => accessor.HttpContext
        ?? throw new InvalidOperationException("No hay un HttpContext activo.");

    // El shoppingCart se identifica por el usuario autenticado, no por un dato enviado por el cliente.
    private string Clave => "carrito:" + (Contexto.User.FindFirstValue(ClaimTypes.Email) ?? "");

    private Dictionary<int, int> Read()
    {
        var json = Contexto.Session.GetString(Clave);
        return string.IsNullOrEmpty(json)
            ? []
            : JsonSerializer.Deserialize<Dictionary<int, int>>(json) ?? [];
    }

    private void Guarda(Dictionary<int, int> items)
        => Contexto.Session.SetString(Clave, JsonSerializer.Serialize(items));

    private static void ValidaCantidad(int cantidad)
    {
        if (cantidad < 1 || cantidad > MaximumQuantity)
            throw ApiException.Create(HttpStatusCode.BadRequest, $"La cantidad debe estar entre 1 y {MaximumQuantity}.");
    }

    // Sin consultar el API: solo suma las cantidades guardadas en la sesión.
    public Task<int> ContarAsync() => Task.FromResult(Read().Values.Sum());

    public async Task<ShoppingCart> GetAsync()
    {
        var guardado = Read();
        var shoppingCart = new ShoppingCart();
        var modificado = false;
        foreach (var (productoId, cantidad) in guardado.ToList())
        {
            Product? producto;
            try
            {
                producto = await products.GetAsync(productoId);
            }
            catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                producto = null;
            }
            if (producto == null)
            {
                // El producto fue eliminado: se retira del shoppingCart.
                guardado.Remove(productoId);
                modificado = true;
                continue;
            }
            shoppingCart.Items.Add(new CartItem
            {
                ProductId = productoId,
                Title = producto.Title,
                UnitPrice = producto.Price.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                Quantity = cantidad,
                Subtotal = (producto.Price * cantidad).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
            });
        }
        if (modificado) Guarda(guardado);
        shoppingCart.Subtotal = shoppingCart.Items.Sum(i => i.SubtotalValue)
            .ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        return shoppingCart;
    }

    public async Task<ShoppingCart> AgregarAsync(int productoId, int cantidad)
    {
        ValidaCantidad(cantidad);
        // Lanza 404 si el producto no existe.
        _ = await products.GetAsync(productoId) ?? throw ApiException.Create(HttpStatusCode.NotFound, "Producto no encontrado.");
        var items = Read();
        items[productoId] = Math.Min(MaximumQuantity, items.GetValueOrDefault(productoId) + cantidad);
        Guarda(items);
        return await GetAsync();
    }

    public async Task<ShoppingCart> ActualizarAsync(int productoId, int cantidad)
    {
        ValidaCantidad(cantidad);
        var items = Read();
        if (!items.ContainsKey(productoId))
            throw ApiException.Create(HttpStatusCode.NotFound, "El producto no está en el carrito.");
        items[productoId] = cantidad;
        Guarda(items);
        return await GetAsync();
    }

    public Task QuitarAsync(int productoId)
    {
        var items = Read();
        items.Remove(productoId);
        Guarda(items);
        return Task.CompletedTask;
    }

    public Task VaciarAsync()
    {
        Contexto.Session.Remove(Clave);
        return Task.CompletedTask;
    }
}
