using Microsoft.AspNetCore.Mvc;

namespace FrontendNet;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View("Index");
    }

    public IActionResult Error([FromServices] IHostEnvironment hostEnvironment)
    {
        return View("Error");
    }

    public IActionResult AccessDenied()
    {
        return View("AccessDenied");
    }

    /// <summary>Página para códigos de estado sin cuerpo (404, 403…) vía UseStatusCodePagesWithReExecute.</summary>
    [ActionName("Estado")]
    public IActionResult Status(int id)
    {
        // SEC-09: Valida rango válido de HTTP StatusCode (400-599) para evitar ArgumentOutOfRangeException
        var codigo = id is >= 400 and <= 599 ? id : StatusCodes.Status500InternalServerError;
        Response.StatusCode = codigo;
        ViewBag.Codigo = codigo;
        return View("Status");
    }
}
