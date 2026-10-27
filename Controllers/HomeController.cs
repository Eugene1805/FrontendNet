using Microsoft.AspNetCore.Mvc;

namespace FrontendNet;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Error([FromServices] IHostEnvironment hostEnvironment)
    {
        return View();
    }

    public IActionResult AccessDenied()
    {
        return View();
    }

    /// <summary>Página para códigos de estado sin cuerpo (404, 403…) vía UseStatusCodePagesWithReExecute.</summary>
    public IActionResult Estado(int id)
    {
        Response.StatusCode = id;
        ViewBag.Codigo = id;
        return View();
    }
}
