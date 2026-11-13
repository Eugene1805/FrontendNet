using FrontendNet.Models;
namespace FrontendNet.Services;

public class CategoryClientService(HttpClient client)
{
    public async Task<List<Category>?> GetAsync()
    {
        return await client.GetFromJsonAsync<List<Category>>("api/categories");
    }
    public async Task<Category?> GetAsync(int id)
    {
        return await client.GetFromJsonAsync<Category>($"api/categories/{id}");
    }
    public async Task PostAsync(Category categoria)
    {
        using var response = await client.PostAsJsonAsync("api/categories", categoria);
    }

    public async Task PutAsync(Category categoria)
    {
        using var response = await client.PutAsJsonAsync($"api/categories/{categoria.CategoryId}", categoria);
    }
    public async Task DeleteAsync(int id)
    {
        using var response = await client.DeleteAsync($"api/categories/{id}");
    }
}
