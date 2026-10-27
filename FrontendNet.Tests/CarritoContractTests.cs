using System.Net;
using System.Text.Json;
using FrontendNet.Models;
using FrontendNet.Services;
using FrontendNet.Tests.Support;

namespace FrontendNet.Tests;

/// <summary>
/// Pruebas de contrato (planeacion Fase 7): los MISMOS casos corren contra la simulación en sesión y contra el
/// cliente definitivo (con un API falso que cumple el contrato 5.1), garantizando que el cambio sea transparente.
/// </summary>
public abstract class CarritoContractTests
{
    protected abstract ICarritoService Crea();

    [Fact]
    public async Task Agregar_calcula_subtotal_y_total_con_el_precio_del_servidor()
    {
        var c = Crea();
        var carrito = await c.AgregarAsync(5, 2);

        var item = Assert.Single(carrito.Items);
        Assert.Equal(5, item.ProductoId);
        Assert.Equal("Laptop", item.Titulo);
        Assert.Equal(15999.00m, item.Precio);
        Assert.Equal(2, item.Cantidad);
        Assert.Equal(31998.00m, item.Subtotal);
        Assert.Equal(2, carrito.TotalArticulos);
        Assert.Equal(31998.00m, carrito.Total);
    }

    [Fact]
    public async Task Agregar_dos_productos_suma_el_total()
    {
        var c = Crea();
        await c.AgregarAsync(5, 1);
        var carrito = await c.AgregarAsync(6, 3);

        Assert.Equal(2, carrito.Items.Count);
        Assert.Equal(4, carrito.TotalArticulos);
        Assert.Equal(15999.00m + 3 * 100.50m, carrito.Total);
        Assert.Equal(4, await c.ContarAsync());
    }

    [Fact]
    public async Task Actualizar_cambia_la_cantidad()
    {
        var c = Crea();
        await c.AgregarAsync(5, 1);
        var carrito = await c.ActualizarAsync(5, 4);

        Assert.Equal(4, carrito.Items.Single().Cantidad);
        Assert.Equal(4 * 15999.00m, carrito.Total);
    }

    [Fact]
    public async Task Actualizar_un_item_que_no_esta_en_el_carrito_responde_404()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() => Crea().ActualizarAsync(5, 2));
        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1000)]
    public async Task Agregar_con_cantidad_invalida_responde_400(int cantidad)
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() => Crea().AgregarAsync(5, cantidad));
        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
    }

    [Fact]
    public async Task Agregar_un_producto_inexistente_responde_404()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() => Crea().AgregarAsync(999, 1));
        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task Quitar_elimina_solo_ese_producto()
    {
        var c = Crea();
        await c.AgregarAsync(5, 1);
        await c.AgregarAsync(6, 1);
        await c.QuitarAsync(5);

        var carrito = await c.GetAsync();
        Assert.Equal(6, carrito.Items.Single().ProductoId);
    }

    [Fact]
    public async Task Vaciar_deja_el_carrito_en_cero()
    {
        var c = Crea();
        await c.AgregarAsync(5, 1);
        await c.VaciarAsync();

        var carrito = await c.GetAsync();
        Assert.Empty(carrito.Items);
        Assert.Equal(0, carrito.TotalArticulos);
        Assert.Equal(0m, carrito.Total);
    }

    // ---------- catálogo falso del backend (GET api/productos/:id, precio como string DECIMAL) ----------
    internal static readonly Dictionary<int, (string Titulo, string Precio, int? Archivo)> Catalogo = new()
    {
        [5] = ("Laptop", "15999.00", 3),
        [6] = ("Mouse", "100.50", null),
    };

    internal static HttpResponseMessage RespondeProducto(Peticion p)
    {
        var id = int.Parse(p.RutaYQuery.Split('/').Last());
        return Catalogo.TryGetValue(id, out var prod)
            ? ApiFalso.JsonCrudo(HttpStatusCode.OK,
                $$"""{"productoId":{{id}},"titulo":"{{prod.Titulo}}","descripcion":"d","precio":"{{prod.Precio}}","archivoid":{{(prod.Archivo?.ToString() ?? "null")}},"categorias":[]}""")
            : ApiFalso.Json(HttpStatusCode.NotFound, new { mensaje = "Producto no encontrado." });
    }
}

public class CarritoSesionContractTests : CarritoContractTests
{
    protected override ICarritoService Crea()
    {
        var ctx = new ContextoFalso();
        var productos = new ProductoClientService(ctx.ClienteApi(new ApiFalso(RespondeProducto)));
        return new CarritoSesionService(ctx.Accessor, productos);
    }

    [Fact]
    public async Task La_sesion_guarda_solo_ids_y_cantidades_nunca_precios()
    {
        var ctx = new ContextoFalso();
        var servicio = new CarritoSesionService(ctx.Accessor, new ProductoClientService(ctx.ClienteApi(new ApiFalso(RespondeProducto))));

        await servicio.AgregarAsync(5, 2);

        var guardado = ctx.Sesion.Keys.Select(k => System.Text.Encoding.UTF8.GetString(ctx.Sesion.TryGetValue(k, out var v) ? v! : [])).Single();
        Assert.Equal("{\"5\":2}", guardado);
    }

    [Fact]
    public async Task Cada_usuario_tiene_su_carrito_aunque_compartan_la_sesion_del_navegador()
    {
        var ctx = new ContextoFalso("a@uv.mx");
        var servicio = new CarritoSesionService(ctx.Accessor, new ProductoClientService(ctx.ClienteApi(new ApiFalso(RespondeProducto))));
        await servicio.AgregarAsync(5, 1);

        ctx.CambiaUsuario("b@uv.mx", "Usuario");

        Assert.Empty((await servicio.GetAsync()).Items);
    }

    [Fact]
    public async Task Un_producto_eliminado_se_retira_del_carrito_sin_fallar()
    {
        var ctx = new ContextoFalso();
        var existe = true;
        var api = new ApiFalso(p => existe ? RespondeProducto(p) : ApiFalso.Json(HttpStatusCode.NotFound, new { mensaje = "x" }));
        var servicio = new CarritoSesionService(ctx.Accessor, new ProductoClientService(ctx.ClienteApi(api)));
        await servicio.AgregarAsync(5, 1);

        existe = false;
        var carrito = await servicio.GetAsync();

        Assert.Empty(carrito.Items);
        Assert.Equal(0, await servicio.ContarAsync());
    }

    [Fact]
    public async Task Acumular_no_supera_la_cantidad_maxima()
    {
        var c = Crea();
        await c.AgregarAsync(5, 90);
        var carrito = await c.AgregarAsync(5, 50);
        Assert.Equal(CarritoSesionService.CantidadMaxima, carrito.Items.Single().Cantidad);
    }
}

public class CarritoClientContractTests : CarritoContractTests
{
    /// <summary>API falso que implementa el contrato PROPUESTO 5.1 (el carrito vive en el servidor y se identifica por el JWT).</summary>
    private class CarritoApi
    {
        private readonly Dictionary<int, int> _items = [];

        public HttpResponseMessage Responde(Peticion p)
        {
            var ruta = p.RutaYQuery;
            if (ruta == "/api/carrito")
            {
                if (p.Metodo == HttpMethod.Delete) { _items.Clear(); return new HttpResponseMessage(HttpStatusCode.NoContent); }
                return Carrito(HttpStatusCode.OK);
            }
            if (ruta == "/api/carrito/items" && p.Metodo == HttpMethod.Post)
            {
                using var json = JsonDocument.Parse(p.Cuerpo!);
                var id = json.RootElement.GetProperty("productoid").GetInt32();
                var cantidad = json.RootElement.GetProperty("cantidad").GetInt32();
                if (cantidad is < 1 or > 99) return ApiFalso.Json(HttpStatusCode.BadRequest, new { mensaje = "Cantidad inválida." });
                if (!Catalogo.ContainsKey(id)) return ApiFalso.Json(HttpStatusCode.NotFound, new { mensaje = "Producto no encontrado." });
                _items[id] = Math.Min(99, _items.GetValueOrDefault(id) + cantidad);
                return Carrito(HttpStatusCode.Created);
            }
            if (ruta.StartsWith("/api/carrito/items/"))
            {
                var id = int.Parse(ruta.Split('/').Last());
                if (p.Metodo == HttpMethod.Delete) { _items.Remove(id); return new HttpResponseMessage(HttpStatusCode.NoContent); }
                if (!_items.ContainsKey(id)) return ApiFalso.Json(HttpStatusCode.NotFound, new { mensaje = "El producto no está en el carrito." });
                using var json = JsonDocument.Parse(p.Cuerpo!);
                var cantidad = json.RootElement.GetProperty("cantidad").GetInt32();
                if (cantidad is < 1 or > 99) return ApiFalso.Json(HttpStatusCode.BadRequest, new { mensaje = "Cantidad inválida." });
                _items[id] = cantidad;
                return Carrito(HttpStatusCode.OK);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private HttpResponseMessage Carrito(HttpStatusCode status)
        {
            decimal total = 0;
            var items = new List<string>();
            foreach (var (id, cantidad) in _items)
            {
                var prod = Catalogo[id];
                var precio = decimal.Parse(prod.Precio, System.Globalization.CultureInfo.InvariantCulture);
                var subtotal = precio * cantidad;
                total += subtotal;
                items.Add($$"""{"productoId":{{id}},"titulo":"{{prod.Titulo}}","precio":"{{prod.Precio}}","archivoid":{{(prod.Archivo?.ToString() ?? "null")}},"cantidad":{{cantidad}},"subtotal":"{{subtotal.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}}"}""");
            }
            return ApiFalso.JsonCrudo(status,
                $$"""{"items":[{{string.Join(',', items)}}],"totalArticulos":{{_items.Values.Sum()}},"total":"{{total.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}}"}""");
        }
    }

    protected override ICarritoService Crea()
    {
        var ctx = new ContextoFalso();
        return new CarritoClientService(ctx.ClienteApi(new ApiFalso(new CarritoApi().Responde)));
    }
}
