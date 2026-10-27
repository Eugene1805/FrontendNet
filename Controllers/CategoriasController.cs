using FrontendNet.Models;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrontendNet;

[Authorize(Roles = "Administrador")]
public class CategoriasController(CategoriaClientService categorias) : AppController
{
    public async Task<IActionResult> Index()
    {
        try
        {
            return View(await categorias.GetAsync() ?? []);
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
            var item = await categorias.GetAsync(id);
            if (item == null) return NotFound();
            return View(item);
        }
        catch (Exception ex) when (EsErrorApi(ex))
        {
            return ErrorGet(ex);
        }
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
            catch (Exception ex) when (EsErrorApi(ex))
            {
                var salida = ManejaErrorApi(ex);
                if (salida != null) return salida;
            }
        }
        return View(itemToCreate);
    }

    public async Task<IActionResult> EditarAsync(int id)
    {
        try
        {
            var item = await categorias.GetAsync(id);
            if (item == null) return NotFound();
            return View(item);
        }
        catch (Exception ex) when (EsErrorApi(ex))
        {
            return ErrorGet(ex);
        }
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
            catch (Exception ex) when (EsErrorApi(ex))
            {
                var salida = ManejaErrorApi(ex);
                if (salida != null) return salida;
            }
        }
        return View(itemToUpdate);
    }

    public async Task<IActionResult> EliminarAsync(int id, string? error = null)
    {
        try
        {
            var item = await categorias.GetAsync(id);
            if (item == null) return NotFound();
            ViewBag.ErrorMessage = error;
            return View(item);
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
            await categorias.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (EsErrorApi(ex))
        {
            // D-06: el API no responde 409; la FK es ON DELETE CASCADE y las protegidas responden 400 con su mensaje.
            return ManejaErrorApi(ex) is { } salida
                ? salida
                : RedirectToAction(nameof(Eliminar), new { id, error = MensajePara(ex) });
        }
    }
}
