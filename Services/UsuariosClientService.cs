using System.Text.Json;
using System.Text.Json.Serialization;
using FrontendNet.Helpers;
using FrontendNet.Models;
namespace FrontendNet.Services;

public class UsuariosClientService(HttpClient client)
{
    private static readonly JsonSerializerOptions OmiteNulos = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<List<Usuario>?> GetAsync()
    {
        return await client.GetFromJsonAsync<List<Usuario>>("api/usuarios");
    }

    public async Task<Usuario?> GetAsync(string email)
    {
        return await client.GetFromJsonAsync<Usuario>($"api/usuarios/{Url.Codifica(email)}");
    }

    public async Task PostAsync(UsuarioPwd usuario)
    {
        using var response = await client.PostAsJsonAsync("api/usuarios", usuario);
    }

    /// <summary>PUT api/usuarios/:email { nombre, rol, password? } (D-13).</summary>
    public async Task PutAsync(Usuario usuario)
    {
        var body = new UsuarioRequest { Nombre = usuario.Nombre, Rol = usuario.Rol, Password = usuario.Password };
        using var response = await client.PutAsJsonAsync($"api/usuarios/{Url.Codifica(usuario.Email)}", body, OmiteNulos);
    }

    public async Task DeleteAsync(string email)
    {
        using var response = await client.DeleteAsync($"api/usuarios/{Url.Codifica(email)}");
    }
}
