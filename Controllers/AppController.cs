using System.Net;
using System.Text.Json;
using FrontendNet.Models;
using Microsoft.AspNetCore.Mvc;

namespace FrontendNet;

/// <summary>
/// Base de los controladores que consumen el API. Centraliza el mapeo de errores (D-04):
/// 401 → Auth/Salir · 403 → AccessDenied · 404 → NotFound · 400 → ModelState por campo ·
/// 409/otros → mensaje del API · 429 → espera · red caída/5xx → mensaje amable.
/// </summary>
public abstract class AppController : Controller
{
    protected static bool EsErrorApi(Exception ex) =>
        ex is HttpRequestException or JsonException or TaskCanceledException;

    private static HttpStatusCode? Estado(Exception ex) => (ex as HttpRequestException)?.StatusCode;

    // En un POST, un 403 es una regla de negocio con mensaje (p. ej. usuario protegido): se muestra el mensaje.
    // En un GET, un 403 significa que el rol no puede ver el recurso: AccessDenied.
    private IActionResult? Redireccion(Exception ex, bool mostrarMensaje403 = false) => Estado(ex) switch
    {
        HttpStatusCode.Unauthorized => RedirectToAction("Salir", "Auth"),
        HttpStatusCode.Forbidden when !mostrarMensaje403 => RedirectToAction("AccessDenied", "Home"),
        HttpStatusCode.NotFound => NotFound(),
        _ => null
    };

    /// <summary>Mensaje seguro para mostrar al usuario (sin detalles técnicos).</summary>
    protected static string MensajePara(Exception ex)
    {
        if (ex is ApiException api)
        {
            if (api.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var espera = api.RetryAfter is { } t && t > TimeSpan.Zero
                    ? $" Intente de nuevo en {Math.Max(1, (int)Math.Ceiling(t.TotalMinutes))} minuto(s)."
                    : " Intente de nuevo más tarde.";
                return "Demasiados intentos." + espera;
            }
            if ((int?)api.StatusCode is >= 400 and < 500 && !string.IsNullOrWhiteSpace(api.Error?.Mensaje))
                return api.Error!.Mensaje!;
            if (api.StatusCode == null)
                return "No fue posible comunicarse con el servidor. Inténtelo de nuevo más tarde.";
        }
        return "No ha sido posible realizar la acción. Inténtelo nuevamente.";
    }

    /// <summary>
    /// Para acciones que reciben un formulario. Devuelve un resultado cuando hay que salir de la acción
    /// (401/403/404); si devuelve null, los errores ya están en el ModelState y se debe regresar la vista.
    /// </summary>
    protected IActionResult? ManejaErrorApi(Exception ex, string claveGeneral = "")
    {
        var salida = Redireccion(ex, mostrarMensaje403: true);
        if (salida != null) return salida;

        if (ex is ApiException { StatusCode: HttpStatusCode.BadRequest, Error.Errores: { Count: > 0 } errores })
        {
            // path del backend ("titulo") → propiedad del modelo ("Titulo"); ModelState no distingue mayúsculas.
            foreach (var e in errores)
                ModelState.AddModelError(e.Path ?? claveGeneral, e.Msg ?? "Valor no válido.");
            return null;
        }
        ModelState.AddModelError(claveGeneral, MensajePara(ex));
        return null;
    }

    /// <summary>Para acciones GET: nunca regresa View(null); muestra una página de error amigable.</summary>
    protected IActionResult ErrorGet(Exception ex)
    {
        var salida = Redireccion(ex);
        if (salida != null) return salida;
        Response.StatusCode = (int?)Estado(ex) is >= 400 and < 600 and var codigo ? codigo : 503;
        return View("ErrorApi", MensajePara(ex));
    }
    /// <summary>Pagina en el frontend (el API no pagina) y publica los datos del paginador en ViewBag.</summary>
    protected List<T> Pagina<T>(IEnumerable<T> origen, int pagina)
    {
        var tamano = HttpContext.RequestServices.GetRequiredService<IConfiguration>().GetValue("Paginacion:Tamano", 20);
        var lista = new ListaPaginada<T>(origen, pagina, tamano);
        ViewBag.Pagina = lista.Pagina;
        ViewBag.TotalPaginas = lista.TotalPaginas;
        ViewBag.Total = lista.Total;
        return lista.Items;
    }
}
