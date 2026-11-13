using FrontendNet.Models;
namespace FrontendNet.Services;

/// <summary>Client for the persistent cart API.</summary>
public class ShoppingCartClientService(HttpClient client) : IShoppingCartService
{
    public async Task<ShoppingCart> GetAsync()
        => await client.GetFromJsonAsync<ShoppingCart>("api/cart") ?? new ShoppingCart();

    public async Task<int> ContarAsync() => (await GetAsync()).Items.Sum(item => item.Quantity);

    public async Task<ShoppingCart> AgregarAsync(int productoId, int cantidad)
    {
        using var response = await client.PostAsJsonAsync("api/cart/items", new { productId = productoId, quantity = cantidad });
        return await response.Content.ReadFromJsonAsync<ShoppingCart>() ?? new ShoppingCart();
    }

    public async Task<ShoppingCart> ActualizarAsync(int productoId, int cantidad)
    {
        using var response = await client.PutAsJsonAsync($"api/cart/items/{productoId}", new { quantity = cantidad });
        return await response.Content.ReadFromJsonAsync<ShoppingCart>() ?? new ShoppingCart();
    }

    public async Task QuitarAsync(int productoId)
    {
        using var response = await client.DeleteAsync($"api/cart/items/{productoId}");
    }

    public async Task VaciarAsync()
    {
        using var response = await client.DeleteAsync("api/cart");
    }
}
