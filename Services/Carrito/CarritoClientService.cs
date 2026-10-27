using FrontendNet.Models;
namespace FrontendNet.Services;

/// <summary>Implementación definitiva contra el contrato PROPUESTO api/carrito (planeacion 5.1). Aún no existe en el backend.</summary>
public class CarritoClientService(HttpClient client) : ICarritoService
{
    public async Task<Carrito> GetAsync()
        => await client.GetFromJsonAsync<Carrito>("api/carrito") ?? new Carrito();

    public async Task<int> ContarAsync() => (await GetAsync()).TotalArticulos;

    public async Task<Carrito> AgregarAsync(int productoId, int cantidad)
    {
        using var response = await client.PostAsJsonAsync("api/carrito/items", new { productoid = productoId, cantidad });
        return await response.Content.ReadFromJsonAsync<Carrito>() ?? new Carrito();
    }

    public async Task<Carrito> ActualizarAsync(int productoId, int cantidad)
    {
        using var response = await client.PutAsJsonAsync($"api/carrito/items/{productoId}", new { cantidad });
        return await response.Content.ReadFromJsonAsync<Carrito>() ?? new Carrito();
    }

    public async Task QuitarAsync(int productoId)
    {
        using var response = await client.DeleteAsync($"api/carrito/items/{productoId}");
    }

    public async Task VaciarAsync()
    {
        using var response = await client.DeleteAsync("api/carrito");
    }
}
