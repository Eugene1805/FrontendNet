using System.Net;
using System.Security.Claims;
using FrontendNet.Models;
namespace FrontendNet.Services;

/// <summary>Almacén en memoria compartido por toda la aplicación (se pierde al reiniciar). Solo para la simulación.</summary>
public class PedidosAlmacen
{
    private readonly object _candado = new();
    private readonly List<Pedido> _pedidos = [];
    private int _siguienteId = 1;

    public Pedido Agrega(Func<int, Pedido> crea)
    {
        lock (_candado)
        {
            var pedido = crea(_siguienteId++);
            _pedidos.Add(pedido);
            return pedido;
        }
    }

    public List<Pedido> Lista()
    {
        lock (_candado) return _pedidos.Select(Copia).ToList();
    }

    public Pedido? Busca(int id)
    {
        lock (_candado) return _pedidos.Where(p => p.PedidoId == id).Select(Copia).FirstOrDefault();
    }

    public void Modifica(int id, Action<Pedido> accion)
    {
        lock (_candado) accion(_pedidos.First(p => p.PedidoId == id));
    }

    private static Pedido Copia(Pedido p) => new()
    {
        PedidoId = p.PedidoId, Fecha = p.Fecha, Estado = p.Estado, Usuario = p.Usuario, Total = p.Total,
        DireccionEnvio = p.DireccionEnvio, Items = p.Items.Select(i => new PedidoItem
        {
            ProductoId = i.ProductoId, Titulo = i.Titulo, PrecioUnitario = i.PrecioUnitario,
            Cantidad = i.Cantidad, Subtotal = i.Subtotal
        }).ToList()
    };
}

/// <summary>
/// SIMULACIÓN de pedidos (pago simulado). Reproduce las reglas del contrato propuesto: el usuario sale del JWT/cookie,
/// los precios se copian del carrito (calculado contra el API), un pedido ajeno responde 404 y las transiciones se validan.
/// </summary>
public class PedidosMemoriaService(PedidosAlmacen almacen, ICarritoService carrito, IHttpContextAccessor accessor) : IPedidosService
{
    private ClaimsPrincipal Usuario => accessor.HttpContext?.User ?? throw new InvalidOperationException("No hay un HttpContext activo.");
    private string Email => Usuario.FindFirstValue(ClaimTypes.Email) ?? "";
    private bool EsAdmin => Usuario.IsInRole("Administrador");

    private static ApiException NoEncontrado() => ApiException.De(HttpStatusCode.NotFound, "Pedido no encontrado.");

    private Pedido? Visible(int id)
    {
        var pedido = almacen.Busca(id);
        return pedido != null && (EsAdmin || pedido.Usuario == Email) ? pedido : null;
    }

    public async Task<Pedido> CrearAsync(PedidoNuevo nuevo)
    {
        if (EsAdmin) throw ApiException.De(HttpStatusCode.Forbidden, "El administrador no puede realizar compras.");
        var actual = await carrito.GetAsync();
        if (actual.Items.Count == 0)
            throw ApiException.De(HttpStatusCode.BadRequest, "El carrito está vacío.");

        var pedido = almacen.Agrega(id => new Pedido
        {
            PedidoId = id,
            Fecha = DateTime.UtcNow,
            Estado = EstadoPedido.Pendiente,
            Usuario = Email,
            DireccionEnvio = nuevo.DireccionEnvio,
            Total = actual.Total,
            // Se copian título y precio: el producto puede cambiar o borrarse después.
            Items = actual.Items.Select(i => new PedidoItem
            {
                ProductoId = i.ProductoId, Titulo = i.Titulo, PrecioUnitario = i.Precio,
                Cantidad = i.Cantidad, Subtotal = i.Subtotal
            }).ToList()
        });
        await carrito.VaciarAsync();
        return pedido;
    }

    public Task<List<PedidoResumen>> GetAsync(string? estado)
    {
        IEnumerable<Pedido> lista = almacen.Lista();
        if (!EsAdmin) lista = lista.Where(p => p.Usuario == Email);
        if (!string.IsNullOrWhiteSpace(estado)) lista = lista.Where(p => p.Estado == estado);
        return Task.FromResult(lista.OrderByDescending(p => p.Fecha)
            .Select(p => new PedidoResumen { PedidoId = p.PedidoId, Fecha = p.Fecha, Estado = p.Estado, Usuario = p.Usuario, Total = p.Total })
            .ToList());
    }

    public Task<Pedido?> GetAsync(int id)
        => Task.FromResult(Visible(id) ?? throw NoEncontrado())!;

    public Task CambiaEstadoAsync(int id, string estado)
    {
        if (!EsAdmin) throw ApiException.De(HttpStatusCode.Forbidden, "Solo el administrador puede cambiar el estado.");
        var pedido = almacen.Busca(id) ?? throw NoEncontrado();
        if (!EstadoPedido.Siguientes(pedido.Estado).Contains(estado))
            throw ApiException.De(HttpStatusCode.BadRequest, $"No se puede pasar de '{pedido.Estado}' a '{estado}'.");
        almacen.Modifica(id, p => p.Estado = estado);
        return Task.CompletedTask;
    }

    public Task CancelarAsync(int id)
    {
        var pedido = Visible(id) ?? throw NoEncontrado();
        if (EsAdmin || pedido.Usuario != Email) throw NoEncontrado();
        if (pedido.Estado != EstadoPedido.Pendiente)
            throw ApiException.De(HttpStatusCode.Conflict,
                $"Este pedido ya está en estado «{pedido.Estado}». Solo se pueden cancelar los pedidos pendientes; para otros casos comuníquese con la tienda.");
        almacen.Modifica(id, p => p.Estado = EstadoPedido.Cancelado);
        return Task.CompletedTask;
    }
}
