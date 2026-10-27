using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using FrontendNet.Tests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace FrontendNet.Tests;

/// <summary>API falso con los JSON reales del backend para ejercitar el frontend completo (pipeline MVC real).</summary>
public class ApiIntegracion
{
    public List<Peticion> Peticiones { get; } = [];
    public bool CaidoGlobal { get; set; }

    public HttpResponseMessage Responde(Peticion p)
    {
        Peticiones.Add(p);
        if (CaidoGlobal) throw new HttpRequestException("refused");
        var ruta = p.RutaYQuery;
        if (p.Metodo == HttpMethod.Post && ruta == "/api/auth")
        {
            using var json = JsonDocument.Parse(p.Cuerpo!);
            var email = json.RootElement.GetProperty("email").GetString()!;
            var password = json.RootElement.GetProperty("password").GetString();
            if (password != "patito") return ApiFalso.Json(HttpStatusCode.Unauthorized, new { mensaje = "Credenciales inválidas." });
            var rol = email.StartsWith("admin") ? "Administrador" : "Usuario";
            return ApiFalso.Json(HttpStatusCode.OK, new { email, nombre = "Persona " + rol, rol, jwt = JwtFalso.Crea(DateTimeOffset.UtcNow.AddMinutes(20)) });
        }
        if (p.Metodo == HttpMethod.Put && ruta == "/api/perfil") return new HttpResponseMessage(HttpStatusCode.NoContent);
        if (p.Metodo == HttpMethod.Put && ruta == "/api/perfil/password")
        {
            using var json = JsonDocument.Parse(p.Cuerpo!);
            return json.RootElement.GetProperty("passwordActual").GetString() == "patito"
                ? new HttpResponseMessage(HttpStatusCode.NoContent)
                : ApiFalso.Json(HttpStatusCode.Unauthorized, new { mensaje = "Contraseña actual incorrecta." });
        }
        if (ruta == "/api/auth/tiempo") return ApiFalso.JsonCrudo(HttpStatusCode.OK, """{"tiempoRestante":"00:12:34"}""");
        if (ruta == "/api/categorias") return ApiFalso.JsonCrudo(HttpStatusCode.OK, """[{"categoriaId":1,"nombre":"Tecnología","protegida":false},{"categoriaId":2,"nombre":"Hogar","protegida":false}]""");
        if (ruta.StartsWith("/api/productos?") || ruta == "/api/productos")
        {
            var todos = """
                [{"productoId":5,"titulo":"Laptop","descripcion":"d","precio":"15999.00","archivoid":3,"categorias":[{"categoriaId":1,"nombre":"Tecnología","protegida":false}]},
                 {"productoId":6,"titulo":"Sartén","descripcion":"d","precio":"100.50","archivoid":null,"categorias":[{"categoriaId":2,"nombre":"Hogar","protegida":false}]}]
                """;
            return ApiFalso.JsonCrudo(HttpStatusCode.OK, todos);
        }
        if (ruta.StartsWith("/api/productos/")) return CarritoContractTests.RespondeProducto(p);
        if (ruta == "/api/archivos/3")
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0]) { Headers = { ContentType = new("image/jpeg") } } };
        if (ruta == "/api/archivos/4")
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<script>alert(1)</script>", System.Text.Encoding.UTF8, "text/html") };
        if (ruta == "/api/archivos/99") return ApiFalso.Json(HttpStatusCode.NotFound, new { mensaje = "Archivo no encontrado." });
        if (ruta == "/api/archivos") return ApiFalso.JsonCrudo(HttpStatusCode.OK, """[{"archivoId":3,"mime":"image/jpeg","indb":true,"nombre":"a.jpg","size":1234}]""");
        if (ruta == "/api/archivos/3/detalle") return ApiFalso.JsonCrudo(HttpStatusCode.OK, """{"archivoId":3,"mime":"image/jpeg","indb":true,"nombre":"a.jpg","size":1234}""");
        if (ruta == "/api/bitacora") return ApiFalso.JsonCrudo(HttpStatusCode.OK, """[{"bitacoraId":1,"accion":"usuarios.crear","elementoid":"a@uv.mx","ip":"::1","usuario":"admin@uv.mx","fecha":"2026-10-22T18:30:00.000Z"}]""");
        if (ruta == "/api/roles") return ApiFalso.JsonCrudo(HttpStatusCode.OK, """[{"id":"r1","nombre":"Administrador"},{"id":"r2","nombre":"Usuario"}]""");
        if (p.Metodo == HttpMethod.Get && ruta == "/api/categorias/1") return ApiFalso.JsonCrudo(HttpStatusCode.OK, """{"categoriaId":1,"nombre":"Tecnología","protegida":false}""");
        if (p.Metodo == HttpMethod.Delete && ruta == "/api/categorias/1") return ApiFalso.Json(HttpStatusCode.BadRequest, new { mensaje = "La categoría está protegida." });
        if (p.Metodo == HttpMethod.Put && ruta == "/api/productos/5") return new HttpResponseMessage(HttpStatusCode.NoContent);
        if (p.Metodo == HttpMethod.Post && ruta == "/api/usuarios") return ApiFalso.Json(HttpStatusCode.Conflict, new { mensaje = "El correo electrónico ya se encuentra registrado." });
        if (p.Metodo == HttpMethod.Delete && ruta == "/api/usuarios/ana%40uv.mx") return ApiFalso.Json(HttpStatusCode.Forbidden, new { mensaje = "Acceso prohibido: No se pueden eliminar usuarios protegidos del sistema." });
        if (ruta == "/api/usuarios/ana%40uv.mx") return ApiFalso.JsonCrudo(HttpStatusCode.OK, """{"id":"u2","email":"ana@uv.mx","nombre":"Ana","protegido":false,"rol":"Usuario"}""");
        if (ruta == "/api/usuarios")
            return ApiFalso.JsonCrudo(HttpStatusCode.OK, """[{"id":"u1","email":"admin@uv.mx","nombre":"Admin","protegido":true,"rol":"Administrador"},{"id":"u2","email":"ana@uv.mx","nombre":"Ana","protegido":false,"rol":"Usuario"}]""");
        return ApiFalso.Json(HttpStatusCode.NotFound, new { mensaje = "Ruta no encontrada." });
    }
}

public class AppFactory : WebApplicationFactory<Program>
{
    public ApiIntegracion Api { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        // UseSetting (no ConfigureAppConfiguration): Program.cs lee la configuración durante el arranque.
        builder.UseSetting("UrlWebAPI", "http://api.test");
        builder.UseSetting("Features:CompraSimulada", "true");
        builder.UseSetting("Features:PerfilEditable", "false");
        builder.ConfigureTestServices(servicios =>
        {
            var api = new ApiFalso(Api.Responde);
            servicios.ConfigureAll<HttpClientFactoryOptions>(o => o.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = api));
        });
    }
}

public class IntegrationTests(AppFactory fabrica) : IClassFixture<AppFactory>
{
    private HttpClient Cliente() => fabrica.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static string Token(string html)
    {
        var m = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(m.Success, "La página no contiene token antiforgery.");
        return m.Groups[1].Value;
    }

    /// <summary>Razor codifica en HTML los caracteres no ASCII (á → &#xE1;): se decodifica para comparar texto.</summary>
    private static async Task<string> Texto(HttpResponseMessage r) => WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync());

    private static async Task<string> Pagina(HttpClient c, string ruta)
    {
        var r = await c.GetAsync(ruta);
        var cuerpo = await Texto(r);
        Assert.True(r.StatusCode == HttpStatusCode.OK, $"{ruta} respondió {(int)r.StatusCode}: {cuerpo[..Math.Min(cuerpo.Length, 600)]}");
        return cuerpo;
    }

    private static async Task<HttpResponseMessage> Post(HttpClient c, string ruta, string paginaConToken, Dictionary<string, string> datos)
    {
        datos["__RequestVerificationToken"] = Token(paginaConToken);
        return await c.PostAsync(ruta, new FormUrlEncodedContent(datos));
    }

    private async Task<HttpClient> ConSesion(string email)
    {
        var c = Cliente();
        var login = await Pagina(c, "/Auth");
        var r = await Post(c, "/Auth", login, new() { ["Email"] = email, ["Password"] = "patito" });
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        return c;
    }

    // ---------- seguridad del propio frontend (Fase 6) ----------

    [Fact]
    public async Task Todas_las_respuestas_llevan_cabeceras_de_seguridad()
    {
        var r = await Cliente().GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("script-src 'self' https://cdnjs.cloudflare.com", r.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("nosniff", r.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", r.Headers.GetValues("X-Frame-Options").Single());
        Assert.True(r.Headers.Contains("Referrer-Policy"));
    }

    [Fact]
    public async Task Las_paginas_cargan_recursos_externos_con_SRI()
    {
        var html = await Cliente().GetStringAsync("/Auth");   // sin decodificar: se revisan las etiquetas tal cual
        foreach (var linea in Regex.Matches(html, "<(?:script|link)[^>]*(?:cdnjs|jsdelivr)[^>]*>").Select(m => m.Value))
            Assert.Contains("integrity=\"sha384-", linea);
        Assert.DoesNotContain("<script>", html.Replace("<script src", ""));   // sin scripts en línea
    }

    [Fact]
    public async Task Un_POST_sin_token_antiforgery_se_rechaza_con_400()
    {
        var c = Cliente();
        var r = await c.PostAsync("/Auth", new FormUrlEncodedContent(new Dictionary<string, string> { ["Email"] = "a@uv.mx", ["Password"] = "x" }));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Los_POST_de_negocio_tambien_exigen_antiforgery()
    {
        var c = await ConSesion("ana@uv.mx");
        var r = await c.PostAsync("/Carrito/Agregar", new FormUrlEncodedContent(new Dictionary<string, string> { ["productoid"] = "5", ["cantidad"] = "1" }));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Las_cookies_de_sesion_son_HttpOnly_y_SameSite()
    {
        var c = Cliente();
        var login = await Pagina(c, "/Auth");
        var r = await Post(c, "/Auth", login, new() { ["Email"] = "ana@uv.mx", ["Password"] = "patito" });
        var cookie = r.Headers.GetValues("Set-Cookie").Single(h => h.StartsWith(".frontendnet="));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rutas_protegidas_sin_sesion_redirigen_al_login()
    {
        var r = await Cliente().GetAsync("/Carrito");
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal("/Auth", r.Headers.Location!.AbsolutePath);
    }

    [Fact]
    public async Task Un_Usuario_no_puede_entrar_al_panel_de_administrador()
    {
        var c = await ConSesion("ana@uv.mx");
        var r = await c.GetAsync("/Usuarios");
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains("AccessDenied", r.Headers.Location!.OriginalString);
    }

    // ---------- login ----------

    [Fact]
    public async Task Login_con_credenciales_invalidas_muestra_el_mensaje_sin_iniciar_sesion()
    {
        var c = Cliente();
        var login = await Pagina(c, "/Auth");
        var r = await Post(c, "/Auth", login, new() { ["Email"] = "ana@uv.mx", ["Password"] = "mal" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("Credenciales inválidas", await Texto(r));
    }

    [Fact]
    public async Task Login_con_API_caido_muestra_un_mensaje_amable()
    {
        var c = Cliente();
        var login = await Pagina(c, "/Auth");
        fabrica.Api.CaidoGlobal = true;
        try
        {
            var r = await Post(c, "/Auth", login, new() { ["Email"] = "ana@uv.mx", ["Password"] = "patito" });
            var html = await Texto(r);
            Assert.Contains("No fue posible comunicarse con el servidor", html);
            Assert.DoesNotContain("refused", html);
        }
        finally { fabrica.Api.CaidoGlobal = false; }
    }

    // ---------- imágenes (proxy, DD-05) ----------

    [Fact]
    public async Task El_proxy_de_imagenes_reenvia_imagenes_con_nosniff()
    {
        var r = await Cliente().GetAsync("/api/archivos/3");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("image/jpeg", r.Content.Headers.ContentType!.MediaType);
        Assert.Equal("nosniff", r.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal([0xFF, 0xD8, 0xFF, 0xE0], await r.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task El_proxy_rechaza_contenido_que_no_sea_imagen()
        => Assert.Equal(HttpStatusCode.NotFound, (await Cliente().GetAsync("/api/archivos/4")).StatusCode);

    [Fact]
    public async Task El_proxy_responde_404_si_el_archivo_no_existe()
        => Assert.Equal(HttpStatusCode.NotFound, (await Cliente().GetAsync("/api/archivos/99")).StatusCode);

    // ---------- flujo de compra de punta a punta (simulación) ----------

    [Fact]
    public async Task Flujo_completo_comprar_carrito_checkout_y_gestion_del_administrador()
    {
        var usuario = await ConSesion("ana@uv.mx");

        // Catálogo: tarjetas con botón POST (no href="#"), banner de simulación y filtro por categoría.
        var catalogo = await Pagina(usuario, "/Comprar");
        Assert.Contains("Laptop", catalogo);
        Assert.Contains("Modo simulación", catalogo);
        Assert.DoesNotContain("href=\"#\"", catalogo);
        var soloHogar = await Pagina(usuario, "/Comprar?categoriaid=2");
        Assert.Contains("Sartén", soloHogar);
        Assert.DoesNotContain("Laptop", soloHogar);
        var porPrecio = await Pagina(usuario, "/Comprar?orden=precio_desc");
        Assert.True(porPrecio.IndexOf("Laptop", StringComparison.Ordinal) < porPrecio.IndexOf("Sartén", StringComparison.Ordinal));

        // Agregar al carrito (POST + antiforgery).
        var detalle = await Pagina(usuario, "/Comprar/Detalle/5");
        var agrega = await Post(usuario, "/Carrito/Agregar", detalle, new() { ["productoid"] = "5", ["cantidad"] = "2" });
        Assert.Equal(HttpStatusCode.Redirect, agrega.StatusCode);
        var carrito = await Pagina(usuario, "/Carrito");
        Assert.Contains("Laptop", carrito);
        Assert.Contains("$31,998.00", carrito);

        // Checkout con dirección demasiado corta → error de validación y no se crea el pedido.
        var checkout = await Pagina(usuario, "/Pedidos/Checkout");
        var corto = await Post(usuario, "/Pedidos/Checkout", checkout, new() { ["DireccionEnvio"] = "corta" });
        Assert.Equal(HttpStatusCode.OK, corto.StatusCode);
        Assert.Contains("entre 10 y 300", await corto.Content.ReadAsStringAsync());

        // Checkout correcto → redirige al detalle del pedido, en estado Pendiente.
        var crea = await Post(usuario, "/Pedidos/Checkout", checkout, new() { ["DireccionEnvio"] = "Calle 1 #23, Xalapa, Veracruz", ["MetodoPago"] = "tarjeta-real" });
        Assert.Equal(HttpStatusCode.Redirect, crea.StatusCode);
        var pedido = await Pagina(usuario, crea.Headers.Location!.OriginalString);
        Assert.Contains("Pendiente", pedido);
        Assert.Contains("Laptop", pedido);
        Assert.Contains("Cancelar pedido", pedido);
        // El carrito quedó vacío.
        Assert.Contains("Su carrito está vacío", await Pagina(usuario, "/Carrito"));

        // El administrador ve el pedido y puede avanzar su estado.
        var admin = await ConSesion("admin@uv.mx");
        var lista = await Pagina(admin, "/Pedidos");
        Assert.Contains("ana@uv.mx", lista);
        var detalleAdmin = await Pagina(admin, crea.Headers.Location!.OriginalString);
        Assert.Contains("Marcar como Pagado", detalleAdmin);
        var id = Regex.Match(crea.Headers.Location!.OriginalString, @"/(\d+)$").Groups[1].Value;
        var paga = await Post(admin, $"/Pedidos/Estado/{id}", detalleAdmin, new() { ["estado"] = "Pagado" });
        Assert.Equal(HttpStatusCode.Redirect, paga.StatusCode);
        Assert.Contains("Marcar como Enviado", await Pagina(admin, $"/Pedidos/Detalle/{id}"));

        // El usuario ya no puede cancelar (pedido Pagado) y un administrador no compra.
        var recargado = await Pagina(usuario, $"/Pedidos/Detalle/{id}");
        Assert.DoesNotContain("Cancelar pedido", recargado);
        var adminCompra = await admin.GetAsync("/Pedidos/Checkout");
        Assert.Equal(HttpStatusCode.Redirect, adminCompra.StatusCode);
        Assert.Contains("AccessDenied", adminCompra.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Un_usuario_no_puede_ver_el_pedido_de_otro()
    {
        var ana = await ConSesion("ana@uv.mx");
        var detalle = await Pagina(ana, "/Comprar/Detalle/6");
        await Post(ana, "/Carrito/Agregar", detalle, new() { ["productoid"] = "6", ["cantidad"] = "1" });
        var checkout = await Pagina(ana, "/Pedidos/Checkout");
        var crea = await Post(ana, "/Pedidos/Checkout", checkout, new() { ["DireccionEnvio"] = "Calle 2 #45, Xalapa, Veracruz" });

        var beto = await ConSesion("beto@uv.mx");
        var r = await beto.GetAsync(crea.Headers.Location!.OriginalString);

        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }

    [Fact]
    public async Task Detalle_de_producto_inexistente_muestra_404_amigable_no_una_excepcion()
    {
        var c = await ConSesion("ana@uv.mx");
        var r = await c.GetAsync("/Comprar/Detalle/999");
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        Assert.Contains("no existe", await Texto(r));
    }

    [Fact]
    public async Task Con_el_API_caido_las_pantallas_muestran_error_amigable_y_no_la_pagina_de_excepcion()
    {
        var c = await ConSesion("ana@uv.mx");
        fabrica.Api.CaidoGlobal = true;
        try
        {
            var r = await c.GetAsync("/Comprar");
            var html = await Texto(r);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode);
            Assert.Contains("No fue posible comunicarse con el servidor", html);
            Assert.DoesNotContain("refused", html);
        }
        finally { fabrica.Api.CaidoGlobal = false; }
    }

    // ---------- perfil (D-03, D-11) y usuarios (D-05) ----------

    [Fact]
    public async Task Perfil_muestra_el_tiempo_formateado_y_nunca_el_jwt()
    {
        var c = await ConSesion("ana@uv.mx");
        var html = await Pagina(c, "/Perfil");
        Assert.Contains("00:12:34", html);
        Assert.DoesNotContain("tiempoRestante", html);
        Assert.DoesNotContain("eyJ", html);               // prefijo de todo JWT en base64url
        Assert.Contains("ServidorFeiJWT", html);
    }

    [Fact]
    public async Task Perfil_editable_esta_apagado_por_defecto()
    {
        var c = await ConSesion("ana@uv.mx");
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/Perfil/CambiarPassword")).StatusCode);
        Assert.DoesNotContain("Cambiar contraseña", await Pagina(c, "/Perfil"));
    }

    [Fact]
    public async Task Usuarios_protegidos_no_ofrecen_eliminar()
    {
        var c = await ConSesion("admin@uv.mx");
        var html = await Pagina(c, "/Usuarios");
        Assert.Single(Regex.Matches(html, "Eliminar</a>"));   // solo ana@uv.mx
        Assert.Contains("bi-shield-lock", html);
    }

    // ---------- panel de administrador: todas las pantallas cargan con los JSON reales ----------

    [Theory]
    [InlineData("/Productos")]
    [InlineData("/Productos?s=lap%26top&pagina=1")]
    [InlineData("/Productos/Crear")]
    [InlineData("/Productos/Detalle/5")]
    [InlineData("/Productos/Editar/5")]
    [InlineData("/Productos/Eliminar/5")]
    [InlineData("/Productos/Categorias/5")]
    [InlineData("/Productos/CategoriasAgregar/5")]
    [InlineData("/Categorias")]
    [InlineData("/Categorias/Crear")]
    [InlineData("/Categorias/Detalle/1")]
    [InlineData("/Categorias/Editar/1")]
    [InlineData("/Categorias/Eliminar/1")]
    [InlineData("/Archivos")]
    [InlineData("/Archivos/Crear")]
    [InlineData("/Archivos/Detalle/3")]
    [InlineData("/Archivos/Editar/3")]
    [InlineData("/Archivos/Eliminar/3")]
    [InlineData("/Usuarios")]
    [InlineData("/Usuarios/Crear")]
    [InlineData("/Usuarios/Detalle/ana@uv.mx")]
    [InlineData("/Usuarios/Editar/ana@uv.mx")]
    [InlineData("/Usuarios/Eliminar/ana@uv.mx")]
    [InlineData("/Bitacora")]
    [InlineData("/Pedidos")]
    public async Task Pantallas_del_administrador_cargan(string ruta)
    {
        var admin = await ConSesion("admin@uv.mx");
        await Pagina(admin, ruta);
    }

    [Fact]
    public async Task D02_Detalle_de_archivos_ya_no_truena()
    {
        var admin = await ConSesion("admin@uv.mx");
        Assert.Contains("a.jpg", await Pagina(admin, "/Archivos/Detalle/3"));
        Assert.Contains(fabrica.Api.Peticiones, p => p.RutaYQuery == "/api/archivos/3/detalle");
    }

    [Fact]
    public async Task D01_Editar_producto_envia_la_portada_en_archivoid()
    {
        var admin = await ConSesion("admin@uv.mx");
        var form = await Pagina(admin, "/Productos/Editar/5");
        fabrica.Api.Peticiones.Clear();
        var r = await Post(admin, "/Productos/Editar/5", form, new()
        {
            ["ProductoId"] = "5", ["Titulo"] = "Laptop", ["Descripcion"] = "d", ["Precio"] = "15999.00", ["ArchivoId"] = "3"
        });
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        var put = fabrica.Api.Peticiones.Single(p => p.Metodo == HttpMethod.Put);
        using var json = JsonDocument.Parse(put.Cuerpo!);
        Assert.Equal(3, json.RootElement.GetProperty("archivoid").GetInt32());
    }

    [Fact]
    public async Task D06_Eliminar_categoria_muestra_el_mensaje_real_del_API()
    {
        var admin = await ConSesion("admin@uv.mx");
        var form = await Pagina(admin, "/Categorias/Eliminar/1");
        Assert.Contains("se quitará de todos ellos", form);
        var r = await Post(admin, "/Categorias/Eliminar/1", form, new() { ["id"] = "1" });
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        var vuelta = await Pagina(admin, r.Headers.Location!.OriginalString);
        Assert.Contains("La categoría está protegida.", vuelta);
        Assert.DoesNotContain("tiene productos asociados", vuelta);
    }

    [Fact]
    public async Task Crear_usuario_duplicado_informa_el_409()
    {
        var admin = await ConSesion("admin@uv.mx");
        var form = await Pagina(admin, "/Usuarios/Crear");
        var r = await Post(admin, "/Usuarios/Crear", form, new() { ["Email"] = "ana@uv.mx", ["Password"] = "patito", ["Nombre"] = "Ana", ["Rol"] = "Usuario" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("ya se encuentra registrado", await Texto(r));
    }

    [Fact]
    public async Task Eliminar_usuario_protegido_muestra_el_mensaje_del_403_no_AccessDenied()
    {
        var admin = await ConSesion("admin@uv.mx");
        var form = await Pagina(admin, "/Usuarios/Eliminar/ana@uv.mx");
        var r = await Post(admin, "/Usuarios/Eliminar/ana@uv.mx", form, new() { ["id"] = "ana@uv.mx" });
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.DoesNotContain("AccessDenied", r.Headers.Location!.OriginalString);
        Assert.Contains("usuarios protegidos", await Pagina(admin, r.Headers.Location!.OriginalString));
    }

    [Fact]
    public async Task El_administrador_no_puede_eliminarse_a_si_mismo()
    {
        var admin = await ConSesion("admin@uv.mx");
        var form = await Pagina(admin, "/Usuarios/Eliminar/ana@uv.mx");
        var r = await Post(admin, "/Usuarios/Eliminar/admin@uv.mx", form, new() { ["id"] = "admin@uv.mx" });
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);   // Forbid → AccessDenied
        Assert.Contains("AccessDenied", r.Headers.Location!.OriginalString);
    }

    // ---------- banderas de configuración (Features) ----------

    private HttpClient ClienteCon(Dictionary<string, string?> config) =>
        fabrica.WithWebHostBuilder(b => { foreach (var (clave, valor) in config) b.UseSetting(clave, valor); })
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private async Task<HttpClient> Entra(HttpClient c, string email)
    {
        var login = await Pagina(c, "/Auth");
        Assert.Equal(HttpStatusCode.Redirect, (await Post(c, "/Auth", login, new() { ["Email"] = email, ["Password"] = "patito" })).StatusCode);
        return c;
    }

    [Fact]
    public async Task Sin_CompraSimulada_se_usan_los_clientes_del_API_y_su_ausencia_no_causa_500()
    {
        var c = await Entra(ClienteCon(new() { ["Features:CompraSimulada"] = "false" }), "ana@uv.mx");

        var r = await c.GetAsync("/Carrito");   // el API falso responde 404 para /api/carrito (aún no existe)

        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        Assert.DoesNotContain("Modo simulación", await Pagina(c, "/Comprar"));
        Assert.Contains(fabrica.Api.Peticiones, p => p.RutaYQuery == "/api/carrito");
    }

    [Fact]
    public async Task Con_PerfilEditable_se_puede_cambiar_nombre_y_contrasena()
    {
        var c = await Entra(ClienteCon(new() { ["Features:PerfilEditable"] = "true" }), "ana@uv.mx");

        var perfil = await Pagina(c, "/Perfil");
        Assert.Contains("Cambiar contraseña", perfil);

        var editar = await Pagina(c, "/Perfil/Editar");
        var r = await Post(c, "/Perfil/Editar", editar, new() { ["Nombre"] = "Nombre Nuevo" });
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains("Nombre Nuevo", await Pagina(c, "/Perfil"));     // el claim Name de la cookie se actualizó

        var form = await Pagina(c, "/Perfil/CambiarPassword");
        var mal = await Post(c, "/Perfil/CambiarPassword", form, new() { ["PasswordActual"] = "incorrecta", ["PasswordNueva"] = "nueva123", ["Confirmacion"] = "nueva123" });
        Assert.Equal(HttpStatusCode.OK, mal.StatusCode);   // 401 del API = contraseña actual incorrecta, NO cierre de sesión
        var htmlMal = await Texto(mal);
        Assert.Contains("La contraseña actual es incorrecta", htmlMal);
        Assert.DoesNotContain("incorrecta\" value", htmlMal);

        var distinta = await Post(c, "/Perfil/CambiarPassword", form, new() { ["PasswordActual"] = "patito", ["PasswordNueva"] = "nueva123", ["Confirmacion"] = "otra" });
        Assert.Contains("no coincide", await Texto(distinta));

        var bien = await Post(c, "/Perfil/CambiarPassword", form, new() { ["PasswordActual"] = "patito", ["PasswordNueva"] = "nueva123", ["Confirmacion"] = "nueva123" });
        Assert.Equal(HttpStatusCode.Redirect, bien.StatusCode);
    }
}
