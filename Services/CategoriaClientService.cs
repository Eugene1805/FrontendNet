using FrontendNet.Models;
namespace FrontendNet.Services;

public class CategoriaClientService(HttpClient client)
{
    public async Task<List<Categoria>?> GetAsync()
    {
        return await client.GetFromJsonAsync<List<Categoria>>("api/categorias");
    }
    public async Task<Categoria?> GetAsync(int id)
    {
        return await client.GetFromJsonAsync<Categoria>($"api/categorias/{id}");
    }
    public async Task PostAsync(Categoria categoria)
    {
        using var response = await client.PostAsJsonAsync("api/categorias", categoria);
    }

    public async Task PutAsync(Categoria categoria)
    {
        using var response = await client.PutAsJsonAsync($"api/categorias/{categoria.CategoriaId}", categoria);
    }
    public async Task DeleteAsync(int id)
    {
        using var response = await client.DeleteAsync($"api/categorias/{id}");
    }
}
