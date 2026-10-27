using System.Net;
using System.Security.Claims;
using System.Text.Json;
using FrontendNet.Models;
namespace FrontendNet.Services;

/// <summary>
/// SIMULACIÓN del carrito: guarda solo { productoId, cantidad } en la sesión del servidor web
/// (nunca precios) y resuelve título y precio contra el API en cada lectura, de modo que el cliente no puede manipularlos.
/// </summary>
public class CarritoSesionService(IHttpContextAccessor accessor, ProductoClientService productos) : ICarritoService
{
    public const int CantidadMaxima = 99;

    private HttpContext Contexto => accessor.HttpContext
        ?? throw new InvalidOperationException("No hay un HttpContext activo.");

    // El carrito se identifica por el usuario autenticado, no por un dato enviado por el cliente.
    private string Clave => "carrito:" + (Contexto.User.FindFirstValue(ClaimTypes.Email) ?? "");

    private Dictionary<int, int> Lee()
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
        if (cantidad < 1 || cantidad > CantidadMaxima)
            throw ApiException.De(HttpStatusCode.BadRequest, $"La cantidad debe estar entre 1 y {CantidadMaxima}.");
    }

    // Sin consultar el API: solo suma las cantidades guardadas en la sesión.
    public Task<int> ContarAsync() => Task.FromResult(Lee().Values.Sum());

    public async Task<Carrito> GetAsync()
    {
        var guardado = Lee();
        var carrito = new Carrito();
        var modificado = false;
        foreach (var (productoId, cantidad) in guardado.ToList())
        {
            Producto? producto;
            try
            {
                producto = await productos.GetAsync(productoId);
            }
            catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                producto = null;
            }
            if (producto == null)
            {
                // El producto fue eliminado: se retira del carrito.
                guardado.Remove(productoId);
                modificado = true;
                continue;
            }
            carrito.Items.Add(new CarritoItem
            {
                ProductoId = productoId,
                Titulo = producto.Titulo,
                Precio = producto.Precio,
                ArchivoId = producto.ArchivoId,
                Cantidad = cantidad,
                Subtotal = producto.Precio * cantidad
            });
        }
        if (modificado) Guarda(guardado);
        carrito.TotalArticulos = carrito.Items.Sum(i => i.Cantidad);
        carrito.Total = carrito.Items.Sum(i => i.Subtotal);
        return carrito;
    }

    public async Task<Carrito> AgregarAsync(int productoId, int cantidad)
    {
        ValidaCantidad(cantidad);
        // Lanza 404 si el producto no existe.
        _ = await productos.GetAsync(productoId) ?? throw ApiException.De(HttpStatusCode.NotFound, "Producto no encontrado.");
        var items = Lee();
        items[productoId] = Math.Min(CantidadMaxima, items.GetValueOrDefault(productoId) + cantidad);
        Guarda(items);
        return await GetAsync();
    }

    public async Task<Carrito> ActualizarAsync(int productoId, int cantidad)
    {
        ValidaCantidad(cantidad);
        var items = Lee();
        if (!items.ContainsKey(productoId))
            throw ApiException.De(HttpStatusCode.NotFound, "El producto no está en el carrito.");
        items[productoId] = cantidad;
        Guarda(items);
        return await GetAsync();
    }

    public Task QuitarAsync(int productoId)
    {
        var items = Lee();
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
