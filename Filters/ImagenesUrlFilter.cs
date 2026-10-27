using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FrontendNet.Filters;

/// <summary>
/// Publica en ViewBag.Url la base desde la que el navegador pide las imágenes (/api/archivos/{id}).
/// Modo Proxy (default, DD-05): cadena vacía → las pide al propio frontend, que las reenvía desde el API.
/// Modo Directo: UrlWebAPIPublica (o UrlWebAPI) → el navegador habla con el API.
/// </summary>
public class ImagenesUrlFilter(IConfiguration configuration) : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.Controller is not Controller controller) return;
        var directo = string.Equals(configuration["Imagenes:Modo"], "Directo", StringComparison.OrdinalIgnoreCase);
        controller.ViewBag.Url = directo
            ? (configuration["UrlWebAPIPublica"] is { Length: > 0 } publica ? publica : configuration["UrlWebAPI"])
            : "";
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
