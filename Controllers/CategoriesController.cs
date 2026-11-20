using FrontendNet.Models;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrontendNet;

[Authorize(Roles = Roles.Admin)]
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
            return await HandleGetErrorAsync(ex);
        }
    }

    public async Task<IActionResult> Details(int id)
    {
        try
        {
            var item = await categories.GetAsync(id);
            if (item is null) return NotFound();
            return View("Details", item);
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return await HandleGetErrorAsync(ex);
        }
    }

    public IActionResult Create() => View("Create");

    [HttpPost]
    public async Task<IActionResult> CreateAsync(Category itemToCreate)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await categories.PostAsync(itemToCreate);
                TempData["Success"] = "Categoría creada correctamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (IsApiError(ex))
            {
                var result = await HandleApiErrorAsync(ex, nameof(itemToCreate.Name));
                if (result is not null) return result;
            }
        }

        return View("Create", itemToCreate);
    }

    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            var item = await categories.GetAsync(id);
            if (item is null) return NotFound();
            return View("Edit", item);
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return await HandleGetErrorAsync(ex);
        }
    }

    [HttpPost]
    public async Task<IActionResult> EditAsync(int id, Category itemToUpdate)
    {
        if (id != itemToUpdate.CategoryId)
            return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                await categories.PutAsync(itemToUpdate);
                TempData["Success"] = "Categoría actualizada correctamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (IsApiError(ex))
            {
                var result = await HandleApiErrorAsync(ex, nameof(itemToUpdate.Name));
                if (result is not null) return result;
            }
        }

        return View("Edit", itemToUpdate);
    }

    public async Task<IActionResult> Delete(int id, string? error = null)
    {
        try
        {
            var item = await categories.GetAsync(id);
            if (item is null) return NotFound();

            ViewData["ErrorMessage"] = error;
            return View("Delete", item);
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return await HandleGetErrorAsync(ex);
        }
    }

    [HttpPost]
    [ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmedAsync(int id)
    {
        try
        {
            await categories.DeleteAsync(id);
            TempData["Success"] = "Categoría eliminada correctamente.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            var result = await HandleApiErrorAsync(ex);
            if (result is not null) return result;
            return RedirectToAction(nameof(Delete), new { id, error = GetUserMessage(ex) });
        }
    }
}
