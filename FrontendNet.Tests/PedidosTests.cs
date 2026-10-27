using System.Net;
using System.Text.Json;
using FrontendNet.Models;
using FrontendNet.Services;
using FrontendNet.Tests.Support;

namespace FrontendNet.Tests;

public class PedidosMemoriaTests
{
    private readonly PedidosAlmacen _almacen = new();
    private readonly ContextoFalso _ctx = new("ana@uv.mx");
    private readonly ICarritoService _carrito;
    private readonly PedidosMemoriaService _pedidos;

    public PedidosMemoriaTests()
    {
        var productos = new ProductoClientService(_ctx.ClienteApi(new ApiFalso(CarritoContractTests.RespondeProducto)));
        _carrito = new CarritoSesionService(_ctx.Accessor, productos);
        _pedidos = new PedidosMemoriaService(_almacen, _carrito, _ctx.Accessor);
    }

    private PedidosMemoriaService Como(string email, string rol)
    {
        _ctx.CambiaUsuario(email, rol);
        return new PedidosMemoriaService(_almacen, _carrito, _ctx.Accessor);
    }

    private static PedidoNuevo Nuevo() => new() { DireccionEnvio = "Calle 1 #23, Xalapa, Veracruz" };

    [Fact]
    public async Task Crear_copia_titulo_y_precio_vacia_el_carrito_y_arranca_Pendiente()
    {
        await _carrito.AgregarAsync(5, 2);
        await _carrito.AgregarAsync(6, 1);

        var pedido = await _pedidos.CrearAsync(Nuevo());

        Assert.Equal(EstadoPedido.Pendiente, pedido.Estado);
        Assert.Equal("ana@uv.mx", pedido.Usuario);
        Assert.Equal(2 * 15999.00m + 100.50m, pedido.Total);
        Assert.Equal(2, pedido.Items.Count);
        Assert.Equal("Laptop", pedido.Items.First(i => i.ProductoId == 5).Titulo);
        Assert.Equal(15999.00m, pedido.Items.First(i => i.ProductoId == 5).PrecioUnitario);
        Assert.Empty((await _carrito.GetAsync()).Items);
    }

    [Fact]
    public async Task Crear_con_carrito_vacio_responde_400_y_no_crea_pedido()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() => _pedidos.CrearAsync(Nuevo()));
        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Empty(await _pedidos.GetAsync(null));
    }

    [Fact]
    public async Task El_pedido_conserva_precios_aunque_el_producto_cambie_despues()
    {
        var precio = "15999.00";
        var ctx = new ContextoFalso("ana@uv.mx");
        var api = new ApiFalso(_ => ApiFalso.JsonCrudo(HttpStatusCode.OK,
            $$"""{"productoId":5,"titulo":"Laptop","descripcion":"d","precio":"{{precio}}","archivoid":null,"categorias":[]}"""));
        var carrito = new CarritoSesionService(ctx.Accessor, new ProductoClientService(ctx.ClienteApi(api)));
        var pedidos = new PedidosMemoriaService(new PedidosAlmacen(), carrito, ctx.Accessor);
        await carrito.AgregarAsync(5, 1);
        var creado = await pedidos.CrearAsync(Nuevo());

        precio = "1.00";   // el producto cambia de precio después de la compra

        var leido = await pedidos.GetAsync(creado.PedidoId);
        Assert.Equal(15999.00m, leido!.Items.Single().PrecioUnitario);
    }

    [Fact]
    public async Task Un_usuario_solo_ve_sus_pedidos_y_el_administrador_todos()
    {
        await _carrito.AgregarAsync(5, 1);
        await _pedidos.CrearAsync(Nuevo());
        var otro = Como("beto@uv.mx", "Usuario");
        await _carrito.AgregarAsync(6, 1);
        await otro.CrearAsync(Nuevo());

        Assert.Single(await otro.GetAsync(null));
        Assert.Equal(2, (await Como("admin@uv.mx", "Administrador").GetAsync(null)).Count);
    }

    [Fact]
    public async Task Un_pedido_ajeno_responde_404_no_403()
    {
        await _carrito.AgregarAsync(5, 1);
        var pedido = await _pedidos.CrearAsync(Nuevo());

        var otro = Como("beto@uv.mx", "Usuario");
        var ex = await Assert.ThrowsAsync<ApiException>(() => otro.GetAsync(pedido.PedidoId));
        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
        var ex2 = await Assert.ThrowsAsync<ApiException>(() => otro.CancelarAsync(pedido.PedidoId));
        Assert.Equal(HttpStatusCode.NotFound, ex2.StatusCode);
    }

    [Fact]
    public async Task El_administrador_no_puede_comprar()
    {
        var admin = Como("admin@uv.mx", "Administrador");
        var ex = await Assert.ThrowsAsync<ApiException>(() => admin.CrearAsync(Nuevo()));
        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
    }

    [Fact]
    public async Task Cancelar_funciona_solo_en_Pendiente()
    {
        await _carrito.AgregarAsync(5, 1);
        var p1 = await _pedidos.CrearAsync(Nuevo());
        await _pedidos.CancelarAsync(p1.PedidoId);
        Assert.Equal(EstadoPedido.Cancelado, (await _pedidos.GetAsync(p1.PedidoId))!.Estado);

        await _carrito.AgregarAsync(5, 1);
        var p2 = await _pedidos.CrearAsync(Nuevo());
        await Como("admin@uv.mx", "Administrador").CambiaEstadoAsync(p2.PedidoId, EstadoPedido.Pagado);
        var ex = await Assert.ThrowsAsync<ApiException>(() => Como("ana@uv.mx", "Usuario").CancelarAsync(p2.PedidoId));
        Assert.Equal(HttpStatusCode.Conflict, ex.StatusCode);
    }

    [Fact]
    public async Task Solo_el_administrador_cambia_el_estado_y_solo_con_transiciones_validas()
    {
        await _carrito.AgregarAsync(5, 1);
        var pedido = await _pedidos.CrearAsync(Nuevo());

        var exUsuario = await Assert.ThrowsAsync<ApiException>(() => _pedidos.CambiaEstadoAsync(pedido.PedidoId, EstadoPedido.Pagado));
        Assert.Equal(HttpStatusCode.Forbidden, exUsuario.StatusCode);

        var admin = Como("admin@uv.mx", "Administrador");
        var salto = await Assert.ThrowsAsync<ApiException>(() => admin.CambiaEstadoAsync(pedido.PedidoId, EstadoPedido.Entregado));
        Assert.Equal(HttpStatusCode.BadRequest, salto.StatusCode);

        await admin.CambiaEstadoAsync(pedido.PedidoId, EstadoPedido.Pagado);
        await admin.CambiaEstadoAsync(pedido.PedidoId, EstadoPedido.Enviado);
        await admin.CambiaEstadoAsync(pedido.PedidoId, EstadoPedido.Entregado);
        Assert.Equal(EstadoPedido.Entregado, (await admin.GetAsync(pedido.PedidoId))!.Estado);
        await Assert.ThrowsAsync<ApiException>(() => admin.CambiaEstadoAsync(pedido.PedidoId, EstadoPedido.Cancelado));
    }

    [Fact]
    public async Task Filtra_por_estado()
    {
        await _carrito.AgregarAsync(5, 1);
        var p1 = await _pedidos.CrearAsync(Nuevo());
        await _carrito.AgregarAsync(6, 1);
        await _pedidos.CrearAsync(Nuevo());
        await _pedidos.CancelarAsync(p1.PedidoId);

        Assert.Single(await _pedidos.GetAsync(EstadoPedido.Cancelado));
        Assert.Single(await _pedidos.GetAsync(EstadoPedido.Pendiente));
    }

    [Fact]
    public async Task Los_ids_son_consecutivos_y_unicos_bajo_concurrencia()
    {
        var ids = new System.Collections.Concurrent.ConcurrentBag<int>();
        await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => Task.Run(() =>
            ids.Add(_almacen.Agrega(id => new Pedido { PedidoId = id }).PedidoId))));
        Assert.Equal(50, ids.Distinct().Count());
    }
}

public class PedidosClientTests
{
    [Fact]
    public async Task Usa_las_rutas_y_cuerpos_del_contrato_5_2()
    {
        var api = new ApiFalso(p => p.Metodo == HttpMethod.Post && p.RutaYQuery == "/api/pedidos"
            ? ApiFalso.JsonCrudo(HttpStatusCode.Created,
                """{"pedidoId":12,"fecha":"2026-10-22T18:30:00.000Z","estado":"Pendiente","usuario":"p@uv.mx","direccionEnvio":"x","items":[{"productoId":5,"titulo":"Laptop","precioUnitario":"15999.00","cantidad":1,"subtotal":"15999.00"}],"total":"15999.00"}""")
            : p.RutaYQuery.StartsWith("/api/pedidos?") || p.RutaYQuery == "/api/pedidos"
                ? ApiFalso.JsonCrudo(HttpStatusCode.OK, """[{"pedidoId":12,"fecha":"2026-10-22T18:30:00Z","estado":"Pendiente","usuario":"p@uv.mx","total":"15999.00"}]""")
                : new HttpResponseMessage(HttpStatusCode.NoContent));
        var servicio = new PedidosClientService(new ContextoFalso().ClienteApi(api));

        var pedido = await servicio.CrearAsync(new PedidoNuevo { DireccionEnvio = "Calle 1 #23" });
        var lista = await servicio.GetAsync("Pendiente");
        await servicio.CambiaEstadoAsync(12, "Enviado");
        await servicio.CancelarAsync(12);

        Assert.Equal(12, pedido.PedidoId);
        Assert.Equal(15999.00m, pedido.Items.Single().PrecioUnitario);
        Assert.Equal(15999.00m, lista.Single().Total);
        using var cuerpo = JsonDocument.Parse(api.Peticiones[0].Cuerpo!);
        Assert.Equal("simulado", cuerpo.RootElement.GetProperty("metodoPago").GetString());
        Assert.Equal("Calle 1 #23", cuerpo.RootElement.GetProperty("direccionEnvio").GetString());
        Assert.Equal("/api/pedidos?estado=Pendiente", api.Peticiones[1].RutaYQuery);
        Assert.Equal((HttpMethod.Put, "/api/pedidos/12/estado"), (api.Peticiones[2].Metodo, api.Peticiones[2].RutaYQuery));
        Assert.Equal((HttpMethod.Post, "/api/pedidos/12/cancelar"), (api.Peticiones[3].Metodo, api.Peticiones[3].RutaYQuery));
    }
}
