using FrontendNet.Helpers;
using FrontendNet.Models;
namespace FrontendNet.Services;

public class ProductoClientService(HttpClient client)
{
    public async Task<List<Producto>?> GetAsync(string? search)
    {
        var url = string.IsNullOrWhiteSpace(search) ? "api/productos" : $"api/productos?s={Url.Codifica(search)}";
        return await client.GetFromJsonAsync<List<Producto>>(url);
    }
    public async Task<Producto?> GetAsync(int id)
    {
        return await client.GetFromJsonAsync<Producto>($"api/productos/{id}");
    }
    // El API lee "archivoid" en minúsculas: se envía un DTO de escritura (D-01).
    public async Task PostAsync(Producto producto)
    {
        using var response = await client.PostAsJsonAsync("api/productos", ProductoRequest.De(producto));
    }
    public async Task PutAsync(Producto producto)
    {
        using var response = await client.PutAsJsonAsync($"api/productos/{producto.ProductoId}", ProductoRequest.De(producto));
    }
    public async Task DeleteAsync(int id)
    {
        using var response = await client.DeleteAsync($"api/productos/{id}");
    }
    public async Task PostAsync(int id, int categoriaid)
    {
        using var response = await client.PostAsJsonAsync($"api/productos/{id}/categoria", new { categoriaid });
    }
    public async Task DeleteAsync(int id, int categoriaid)
    {
        using var response = await client.DeleteAsync($"api/productos/{id}/categoria/{categoriaid}");
    }
}
