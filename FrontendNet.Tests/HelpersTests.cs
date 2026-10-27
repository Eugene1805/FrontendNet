using FrontendNet.Helpers;
using FrontendNet.Middlewares;
using FrontendNet.Models;
using FrontendNet.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace FrontendNet.Tests;

public class HelpersTests
{
    [Fact]
    public void JwtInfo_lee_exp_iss_aud_sin_validar_firma()
    {
        var exp = DateTimeOffset.UtcNow.AddMinutes(20);
        var info = JwtInfo.Lee(JwtFalso.Crea(exp))!;
        Assert.Equal(exp.ToUnixTimeSeconds(), info.Expira!.Value.ToUnixTimeSeconds());
        Assert.Equal("ServidorFeiJWT", info.Emisor);
        Assert.Equal("ClientesFeiJWT", info.Audiencia);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-es-jwt")]
    [InlineData("a.%%%.c")]
    public void JwtInfo_con_valores_invalidos_devuelve_null(string? jwt) => Assert.Null(JwtInfo.Lee(jwt));

    [Fact]
    public void ListaPaginada_calcula_paginas_y_acota_la_pagina_solicitada()
    {
        var lista = new ListaPaginada<int>(Enumerable.Range(1, 45), pagina: 99, tamano: 20);
        Assert.Equal(3, lista.TotalPaginas);
        Assert.Equal(3, lista.Pagina);
        Assert.Equal(45, lista.Total);
        Assert.Equal([41, 42, 43, 44, 45], lista.Items);
    }

    [Fact]
    public void ListaPaginada_vacia_tiene_una_pagina()
    {
        var lista = new ListaPaginada<int>([], pagina: 0, tamano: 20);
        Assert.Equal(1, lista.TotalPaginas);
        Assert.Equal(1, lista.Pagina);
        Assert.Empty(lista.Items);
    }

    [Fact]
    public void Url_Codifica_escapa_caracteres_reservados()
        => Assert.Equal("a%2Bb%40c%26d", Url.Codifica("a+b@c&d"));

    [Fact]
    public void EstadoPedido_define_las_transiciones_validas()
    {
        Assert.Equal([EstadoPedido.Pagado, EstadoPedido.Cancelado], EstadoPedido.Siguientes(EstadoPedido.Pendiente));
        Assert.Equal([EstadoPedido.Entregado], EstadoPedido.Siguientes(EstadoPedido.Enviado));
        Assert.Empty(EstadoPedido.Siguientes(EstadoPedido.Entregado));
        Assert.Empty(EstadoPedido.Siguientes(EstadoPedido.Cancelado));
    }

    private class Entorno(string nombre) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = nombre;
        public string ApplicationName { get; set; } = "t";
        public string ContentRootPath { get; set; } = ".";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private static IConfiguration Config(params (string, string)[] valores) =>
        new ConfigurationBuilder().AddInMemoryCollection(valores.ToDictionary(v => v.Item1, v => (string?)v.Item2)).Build();

    [Theory]
    [InlineData("""{"id":"u","email":"e@x.mx","nombre":"N","rol":"R","protegido":1}""", true)]
    [InlineData("""{"id":"u","email":"e@x.mx","nombre":"N","rol":"R","protegido":0}""", false)]
    [InlineData("""{"id":"u","email":"e@x.mx","nombre":"N","rol":"R","protegido":true}""", true)]
    [InlineData("""{"id":"u","email":"e@x.mx","nombre":"N","rol":"R","protegido":false}""", false)]
    public void El_backend_envia_booleanos_como_0_o_1_y_se_leen_bien(string json, bool esperado)
    {
        var usuario = System.Text.Json.JsonSerializer.Deserialize<Usuario>(json, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.Equal(esperado, usuario!.Protegido);
    }

    [Fact]
    public void La_direccion_se_recorta_antes_de_validar_el_minimo()
    {
        var modelo = new PedidoNuevo { DireccionEnvio = "  Av Xalapa  " };
        Assert.Equal("Av Xalapa", modelo.DireccionEnvio);
        var resultados = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        Assert.False(System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            modelo, new System.ComponentModel.DataAnnotations.ValidationContext(modelo), resultados, true));
    }

    [Fact]
    public void Csp_permite_a_los_CDN_servir_los_mapas_de_las_librerias()
    {
        var csp = SecurityHeadersMiddleware.ConstruyeCsp(Config(), new Entorno("Production"));
        Assert.Contains("connect-src 'self' https://cdnjs.cloudflare.com https://cdn.jsdelivr.net", csp);
    }

    [Fact]
    public void Csp_no_permite_scripts_en_linea_ni_framing()
    {
        var csp = SecurityHeadersMiddleware.ConstruyeCsp(Config(), new Entorno("Production"));
        var script = csp.Split("; ").Single(d => d.StartsWith("script-src"));
        Assert.DoesNotContain("unsafe-inline", script);
        Assert.DoesNotContain("unsafe-eval", script);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.Contains("object-src 'none'", csp);
        Assert.DoesNotContain("ws:", csp);
    }

    [Fact]
    public void Csp_en_modo_Directo_permite_el_origen_del_API_para_imagenes()
    {
        var csp = SecurityHeadersMiddleware.ConstruyeCsp(
            Config(("Imagenes:Modo", "Directo"), ("UrlWebAPI", "http://localhost:3000/api")), new Entorno("Production"));
        Assert.Contains("img-src 'self' data: https://placehold.co http://localhost:3000", csp);
    }

    [Fact]
    public void Csp_en_modo_Proxy_no_abre_imagenes_al_API()
    {
        var csp = SecurityHeadersMiddleware.ConstruyeCsp(Config(("UrlWebAPI", "http://localhost:3000")), new Entorno("Production"));
        Assert.DoesNotContain("localhost:3000", csp);
    }
}
