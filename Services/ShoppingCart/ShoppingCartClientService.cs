using System.Net;
using FrontendNet.Models;

namespace FrontendNet.Services;

/// <summary>HTTP client for the backend persistent shopping cart API.</summary>
public class ShoppingCartClientService(HttpClient client) : IShoppingCartService
{
    public async Task<ShoppingCart> GetAsync() =>
        await client.GetFromJsonAsync<ShoppingCart>("api/cart") ?? new ShoppingCart();

    public async Task<int> GetCountAsync() =>
        (await GetAsync()).Items.Sum(item => item.Quantity);

    public async Task<ShoppingCart> AddAsync(int productId, int quantity)
    {
        using var response = await client.PostAsJsonAsync("api/cart/items", new { productId, quantity });
        return await response.Content.ReadFromJsonAsync<ShoppingCart>() ?? new ShoppingCart();
    }

    public async Task<ShoppingCart> UpdateAsync(int productId, int quantity)
    {
        using var response = await client.PutAsJsonAsync($"api/cart/items/{productId}", new { quantity });
        return await response.Content.ReadFromJsonAsync<ShoppingCart>() ?? new ShoppingCart();
    }

    public async Task RemoveAsync(int productId)
    {
        using var response = await client.DeleteAsync($"api/cart/items/{productId}");
    }

    public async Task ClearAsync()
    {
        var cart = await GetAsync();
        foreach (var item in cart.Items)
        {
            try
            {
                await RemoveAsync(item.ProductId);
            }
            catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Ignore lines that were already removed concurrently.
            }
        }
    }
}
