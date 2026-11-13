using FrontendNet.Models;
namespace FrontendNet.Services;

public class ProfileClientService(HttpClient client)
{
    /// <summary>GET api/auth/time returns { remainingTime }.</summary>
    public async Task<string?> GetRemainingTimeAsync()
    {
        var time = await client.GetFromJsonAsync<SessionTime>("api/auth/time");
        return time?.RemainingTime;
    }

    // --- Contratos PROPUESTOS (planeacion 5.3 y 5.5), habilitados con Features:PerfilEditable / Features:LogoutApi ---

    /// <summary>PUT api/profile { nombre } responde 204 (con Set-Authorization con el nuevo givenname).</summary>
    public async Task UpdateNameAsync(string name)
    {
        using var response = await client.PutAsJsonAsync("api/profile", new { name });
    }

    /// <summary>PUT api/profile/password responde 204; 400 por política; 401 si la contraseña actual es incorrecta.</summary>
    public async Task PutPasswordAsync(ChangePassword cambio)
    {
        using var response = await client.PutAsJsonAsync("api/profile/password",
            new { currentPassword = cambio.CurrentPassword, newPassword = cambio.NewPassword });
    }

    /// <summary>POST api/auth/salir responde 204 (revoca el JWT).</summary>
    public async Task RevocaSesionAsync()
    {
        using var response = await client.PostAsync("api/auth/logout", null);
    }
}
