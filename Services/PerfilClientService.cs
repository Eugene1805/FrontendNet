using FrontendNet.Models;
namespace FrontendNet.Services;

public class PerfilClientService(HttpClient client)
{
    /// <summary>GET api/auth/tiempo devuelve { tiempoRestante } (D-03).</summary>
    public async Task<string?> ObtenTiempoAsync()
    {
        var tiempo = await client.GetFromJsonAsync<TiempoSesion>("api/auth/tiempo");
        return tiempo?.TiempoRestante;
    }

    // --- Contratos PROPUESTOS (planeacion 5.3 y 5.5), habilitados con Features:PerfilEditable / Features:LogoutApi ---

    /// <summary>PUT api/perfil { nombre } responde 204 (con Set-Authorization con el nuevo givenname).</summary>
    public async Task PutNombreAsync(string nombre)
    {
        using var response = await client.PutAsJsonAsync("api/perfil", new { nombre });
    }

    /// <summary>PUT api/perfil/password responde 204; 400 por política; 401 si la contraseña actual es incorrecta.</summary>
    public async Task PutPasswordAsync(CambioPassword cambio)
    {
        using var response = await client.PutAsJsonAsync("api/perfil/password",
            new { passwordActual = cambio.PasswordActual, passwordNueva = cambio.PasswordNueva });
    }

    /// <summary>POST api/auth/salir responde 204 (revoca el JWT).</summary>
    public async Task RevocaSesionAsync()
    {
        using var response = await client.PostAsync("api/auth/salir", null);
    }
}
