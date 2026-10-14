using FrontendNet.Models;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrontendNet;

[Authorize(Roles = "Administrador")]
public class CategoriasController(CategoriaClientService categorias) : Controller
{
    public async Task<IActionResult> Index()
    {
        List<Categoria>? lista = [];
        try
        {
            lista = await categorias.GetAsync();
        }
        catch (HttpRequestException ex)
        {
            if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return RedirectToAction("Salir", "Auth");
            }
        }
        return View(lista);
    }

    public async Task<IActionResult> Detalle(int id)
    {
        Categoria? item = null;
        try
        {
            item = await categorias.GetAsync(id);
            if (item == null) return NotFound();
        }
        catch (HttpRequestException ex)
        {
            if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return RedirectToAction("Salir", "Auth");
            }
        }
        return View(item);
    }

    public IActionResult Crear()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> CrearAsync(Categoria itemToCreate)
    {
        if (ModelState.IsValid)
        {

            try
            {
                await categorias.PostAsync(itemToCreate);
                return RedirectToAction(nameof(Index));
            }
            catch (HttpRequestException ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return RedirectToAction("Salir", "Auth");
                }
            }
        }
        ModelState.AddModelError(string.Empty, "Ocurrió un error al procesar la solicitud. Inténtelo nuevamente.");
        return View(itemToCreate);
    }

    public async Task<IActionResult> EditarAsync(int id)
    {
        Categoria? item = null;
        try
        {
            item = await categorias.GetAsync(id);
            if (item == null) return NotFound();
        }
        catch (HttpRequestException ex)
        {
            if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return RedirectToAction("Salir", "Auth");
            }
        }
        return View(item);
    }

    [HttpPost]
    public async Task<IActionResult> EditarAsync(int id, Categoria itemToUpdate)
    {
        if (id != itemToUpdate.CategoriaId)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                await categorias.PutAsync(itemToUpdate);
                return RedirectToAction(nameof(Index));
            }
            catch (HttpRequestException ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return RedirectToAction("Salir", "Auth");
                }
            }
        }
        ModelState.AddModelError(string.Empty, "Ocurrió un error al procesar la solicitud. Inténtelo nuevamente.");
        return View(itemToUpdate);
    }

    public async Task<IActionResult> EliminarAsync(int id, bool? showError = false)
    {
        Categoria? item = null;
        try
        {
            item = await categorias.GetAsync(id);
            if (item == null) return NotFound();
            if (showError.GetValueOrDefault())
            {
                ViewBag.ErrorMessage = "No se puede eliminar la categoría porque tiene productos asociados.";
            }
        }
        catch (HttpRequestException ex)
        {
            if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return RedirectToAction("Salir", "Auth");
            }
        }
        return View(item);
    }

    [HttpPost]
    public async Task<IActionResult> Eliminar(int id)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await categorias.DeleteAsync(id);
                return RedirectToAction(nameof(Index));
            }
            catch (HttpRequestException ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return RedirectToAction("Salir", "Auth");
                }
                else if (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    return RedirectToAction(nameof(Eliminar), new { id, showError = true });
                }
            }
        }
        return RedirectToAction(nameof(Eliminar), new { id, showError = true });
    }
}