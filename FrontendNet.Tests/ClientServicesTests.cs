using System.Net;
using System.Text;
using System.Text.Json;
using FrontendNet.Models;
using FrontendNet.Services;
using FrontendNet.Tests.Support;
using Microsoft.AspNetCore.Http;

namespace FrontendNet.Tests;

/// <summary>Verifica contra los JSON reales del backend (docs/planeacion_frontend.md 2.3).</summary>
public class ClientServicesTests
{
    private static (ApiFalso Api, HttpClient Cliente) Crea(Func<Peticion, HttpResponseMessage> responde)
    {
        var api = new ApiFalso(responde);
        return (api, new ContextoFalso().ClienteApi(api));
    }

    [Fact]
    public async Task Producto_lee_precio_como_string_y_archivoid_en_minusculas()
    {
        var (_, http) = Crea(_ => ApiFalso.JsonCrudo(HttpStatusCode.OK,
            """{"productoId":5,"titulo":"Laptop","descripcion":"d","precio":"15999.00","archivoid":3,"categorias":[{"categoriaId":1,"nombre":"Tech","protegida":false}]}"""));

        var p = await new ProductoClientService(http).GetAsync(5);

        Assert.Equal(15999.00m, p!.Precio);
        Assert.Equal(3, p.ArchivoId);
        Assert.Equal("Tech", p.Categorias!.Single().Nombre);
    }

    [Fact]
    public async Task D01_Crear_producto_envia_archivoid_en_minusculas_y_sin_campos_extra()
    {
        var (api, http) = Crea(_ => new HttpResponseMessage(HttpStatusCode.Created));

        await new ProductoClientService(http).PostAsync(new Producto
        {
            ProductoId = 7, Titulo = "Mouse", Descripcion = "USB", Precio = 99.5m, ArchivoId = 4,
            Categorias = [new Categoria { Nombre = "x" }]
        });

        using var json = JsonDocument.Parse(api.Peticiones.Single().Cuerpo!);
        var raiz = json.RootElement;
        Assert.Equal(4, raiz.GetProperty("archivoid").GetInt32());
        Assert.False(raiz.TryGetProperty("archivoId", out _));
        Assert.False(raiz.TryGetProperty("productoId", out _));
        Assert.False(raiz.TryGetProperty("categorias", out _));
        Assert.Equal("Mouse", raiz.GetProperty("titulo").GetString());
    }

    [Fact]
    public async Task D01_Editar_producto_sin_portada_envia_archivoid_nulo()
    {
        var (api, http) = Crea(_ => new HttpResponseMessage(HttpStatusCode.NoContent));

        await new ProductoClientService(http).PutAsync(new Producto { ProductoId = 7, Titulo = "Mouse", Precio = 1m, ArchivoId = null });

        Assert.Equal(HttpMethod.Put, api.Peticiones.Single().Metodo);
        Assert.Equal("/api/productos/7", api.Peticiones.Single().RutaYQuery);
        using var json = JsonDocument.Parse(api.Peticiones.Single().Cuerpo!);
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("archivoid").ValueKind);
    }

    [Theory]
    [InlineData("a&b", "/api/productos?s=a%26b")]
    [InlineData("100% #1", "/api/productos?s=100%25%20%231")]
    [InlineData("1+1", "/api/productos?s=1%2B1")]
    public async Task D08_Busqueda_de_productos_se_codifica(string busqueda, string esperado)
    {
        var (api, http) = Crea(_ => ApiFalso.Json(HttpStatusCode.OK, Array.Empty<object>()));

        await new ProductoClientService(http).GetAsync(busqueda);

        Assert.Equal(esperado, api.Peticiones.Single().RutaYQuery);
    }

    [Fact]
    public async Task Busqueda_vacia_no_agrega_query()
    {
        var (api, http) = Crea(_ => ApiFalso.Json(HttpStatusCode.OK, Array.Empty<object>()));
        await new ProductoClientService(http).GetAsync(null);
        Assert.Equal("/api/productos", api.Peticiones.Single().RutaYQuery);
    }

    [Fact]
    public async Task D02_Detalle_de_archivo_usa_la_ruta_detalle_no_el_binario()
    {
        var (api, http) = Crea(_ => ApiFalso.JsonCrudo(HttpStatusCode.OK,
            """{"archivoId":3,"mime":"image/jpeg","indb":true,"nombre":"a.jpg","size":1234}"""));

        var a = await new ArchivosClientService(http).GetAsync(3);

        Assert.Equal("/api/archivos/3/detalle", api.Peticiones.Single().RutaYQuery);
        Assert.Equal("a.jpg", a!.Nombre);
        Assert.Equal(1234, a.Size);
    }

    [Fact]
    public async Task Binario_de_archivo_inexistente_devuelve_null()
    {
        var (_, http) = Crea(_ => ApiFalso.Json(HttpStatusCode.NotFound, new { mensaje = "no" }));
        Assert.Null(await new ArchivosClientService(http).GetBinarioAsync(99));
    }

    [Fact]
    public async Task Subir_archivo_envia_multipart_con_el_campo_file()
    {
        var (api, http) = Crea(_ => new HttpResponseMessage(HttpStatusCode.Created));
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        var form = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "Portada", "foto.jpg")
        {
            Headers = new HeaderDictionary(), ContentType = "image/jpeg"
        };

        await new ArchivosClientService(http).PostAsync(new Upload { Portada = form });

        var p = api.Peticiones.Single();
        Assert.Equal(HttpMethod.Post, p.Metodo);
        Assert.Contains("name=file", p.Cuerpo!.Replace("\"", ""));
        Assert.Contains("filename=foto.jpg", p.Cuerpo!.Replace("\"", ""));
    }

    [Fact]
    public async Task Usuarios_codifica_el_email_en_la_ruta()
    {
        var (api, http) = Crea(_ => ApiFalso.JsonCrudo(HttpStatusCode.OK,
            """{"id":"u1","email":"a+b@uv.mx","nombre":"N","protegido":true,"rol":"Administrador"}"""));

        var u = await new UsuariosClientService(http).GetAsync("a+b@uv.mx");

        Assert.Equal("/api/usuarios/a%2Bb%40uv.mx", api.Peticiones.Single().RutaYQuery);
        Assert.True(u!.Protegido);
    }

    [Fact]
    public async Task D13_Editar_usuario_envia_password_solo_si_se_capturo()
    {
        var (api, http) = Crea(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var servicio = new UsuariosClientService(http);

        await servicio.PutAsync(new Usuario { Email = "a@uv.mx", Nombre = "A", Rol = "Usuario", Password = null });
        await servicio.PutAsync(new Usuario { Email = "a@uv.mx", Nombre = "A", Rol = "Usuario", Password = "nueva123" });

        using var sin = JsonDocument.Parse(api.Peticiones[0].Cuerpo!);
        using var con = JsonDocument.Parse(api.Peticiones[1].Cuerpo!);
        Assert.False(sin.RootElement.TryGetProperty("password", out _));
        Assert.Equal("nueva123", con.RootElement.GetProperty("password").GetString());
        // El cuerpo no incluye email ni id: el API toma el email de la ruta.
        Assert.False(sin.RootElement.TryGetProperty("email", out _));
    }

    [Fact]
    public async Task D03_Tiempo_de_sesion_se_deserializa_y_no_es_JSON_crudo()
    {
        var (_, http) = Crea(_ => ApiFalso.JsonCrudo(HttpStatusCode.OK, """{"tiempoRestante":"00:12:34"}"""));
        Assert.Equal("00:12:34", await new PerfilClientService(http).ObtenTiempoAsync());
    }

    [Fact]
    public async Task Login_exitoso_devuelve_AuthUser()
    {
        var api = new ApiFalso(_ => ApiFalso.JsonCrudo(HttpStatusCode.OK, """{"email":"a@uv.mx","nombre":"A","rol":"Usuario","jwt":"tok"}"""));
        var auth = new AuthClientService(new HttpClient(api) { BaseAddress = new Uri("http://api.test") }, new ContextoFalso().Accessor);

        var u = await auth.ObtenTokenAsync("a@uv.mx", "pw");

        Assert.Equal("tok", u.Jwt);
        Assert.Equal("/api/auth", api.Peticiones.Single().RutaYQuery);
    }

    [Fact]
    public async Task Login_con_429_conserva_Retry_After()
    {
        var api = new ApiFalso(_ =>
        {
            var r = ApiFalso.Json(HttpStatusCode.TooManyRequests, new { mensaje = "Demasiados intentos" });
            r.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(90));
            return r;
        });
        var auth = new AuthClientService(new HttpClient(api) { BaseAddress = new Uri("http://api.test") }, new ContextoFalso().Accessor);

        var ex = await Assert.ThrowsAsync<ApiException>(() => auth.ObtenTokenAsync("a@uv.mx", "pw"));

        Assert.Equal(HttpStatusCode.TooManyRequests, ex.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(90), ex.RetryAfter);
    }

    [Fact]
    public async Task Login_con_API_caido_lanza_ApiException_sin_status()
    {
        var api = new ApiFalso(_ => throw new HttpRequestException("refused"));
        var auth = new AuthClientService(new HttpClient(api) { BaseAddress = new Uri("http://api.test") }, new ContextoFalso().Accessor);

        var ex = await Assert.ThrowsAsync<ApiException>(() => auth.ObtenTokenAsync("a@uv.mx", "pw"));

        Assert.Null(ex.StatusCode);
    }

    [Fact]
    public async Task Perfil_propuesto_usa_los_contratos_5_3_y_5_5()
    {
        var (api, http) = Crea(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var perfil = new PerfilClientService(http);

        await perfil.PutNombreAsync("Nuevo");
        await perfil.PutPasswordAsync(new CambioPassword { PasswordActual = "a", PasswordNueva = "b12345", Confirmacion = "b12345" });
        await perfil.RevocaSesionAsync();

        Assert.Equal("/api/perfil", api.Peticiones[0].RutaYQuery);
        Assert.Equal("/api/perfil/password", api.Peticiones[1].RutaYQuery);
        using var json = JsonDocument.Parse(api.Peticiones[1].Cuerpo!);
        Assert.Equal("a", json.RootElement.GetProperty("passwordActual").GetString());
        Assert.Equal("b12345", json.RootElement.GetProperty("passwordNueva").GetString());
        Assert.False(json.RootElement.TryGetProperty("confirmacion", out _));
        Assert.Equal(HttpMethod.Post, api.Peticiones[2].Metodo);
        Assert.Equal("/api/auth/salir", api.Peticiones[2].RutaYQuery);
    }
}
