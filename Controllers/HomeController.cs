using Microsoft.AspNetCore.Mvc;

namespace FrontendNet;

public class HomeController : Controller
{
    public IActionResult Index() => View("Index");

    public IActionResult Error() => View("Error");

    public IActionResult AccessDenied() => View("AccessDenied");

    /// <summary>Renders friendly error pages for status codes via UseStatusCodePagesWithReExecute.</summary>
    public IActionResult Status(int id)
    {
        var validStatusCode = id is >= 400 and <= 599 ? id : StatusCodes.Status500InternalServerError;
        Response.StatusCode = validStatusCode;
        ViewBag.StatusCode = validStatusCode;
        return View("Status");
    }
}
