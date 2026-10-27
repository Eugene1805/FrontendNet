using FrontendNet.Models;
namespace FrontendNet.Services;

/// <summary>
/// Contrato de pedidos (docs/planeacion_frontend.md 5.2). Implementaciones: <see cref="PedidosMemoriaService"/> (simulación)
/// y <see cref="PedidosClientService"/> (definitiva, contra el API propuesto).
/// </summary>
public interface IPedidosService
{
    /// <summary>Convierte el carrito actual en pedido (atómico: si falla, el carrito no se vacía).</summary>
    Task<Pedido> CrearAsync(PedidoNuevo nuevo);
    /// <summary>Usuario: sus pedidos. Administrador: todos.</summary>
    Task<List<PedidoResumen>> GetAsync(string? estado);
    /// <summary>Un pedido ajeno responde 404 (no 403) para no revelar su existencia.</summary>
    Task<Pedido?> GetAsync(int id);
    Task CambiaEstadoAsync(int id, string estado);
    Task CancelarAsync(int id);
}
