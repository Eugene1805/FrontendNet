using System.Net;
using System.Security.Claims;
using FrontendNet.Models;
namespace FrontendNet.Services;

/// <summary>Almacén en memoria compartido por toda la aplicación (se pierde al reiniciar). Solo para la simulación.</summary>
public class OrdersStore
{
    private readonly object _candado = new();
    private readonly List<Order> _pedidos = [];
    private int _siguienteId = 1;

    public Order Add(Func<int, Order> create)
    {
        lock (_candado)
        {
            var pedido = create(_siguienteId++);
            _pedidos.Add(pedido);
            return pedido;
        }
    }

    public List<Order> Lista()
    {
        lock (_candado) return _pedidos.Select(Copia).ToList();
    }

    public Order? Busca(string id)
    {
        lock (_candado) return _pedidos.Where(p => p.OrderId == id).Select(Copia).FirstOrDefault();
    }

    public void Modifica(string id, Action<Order> action)
    {
        lock (_candado) action(_pedidos.First(p => p.OrderId == id));
    }

    private static Order Copia(Order p) => new()
    {
        OrderId = p.OrderId, Date = p.Date, Status = p.Status, BuyerId = p.BuyerId, Total = p.Total,
        DeliveryAddressSnapshot = p.DeliveryAddressSnapshot, Items = p.Items.Select(i => new OrderItem
        {
            ProductId = i.ProductId, Title = i.Title, UnitPrice = i.UnitPrice,
            Quantity = i.Quantity, Subtotal = i.Subtotal
        }).ToList()
    };
}

/// <summary>
/// SIMULACIÓN de orders (pago simulado). Reproduce las reglas del contrato propuesto: el usuario sale del JWT/cookie,
/// los precios se copian del shoppingCart (calculado contra el API), un pedido ajeno responde 404 y las transiciones se validan.
/// </summary>
public class InMemoryOrdersService(OrdersStore almacen, IShoppingCartService shoppingCart, IHttpContextAccessor accessor) : IOrdersService
{
    private ClaimsPrincipal User => accessor.HttpContext?.User ?? throw new InvalidOperationException("No hay un HttpContext activo.");
    private string Email => User.FindFirstValue(ClaimTypes.Email) ?? "";
    private bool IsAdmin => User.IsInRole("ADMIN");

    private static ApiException NoEncontrado() => ApiException.Create(HttpStatusCode.NotFound, "Pedido no encontrado.");

    private Order? Visible(string id)
    {
        var pedido = almacen.Busca(id);
        return pedido != null && (IsAdmin || pedido.BuyerId == Email) ? pedido : null;
    }

    public async Task<Order> CrearAsync(NewOrder nuevo)
    {
        if (IsAdmin) throw ApiException.Create(HttpStatusCode.Forbidden, "El administrador no puede realizar compras.");
        var actual = await shoppingCart.GetAsync();
        if (actual.Items.Count == 0)
            throw ApiException.Create(HttpStatusCode.BadRequest, "El carrito está vacío.");

        var pedido = almacen.Add(id => new Order
        {
            OrderId = id.ToString(),
            Date = DateTime.UtcNow,
            Status = OrderStatus.Pending,
            BuyerId = Email,
            DeliveryAddressSnapshot = nuevo.AddressId,
            Total = actual.Subtotal,
            // Se copian título y precio: el producto puede cambiar o borrarse después.
            Items = actual.Items.Select(i => new OrderItem
            {
                ProductId = i.ProductId, Title = i.Title,                 UnitPrice = i.UnitPrice ?? "0.00",
                Quantity = i.Quantity, Subtotal = i.Subtotal
            }).ToList()
        });
        await shoppingCart.VaciarAsync();
        return pedido;
    }

    public Task<List<OrderSummary>> GetAsync(string? estado)
    {
        IEnumerable<Order> items = almacen.Lista();
        if (!IsAdmin) items = items.Where(p => p.BuyerId == Email);
        if (!string.IsNullOrWhiteSpace(estado)) items = items.Where(p => p.Status == estado);
        return Task.FromResult(items.OrderByDescending(p => p.Date)
            .Select(p => new OrderSummary { OrderId = p.OrderId, Date = p.Date, Status = p.Status, BuyerId = p.BuyerId, Total = p.Total })
            .ToList());
    }

    public Task<Order?> GetOrderAsync(string id)
        => Task.FromResult(Visible(id) ?? throw NoEncontrado())!;

    public Task ChangeStatusAsync(string id, string status, string? reason = null)
    {
        if (!IsAdmin) throw ApiException.Create(HttpStatusCode.Forbidden, "Solo el administrador puede cambiar el estado.");
        var pedido = almacen.Busca(id) ?? throw NoEncontrado();
        if (!OrderStatus.GetNextStatuses(pedido.Status).Contains(status))
            throw ApiException.Create(HttpStatusCode.BadRequest, $"No se puede pasar de '{pedido.Status}' a '{status}'.");
        almacen.Modifica(id, p => p.Status = status);
        return Task.CompletedTask;
    }

    public Task CancelAsync(string id, string reason)
    {
        var pedido = Visible(id) ?? throw NoEncontrado();
        if (IsAdmin || pedido.BuyerId != Email) throw NoEncontrado();
        if (pedido.Status != OrderStatus.Pending)
            throw ApiException.Create(HttpStatusCode.Conflict,
                $"Este pedido ya está en estado «{pedido.Status}». Solo se pueden cancelar los pedidos pendientes; para otros casos comuníquese con la tienda.");
        almacen.Modifica(id, p => p.Status = OrderStatus.Canceled);
        return Task.CompletedTask;
    }
}
