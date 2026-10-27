using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using FrontendNet.Models;
using FrontendNet.Tests.Support;

namespace FrontendNet.Tests;

public class RefrescaTokenHandlerTests
{
    private static HttpResponseMessage ConToken(HttpResponseMessage r, string jwt)
    {
        r.Headers.Add("Set-Authorization", jwt);
        return r;
    }

    [Fact]
    public async Task Respuesta_exitosa_con_Set_Authorization_renueva_el_jwt_de_la_cookie()
    {
        var ctx = new ContextoFalso();
        var nuevo = JwtFalso.Crea(DateTimeOffset.UtcNow.AddMinutes(20));
        var api = new ApiFalso(_ => ConToken(ApiFalso.Json(HttpStatusCode.OK, new[] { new { categoriaId = 1, nombre = "A", protegida = false } }), nuevo));
        var cliente = ctx.ClienteApi(api);

        var r = await cliente.GetAsync("api/categorias");

        Assert.True(r.IsSuccessStatusCode);
        Assert.Equal(1, ctx.Autenticacion.SignIns);
        Assert.Equal(nuevo, ctx.Autenticacion.UltimoPrincipal!.FindFirstValue("jwt"));
        // Conserva los otros claims del contrato (Email, Name, Role).
        Assert.Equal("patito@uv.mx", ctx.Autenticacion.UltimoPrincipal!.FindFirstValue(ClaimTypes.Email));
        Assert.Equal("Usuario", ctx.Autenticacion.UltimoPrincipal!.FindFirstValue(ClaimTypes.Role));
        // La petición en curso ya ve el token nuevo.
        Assert.Equal(nuevo, ctx.Http.User.FindFirstValue("jwt"));
    }

    [Fact]
    public async Task La_cookie_expira_cuando_expira_el_jwt()
    {
        var ctx = new ContextoFalso();
        var exp = DateTimeOffset.UtcNow.AddMinutes(20);
        var api = new ApiFalso(_ => ConToken(new HttpResponseMessage(HttpStatusCode.OK), JwtFalso.Crea(exp)));

        await ctx.ClienteApi(api).GetAsync("api/x");

        var expiraCookie = ctx.Autenticacion.UltimasPropiedades!.ExpiresUtc!.Value;
        Assert.InRange(Math.Abs((expiraCookie - exp).TotalSeconds), 0, 1);
    }

    [Fact]
    public async Task Respuesta_de_error_con_Set_Authorization_tambien_renueva_el_token()
    {
        var ctx = new ContextoFalso();
        var nuevo = JwtFalso.Crea(DateTimeOffset.UtcNow.AddMinutes(20));
        var api = new ApiFalso(_ => ConToken(ApiFalso.Json(HttpStatusCode.NotFound, new { mensaje = "Producto no encontrado." }), nuevo));

        await Assert.ThrowsAsync<ApiException>(() => ctx.ClienteApi(api).GetAsync("api/productos/99"));

        Assert.Equal(nuevo, ctx.Autenticacion.UltimoPrincipal!.FindFirstValue("jwt"));
    }

    [Fact]
    public async Task Error_400_conserva_mensaje_y_errores_por_campo()
    {
        var ctx = new ContextoFalso();
        var api = new ApiFalso(_ => ApiFalso.JsonCrudo(HttpStatusCode.BadRequest,
            """{"mensaje":"Datos inválidos","errores":[{"type":"field","msg":"El título es obligatorio","path":"titulo","location":"body","value":""}]}"""));

        var ex = await Assert.ThrowsAsync<ApiException>(() => ctx.ClienteApi(api).PostAsync("api/productos", null));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal("Datos inválidos", ex.Error!.Mensaje);
        var campo = Assert.Single(ex.Error.Errores!);
        Assert.Equal("titulo", campo.Path);
        Assert.Equal("El título es obligatorio", campo.Msg);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Errores_HTTP_se_convierten_en_ApiException_con_su_status(HttpStatusCode status)
    {
        var ctx = new ContextoFalso();
        var api = new ApiFalso(_ => ApiFalso.Json(status, new { mensaje = "x" }));

        var ex = await Assert.ThrowsAsync<ApiException>(() => ctx.ClienteApi(api).GetAsync("api/x"));

        Assert.Equal(status, ex.StatusCode);
    }

    [Fact]
    public async Task Error_429_expone_Retry_After()
    {
        var ctx = new ContextoFalso();
        var api = new ApiFalso(_ =>
        {
            var r = ApiFalso.Json(HttpStatusCode.TooManyRequests, new { mensaje = "Demasiados intentos" });
            r.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMinutes(5));
            return r;
        });

        var ex = await Assert.ThrowsAsync<ApiException>(() => ctx.ClienteApi(api).GetAsync("api/x"));

        Assert.Equal(TimeSpan.FromMinutes(5), ex.RetryAfter);
    }

    [Fact]
    public async Task Cuerpo_no_JSON_no_rompe_la_conversion_del_error()
    {
        var ctx = new ContextoFalso();
        var api = new ApiFalso(_ => new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("<html>proxy</html>") });

        var ex = await Assert.ThrowsAsync<ApiException>(() => ctx.ClienteApi(api).GetAsync("api/x"));

        Assert.Equal(HttpStatusCode.BadGateway, ex.StatusCode);
        Assert.Null(ex.Error);
    }

    [Fact]
    public async Task API_caido_se_expone_como_ApiException_sin_status()
    {
        var ctx = new ContextoFalso();
        var api = new ApiFalso(_ => throw new HttpRequestException("connection refused"));

        var ex = await Assert.ThrowsAsync<ApiException>(() => ctx.ClienteApi(api).GetAsync("api/x"));

        Assert.Null(ex.StatusCode);
    }

    [Fact]
    public async Task Envia_el_jwt_de_la_sesion_como_Bearer()
    {
        var ctx = new ContextoFalso(jwt: "mi-jwt");
        var api = new ApiFalso(_ => new HttpResponseMessage(HttpStatusCode.OK));

        await ctx.ClienteApi(api).GetAsync("api/x");

        Assert.Equal("mi-jwt", api.Peticiones.Single().Bearer);
    }
}
