using System.Net;
using FrontendNet.Models;
using Microsoft.AspNetCore.Mvc;

namespace FrontendNet.Tests;

/// <summary>Mapeo transversal de errores del API a la UI (D-04).</summary>
public class ErrorMappingTests
{
    private class Prueba : AppController
    {
        public IActionResult? Maneja(Exception ex, string clave = "") => ManejaErrorApi(ex, clave);
        public static string Mensaje(Exception ex) => MensajePara(ex);
        public static bool EsError(Exception ex) => EsErrorApi(ex);
    }

    private static ApiException Api(HttpStatusCode status, string? mensaje = null, List<ApiErrorCampo>? errores = null, TimeSpan? retry = null)
        => new(status, new ApiError { Mensaje = mensaje, Errores = errores }, retry);

    [Fact]
    public void Error_401_redirige_a_Auth_Salir()
    {
        var r = Assert.IsType<RedirectToActionResult>(new Prueba().Maneja(Api(HttpStatusCode.Unauthorized)));
        Assert.Equal(("Salir", "Auth"), (r.ActionName, r.ControllerName));
    }

    [Fact]
    public void Error_404_devuelve_NotFound()
        => Assert.IsType<NotFoundResult>(new Prueba().Maneja(Api(HttpStatusCode.NotFound)));

    [Fact]
    public void Error_403_en_formulario_muestra_el_mensaje_del_API_como_usuario_protegido()
    {
        var c = new Prueba();
        var r = c.Maneja(Api(HttpStatusCode.Forbidden, "No se pueden eliminar usuarios protegidos del sistema."));

        Assert.Null(r);
        Assert.Equal("No se pueden eliminar usuarios protegidos del sistema.", c.ModelState[""]!.Errors.Single().ErrorMessage);
    }

    [Fact]
    public void Error_400_con_errores_se_mapea_por_campo_sin_distinguir_mayusculas()
    {
        var c = new Prueba();
        var r = c.Maneja(Api(HttpStatusCode.BadRequest, "Datos inválidos",
        [
            new ApiErrorCampo { Path = "titulo", Msg = "El título es obligatorio" },
            new ApiErrorCampo { Path = "precio", Msg = "Precio inválido" },
        ]));

        Assert.Null(r);
        Assert.Equal("El título es obligatorio", c.ModelState["Titulo"]!.Errors.Single().ErrorMessage);
        Assert.Equal("Precio inválido", c.ModelState["Precio"]!.Errors.Single().ErrorMessage);
    }

    [Fact]
    public void Error_409_muestra_el_mensaje_del_API_en_la_clave_indicada()
    {
        var c = new Prueba();
        Assert.Null(c.Maneja(Api(HttpStatusCode.Conflict, "El correo electrónico ya se encuentra registrado."), "Email"));
        Assert.Equal("El correo electrónico ya se encuentra registrado.", c.ModelState["Email"]!.Errors.Single().ErrorMessage);
    }

    [Fact]
    public void Error_429_informa_la_espera_en_minutos()
    {
        var m = Prueba.Mensaje(Api(HttpStatusCode.TooManyRequests, "x", retry: TimeSpan.FromSeconds(100)));
        Assert.Contains("Demasiados intentos", m);
        Assert.Contains("2 minuto", m);
    }

    [Fact]
    public void API_caido_muestra_mensaje_amable_sin_detalles_tecnicos()
    {
        var m = Prueba.Mensaje(new ApiException(null, null, null, new HttpRequestException("Connection refused 127.0.0.1:3000")));
        Assert.Contains("No fue posible comunicarse", m);
        Assert.DoesNotContain("127.0.0.1", m);
    }

    [Fact]
    public void Error_500_no_filtra_el_mensaje_interno_del_servidor()
    {
        var m = Prueba.Mensaje(Api(HttpStatusCode.InternalServerError, "SequelizeConnectionError: stack…"));
        Assert.DoesNotContain("Sequelize", m);
    }

    [Fact]
    public void Solo_los_errores_de_API_red_o_JSON_se_consideran_recuperables()
    {
        Assert.True(Prueba.EsError(new HttpRequestException()));
        Assert.True(Prueba.EsError(new System.Text.Json.JsonException()));
        Assert.True(Prueba.EsError(new TaskCanceledException()));
        Assert.False(Prueba.EsError(new InvalidOperationException()));
    }
}
