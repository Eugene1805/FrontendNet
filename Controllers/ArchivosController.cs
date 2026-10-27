using FrontendNet.Models;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrontendNet;

[Authorize(Roles = "Administrador")]
public class ArchivosController(ArchivosClientService archivos, IConfiguration configuration) : AppController
{
    private long MaxBytes => configuration.GetValue("Archivos:MaxBytes", 2097152L);
    private string[] Extensiones => configuration.GetSection("Archivos:Extensiones").Get<string[]>() ?? [".jpg", ".jpeg"];

    /// <summary>Valida antes de enviar; el API sigue siendo el árbitro final (D-07).</summary>
    private string? ValidaArchivo(IFormFile archivo)
    {
        if (archivo.Length > MaxBytes)
            return $"El archivo de {archivo.Length / 1024} KB supera el tamaño máximo permitido ({MaxBytes / 1024} KB).";
        var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (!Extensiones.Contains(extension))
            return $"El archivo {Path.GetFileName(archivo.FileName)} no tiene una extensión permitida ({string.Join(", ", Extensiones)}).";
        if (archivo.ContentType != "image/jpeg")
            return $"El tipo de contenido '{archivo.ContentType}' no está permitido; solo se aceptan imágenes JPEG.";
        return null;
    }

    public async Task<IActionResult> Index(int pagina = 1)
    {
        try
        {
            var lista = await archivos.GetAsync() ?? [];
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
            var item = await archivos.GetAsync(id);
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
    public async Task<IActionResult> CrearAsync(Upload itemToCreate)
    {
        if (ModelState.IsValid)
        {
            var error = ValidaArchivo(itemToCreate.Portada);
            if (error != null)
            {
                ModelState.AddModelError("Portada", error);
                return View(itemToCreate);
            }
            try
            {
                await archivos.PostAsync(itemToCreate);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (EsErrorApi(ex))
            {
                var salida = ManejaErrorApi(ex, "Portada");
                if (salida != null) return salida;
            }
        }
        return View(itemToCreate);
    }

    public async Task<IActionResult> EditarAsync(int id)
    {
        try
        {
            Archivo? itemToEdit = await archivos.GetAsync(id);
            if (itemToEdit == null) return NotFound();
            ViewBag.ArchivoId = itemToEdit.ArchivoId;
            ViewBag.Nombre = itemToEdit.Nombre;
            return View();
        }
        catch (Exception ex) when (EsErrorApi(ex))
        {
            return ErrorGet(ex);
        }
    }

    [HttpPost]
    public async Task<IActionResult> EditarAsync(int id, Upload itemToEdit)
    {
        if (itemToEdit == null) return NotFound();
        if (id != itemToEdit.ArchivoId) return NotFound();
        ViewBag.ArchivoId = itemToEdit.ArchivoId;
        ViewBag.Nombre = itemToEdit.Nombre;
        if (ModelState.IsValid)
        {
            var error = ValidaArchivo(itemToEdit.Portada);
            if (error != null)
            {
                ModelState.AddModelError("Portada", error);
                return View(itemToEdit);
            }
            try
            {
                await archivos.PutAsync(itemToEdit);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (EsErrorApi(ex))
            {
                var salida = ManejaErrorApi(ex, "Portada");
                if (salida != null) return salida;
            }
        }
        return View(itemToEdit);
    }

    public async Task<IActionResult> Eliminar(int id, string? error = null)
    {
        try
        {
            var itemToDelete = await archivos.GetAsync(id);
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
            await archivos.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (EsErrorApi(ex))
        {
            return ManejaErrorApi(ex) is { } salida
                ? salida
                : RedirectToAction(nameof(Eliminar), new { id, error = MensajePara(ex) });
        }
    }
}
