using System.Text.Json;
using System.Text.Json.Serialization;
using FrontendNet.Helpers;
using FrontendNet.Models;
namespace FrontendNet.Services;

public class UsersClientService(HttpClient client)
{
    private static readonly JsonSerializerOptions OmiteNulos = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<List<User>?> GetAsync()
    {
        return await client.GetFromJsonAsync<List<User>>("api/users");
    }

    public async Task<User?> GetAsync(string email)
    {
        return await client.GetFromJsonAsync<User>($"api/users/{Url.Encode(email)}");
    }

    public async Task PostAsync(UserPassword usuario)
    {
        using var response = await client.PostAsJsonAsync("api/users", usuario);
    }

    /// <summary>PUT api/users/:email { name, role, password? }.</summary>
    public async Task PutAsync(User usuario)
    {
        var body = new UserRequest { Name = usuario.Name, Role = usuario.Role, Password = usuario.Password };
        using var response = await client.PutAsJsonAsync($"api/users/{Url.Encode(usuario.Email)}", body, OmiteNulos);
    }

    public async Task DeleteAsync(string email)
    {
        using var response = await client.DeleteAsync($"api/users/{Url.Encode(email)}");
    }
}
