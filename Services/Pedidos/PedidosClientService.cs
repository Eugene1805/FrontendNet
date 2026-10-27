using FrontendNet.Helpers;
using FrontendNet.Models;
namespace FrontendNet.Services;

/// <summary>Implementación definitiva contra el contrato PROPUESTO api/pedidos (planeacion 5.2). Aún no existe en el backend.</summary>
public class PedidosClientService(HttpClient client) : IPedidosService
{
    public async Task<Pedido> CrearAsync(PedidoNuevo nuevo)
    {
        using var response = await client.PostAsJsonAsync("api/pedidos", nuevo);
        return (await response.Content.ReadFromJsonAsync<Pedido>())!;
    }

    public async Task<List<PedidoResumen>> GetAsync(string? estado)
    {
        var url = string.IsNullOrWhiteSpace(estado) ? "api/pedidos" : $"api/pedidos?estado={Url.Codifica(estado)}";
        return await client.GetFromJsonAsync<List<PedidoResumen>>(url) ?? [];
    }

    public async Task<Pedido?> GetAsync(int id)
        => await client.GetFromJsonAsync<Pedido>($"api/pedidos/{id}");

    public async Task CambiaEstadoAsync(int id, string estado)
    {
        using var response = await client.PutAsJsonAsync($"api/pedidos/{id}/estado", new { estado });
    }

    public async Task CancelarAsync(int id)
    {
        using var response = await client.PostAsync($"api/pedidos/{id}/cancelar", null);
    }
}
