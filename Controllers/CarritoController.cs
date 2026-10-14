using System.Security.Claims;
using FrontendNet.Models;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FrontendNet;

[Authorize(Roles = "Usuario")]
public class CarritoController() : Controller
{
    public ActionResult Index()
    {
        return View();
    }
}