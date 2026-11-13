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
        Response.StatusCode = id;
        ViewBag.Codigo = id;
        return View("Status");
    }
}
