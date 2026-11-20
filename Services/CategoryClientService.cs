using FrontendNet.Models;

namespace FrontendNet.Services;

public class CategoryClientService(HttpClient client)
{
    public async Task<List<Category>?> GetAsync() =>
        await client.GetFromJsonAsync<List<Category>>("api/categories");

    public async Task<Category?> GetAsync(int id) =>
        await client.GetFromJsonAsync<Category>($"api/categories/{id}");

    public async Task PostAsync(Category category)
    {
        using var response = await client.PostAsJsonAsync("api/categories", new { name = category.Name.Trim() });
    }

    public async Task PutAsync(Category category)
    {
        using var response = await client.PutAsJsonAsync(
            $"api/categories/{category.CategoryId}",
            new { name = category.Name.Trim() });
    }

    public async Task DeleteAsync(int id)
    {
        using var response = await client.DeleteAsync($"api/categories/{id}");
    }
}
