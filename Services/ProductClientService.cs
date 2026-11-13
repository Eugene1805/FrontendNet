using FrontendNet.Helpers;
using FrontendNet.Models;
namespace FrontendNet.Services;

public class ProductClientService(HttpClient client)
{
    public async Task<List<Product>?> GetAsync(string? search)
    {
        var url = string.IsNullOrWhiteSpace(search) ? "api/products" : $"api/products?s={Url.Encode(search)}";
        return await client.GetFromJsonAsync<List<Product>>(url);
    }
    public async Task<Product?> GetAsync(int id)
    {
        return await client.GetFromJsonAsync<Product>($"api/products/{id}");
    }
    // El API lee "fileId" en minúsculas: se envía un DTO de escritura (D-01).
    public async Task PostAsync(Product producto)
    {
        using var response = await client.PostAsJsonAsync("api/products", ProductRequest.Create(producto));
    }
    public async Task PutAsync(Product producto)
    {
        using var response = await client.PutAsJsonAsync($"api/products/{producto.ProductId}", ProductRequest.Create(producto));
    }
    public async Task DeleteAsync(int id)
    {
        using var response = await client.DeleteAsync($"api/products/{id}");
    }
    public async Task PostAsync(int id, int categoryId)
    {
        using var response = await client.PostAsJsonAsync($"api/products/{id}/categories", new { categoryId });
    }
    public async Task DeleteAsync(int id, int categoryId)
    {
        using var response = await client.DeleteAsync($"api/products/{id}/categories/{categoryId}");
    }
}
