using FrontendNet.Models;
namespace FrontendNet.Services;

/// <summary>
/// Contrato de orders (docs/FrontendPlanning.md 5.2). Implementaciones: <see cref="InMemoryOrdersService"/> (simulación)
/// y <see cref="PedidosClientService"/> (definitiva, contra el API propuesto).
/// </summary>
public interface IOrdersService
{
    /// <summary>Convierte el shoppingCart actual en pedido (atómico: si falla, el shoppingCart no se vacía).</summary>
    Task<Order> CrearAsync(NewOrder nuevo);
    /// <summary>User: sus orders. ADMIN: todos.</summary>
    Task<List<OrderSummary>> GetAsync(string? status);
    /// <summary>Un pedido ajeno responde 404 (no 403) para no revelar su existencia.</summary>
    Task<Order?> GetOrderAsync(string id);
    Task ChangeStatusAsync(string id, string status, string? reason = null);
    Task CancelAsync(string id, string reason);
}
