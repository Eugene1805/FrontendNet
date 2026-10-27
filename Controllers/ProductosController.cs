using FrontendNet.Models;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FrontendNet;

[Authorize(Roles = "Administrador")]
public class ProductosController(ProductoClientService productos,
    CategoriaClientService categorias,
    ArchivosClientService archivos) : AppController
{
    public async Task<IActionResult> Index(string? s, int pagina = 1)
    {
        try
        {
            var lista = await productos.GetAsync(s) ?? [];
            ViewBag.SoloAdmin = true;
            ViewBag.search = s;
            return View(Pagina(lista, pagina));
        }
        catch (Exception ex) when (EsErrorApi(ex))
        {
            return ErrorGet(ex);
        }
    }

    public async Task<IActionResult> Detalle(int id)
    {
        try
        {
            var item = await productos.GetAsync(id);
            if (item == null) return NotFound();
            return View(item);
        }
        catch (Exception ex) when (EsErrorApi(ex))
        {
            return ErrorGet(ex);
        }
    }

    public async Task<IActionResult> Crear()
    {
        await ProductosDropDownListAsync();
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> CrearAsync(Producto itemToCreate)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await productos.PostAsync(itemToCreate);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (EsErrorApi(ex))
            {
                var salida = ManejaErrorApi(ex);
                if (salida != null) return salida;
            }
        }
        await ProductosDropDownListAsync(itemToCreate.ArchivoId);
        return View(itemToCreate);
    }

    public async Task<IActionResult> EditarAsync(int id)
    {
        try
        {
            var itemToEdit = await productos.GetAsync(id);
            if (itemToEdit == null) return NotFound();
            await ProductosDropDownListAsync(itemToEdit.ArchivoId);
            return View(itemToEdit);
        }
        catch (Exception ex) when (EsErrorApi(ex))
        {
            return ErrorGet(ex);
        }
    }

    [HttpPost]
    public async Task<IActionResult> EditarAsync(int id, Producto itemToEdit)
    {
        if (id != itemToEdit.ProductoId) return NotFound();
        if (ModelState.IsValid)
        {
            try
            {
                await productos.PutAsync(itemToEdit);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (EsErrorApi(ex))
            {
                var salida = ManejaErrorApi(ex);
                if (salida != null) return salida;
            }
        }
        await ProductosDropDownListAsync(itemToEdit.ArchivoId);
        return View(itemToEdit);
    }

    public async Task<IActionResult> Eliminar(int id, string? error = null)
    {
        try
        {
            var itemToDelete = await productos.GetAsync(id);
            if (itemToDelete == null) return NotFound();
            ViewData["ErrorMessage"] = error;
            return View(itemToDelete);
        }
        catch (Exception ex) when (EsErrorApi(ex))
        {
            return ErrorGet(ex);
        }
    }

    [HttpPost]
    public async Task<IActionResult> Eliminar(int id)
    {
        try
        {
            await productos.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (EsErrorApi(ex))
        {
            return ManejaErrorApi(ex) is { } salida
                ? salida
                : RedirectToAction(nameof(Eliminar), new { id, error = MensajePara(ex) });
        }
    }

    public async Task<IActionResult> Categorias(int id)
    {
        try
        {
            var itemToView = await productos.GetAsync(id);
            if (itemToView == null) return NotFound();
            ViewData["ProductoId"] = itemToView.ProductoId;
            return View(itemToView);
        }
        catch (Exception ex) when (EsErrorApi(ex))
        {
            return ErrorGet(ex);
        }
    }

    public async Task<IActionResult> CategoriasAgregar(int id)
    {
        try
        {
            Producto? producto = await productos.GetAsync(id);
            if (producto == null) return NotFound();
            await CategoriasDropDownListAsync();
            return View(new ProductoCategoria { Producto = producto });
        }
        catch (Exception ex) when (EsErrorApi(ex))
        {
            return ErrorGet(ex);
        }
    }

    [HttpPost]
    public async Task<IActionResult> CategoriasAgregar(int id, int categoriaid)
    {
        Producto? producto = null;
        try
        {
            producto = await productos.GetAsync(id);
            if (producto == null) return NotFound();
            Categoria? categoria = await categorias.GetAsync(categoriaid);
            if (categoria == null) return NotFound();
            await productos.PostAsync(id, categoriaid);
            return RedirectToAction(nameof(Categorias), new { id });
        }
        catch (Exception ex) when (EsErrorApi(ex))
        {
            var salida = ManejaErrorApi(ex, "id");
            if (salida != null) return salida;
        }
        await CategoriasDropDownListAsync();
        return View(new ProductoCategoria { Producto = producto });
    }

    public async Task<IActionResult> CategoriasRemover(int id, int categoriaid, string? error = null)
    {
        try
        {
            Producto? producto = await productos.GetAsync(id);
            if (producto == null) return NotFound();
            Categoria? categoria = await categorias.GetAsync(categoriaid);
            if (categoria == null) return NotFound();
            ViewData["ErrorMessage"] = error;
            return View(new ProductoCategoria { Producto = producto, CategoriaId = categoriaid, Nombre = categoria.Nombre });
        }
        catch (Exception ex) when (EsErrorApi(ex))
        {
            return ErrorGet(ex);
        }
    }

    [HttpPost]
    public async Task<IActionResult> CategoriasRemover(int id, int categoriaid)
    {
        try
        {
            await productos.DeleteAsync(id, categoriaid);
            return RedirectToAction(nameof(Categorias), new { id });
        }
        catch (Exception ex) when (EsErrorApi(ex))
        {
            return ManejaErrorApi(ex) is { } salida
                ? salida
                : RedirectToAction(nameof(CategoriasRemover), new { id, categoriaid, error = MensajePara(ex) });
        }
    }

    private async Task CategoriasDropDownListAsync(object? itemSeleccionado = null)
    {
        List<Categoria> listado = [];
        try { listado = await categorias.GetAsync() ?? []; }
        catch (Exception ex) when (EsErrorApi(ex)) { ModelState.AddModelError("", MensajePara(ex)); }
        ViewBag.Categoria = new SelectList(listado, "CategoriaId", "Nombre", itemSeleccionado);
    }

    private async Task ProductosDropDownListAsync(object? itemSeleccionado = null)
    {
        List<Archivo> listado = [];
        try { listado = await archivos.GetAsync() ?? []; }
        catch (Exception ex) when (EsErrorApi(ex)) { ModelState.AddModelError("", MensajePara(ex)); }
        ViewBag.Archivo = new SelectList(listado, "ArchivoId", "Nombre", itemSeleccionado);
    }
}
