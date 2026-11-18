using FrontendNet.Models;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrontendNet;

[Authorize(Roles = "ADMIN,Administrador")]
public class CategoriesController(CategoryClientService categories) : AppController
{
    public async Task<IActionResult> Index()
    {
        try
        {
            return View("Index", await categories.GetAsync() ?? []);
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return ErrorGet(ex);
        }
    }

    [ActionName("Detalle")]
    public async Task<IActionResult> Details(int id)
    {
        try
        {
            var item = await categories.GetAsync(id);
            if (item == null) return NotFound();
            return View("Details", item);
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return ErrorGet(ex);
        }
    }

    [ActionName("Crear")]
    public IActionResult Create()
    {
        return View("Create");
    }

    [HttpPost]
    [ActionName("Crear")]
    public async Task<IActionResult> CreateAsync(Category itemToCreate)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await categories.PostAsync(itemToCreate);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (IsApiError(ex))
            {
                var result = HandleApiError(ex);
                if (result != null) return result;
            }
        }
        return View("Create", itemToCreate);
    }

    [ActionName("Editar")]
    public async Task<IActionResult> EditAsync(int id)
    {
        try
        {
            var item = await categories.GetAsync(id);
            if (item == null) return NotFound();
            return View("Edit", item);
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return ErrorGet(ex);
        }
    }

    [HttpPost]
    [ActionName("Editar")]
    public async Task<IActionResult> EditAsync(int id, Category itemToUpdate)
    {
        if (id != itemToUpdate.CategoryId)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                await categories.PutAsync(itemToUpdate);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (IsApiError(ex))
            {
                var result = HandleApiError(ex);
                if (result != null) return result;
            }
        }
        return View("Edit", itemToUpdate);
    }

    [ActionName("Eliminar")]
    public async Task<IActionResult> DeleteAsync(int id, string? error = null)
    {
        try
        {
            var item = await categories.GetAsync(id);
            if (item == null) return NotFound();
            ViewBag.ErrorMessage = error;
            return View("Delete", item);
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return ErrorGet(ex);
        }
    }

    [HttpPost]
    [ActionName("Eliminar")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await categories.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            // D-06: el API no responde 409; la FK es ON DELETE CASCADE y las protegidas responden 400 con su mensaje.
            return HandleApiError(ex) is { } result
                ? result
                : RedirectToAction("Eliminar", new { id, error = GetMessageFor(ex) });
        }
    }
}
