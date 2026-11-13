using FrontendNet.Models;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FrontendNet;

[Authorize(Roles = "ADMIN")]
public class ProductsController(ProductClientService products,
    CategoryClientService categories,
    FilesClientService files) : AppController
{
    public async Task<IActionResult> Index(string? s, int page = 1)
    {
        try
        {
            var items = await products.GetAsync(s) ?? [];
            ViewBag.SoloAdmin = true;
            ViewBag.search = s;
            return View("Index", Paginate(items, page));
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
            var item = await products.GetAsync(id);
            if (item == null) return NotFound();
            return View("Details", item);
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return ErrorGet(ex);
        }
    }

    [ActionName("Crear")]
    public async Task<IActionResult> Create()
    {
        await ProductosDropDownListAsync();
        return View("Create");
    }

    [HttpPost]
    [ActionName("Crear")]
    public async Task<IActionResult> CreateAsync(Product itemToCreate)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await products.PostAsync(itemToCreate);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (IsApiError(ex))
            {
                var result = HandleApiError(ex);
                if (result != null) return result;
            }
        }
        await ProductosDropDownListAsync(itemToCreate.FileId);
        return View("Create", itemToCreate);
    }

    [ActionName("Editar")]
    public async Task<IActionResult> EditAsync(int id)
    {
        try
        {
            var itemToEdit = await products.GetAsync(id);
            if (itemToEdit == null) return NotFound();
            await ProductosDropDownListAsync(itemToEdit.FileId);
            return View("Edit", itemToEdit);
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return ErrorGet(ex);
        }
    }

    [HttpPost]
    [ActionName("Editar")]
    public async Task<IActionResult> EditAsync(int id, Product itemToEdit)
    {
        if (id != itemToEdit.ProductId) return NotFound();
        if (ModelState.IsValid)
        {
            try
            {
                await products.PutAsync(itemToEdit);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (IsApiError(ex))
            {
                var result = HandleApiError(ex);
                if (result != null) return result;
            }
        }
        await ProductosDropDownListAsync(itemToEdit.FileId);
        return View("Edit", itemToEdit);
    }

    [ActionName("Eliminar")]
    public async Task<IActionResult> Delete(int id, string? error = null)
    {
        try
        {
            var itemToDelete = await products.GetAsync(id);
            if (itemToDelete == null) return NotFound();
            ViewData["ErrorMessage"] = error;
            return View("Delete", itemToDelete);
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
            await products.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return HandleApiError(ex) is { } result
                ? result
                : RedirectToAction("Eliminar", new { id, error = GetMessageFor(ex) });
        }
    }

    [ActionName("Categorias")]
    public async Task<IActionResult> Categories(int id)
    {
        try
        {
            var itemToView = await products.GetAsync(id);
            if (itemToView == null) return NotFound();
            ViewData["ProductoId"] = itemToView.ProductId;
            return View("Categories", itemToView);
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return ErrorGet(ex);
        }
    }

    [ActionName("CategoriasAgregar")]
    public async Task<IActionResult> AddCategory(int id)
    {
        try
        {
            Product? producto = await products.GetAsync(id);
            if (producto == null) return NotFound();
            await CategoriasDropDownListAsync();
            return View("AddCategory", new ProductCategory { Product = producto });
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return ErrorGet(ex);
        }
    }

    [HttpPost]
    [ActionName("CategoriasAgregar")]
    public async Task<IActionResult> AddCategory(int id, int categoryId)
    {
        Product? producto = null;
        try
        {
            producto = await products.GetAsync(id);
            if (producto == null) return NotFound();
            Category? categoria = await categories.GetAsync(categoryId);
            if (categoria == null) return NotFound();
            await products.PostAsync(id, categoryId);
            return RedirectToAction("Categorias", new { id });
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            var result = HandleApiError(ex, "id");
            if (result != null) return result;
        }
        await CategoriasDropDownListAsync();
        return View("AddCategory", new ProductCategory { Product = producto });
    }

    [ActionName("CategoriasRemover")]
    public async Task<IActionResult> RemoveCategory(int id, int categoryId, string? error = null)
    {
        try
        {
            Product? producto = await products.GetAsync(id);
            if (producto == null) return NotFound();
            Category? categoria = await categories.GetAsync(categoryId);
            if (categoria == null) return NotFound();
            ViewData["ErrorMessage"] = error;
            return View("RemoveCategory", new ProductCategory { Product = producto, CategoryId = categoryId, Name = categoria.Name });
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return ErrorGet(ex);
        }
    }

    [HttpPost]
    [ActionName("CategoriasRemover")]
    public async Task<IActionResult> RemoveCategory(int id, int categoryId)
    {
        try
        {
            await products.DeleteAsync(id, categoryId);
            return RedirectToAction("Categorias", new { id });
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return HandleApiError(ex) is { } result
                ? result
                : RedirectToAction("CategoriasRemover", new { id, categoryId, error = GetMessageFor(ex) });
        }
    }

    private async Task CategoriasDropDownListAsync(object? itemSeleccionado = null)
    {
        List<Category> items = [];
        try { items = await categories.GetAsync() ?? []; }
        catch (Exception ex) when (IsApiError(ex)) { ModelState.AddModelError("", GetMessageFor(ex)); }
        ViewBag.Category = new SelectList(items, "CategoryId", "Name", itemSeleccionado);
    }

    private async Task ProductosDropDownListAsync(object? itemSeleccionado = null)
    {
        List<FileRecord> items = [];
        try { items = await files.GetAsync() ?? []; }
        catch (Exception ex) when (IsApiError(ex)) { ModelState.AddModelError("", GetMessageFor(ex)); }
        ViewBag.FileRecord = new SelectList(items, "FileId", "Name", itemSeleccionado);
    }
}
