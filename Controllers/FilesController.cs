using FrontendNet.Models;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrontendNet;

[Authorize(Roles = "ADMIN,Administrador")]
public class FilesController(FilesClientService files, IConfiguration configuration) : AppController
{
    private long MaxBytes => configuration.GetValue("Archivos:MaxBytes", 2097152L);
    private string[] Extensiones => configuration.GetSection("Archivos:Extensiones").Get<string[]>() ?? [".jpg", ".jpeg"];

    /// <summary>Valida antes de enviar; el API sigue siendo el árbitro final (D-07).</summary>
    private string? ValidaArchivo(IFormFile archivo)
    {
        if (archivo == null || archivo.Length == 0)
            return "No se ha seleccionado ningún archivo.";
        if (archivo.Length > MaxBytes)
            return $"El archivo de {archivo.Length / 1024} KB supera el tamaño máximo permitido ({MaxBytes / 1024} KB).";
        var safeFileName = Path.GetFileName(archivo.FileName);
        var extension = Path.GetExtension(safeFileName).ToLowerInvariant();
        if (!Extensiones.Contains(extension))
            return $"El archivo {safeFileName} no tiene una extensión permitida ({string.Join(", ", Extensiones)}).";
        if (archivo.ContentType != "image/jpeg" && archivo.ContentType != "image/jpg")
            return $"El tipo de contenido '{archivo.ContentType}' no está permitido; solo se aceptan imágenes JPEG.";

        // SEC-06: Validación de magic bytes para evitar polyglots / archivos maliciosos camuflados
        using var stream = archivo.OpenReadStream();
        Span<byte> header = stackalloc byte[3];
        var read = stream.Read(header);
        if (read < 3 || header[0] != 0xFF || header[1] != 0xD8 || header[2] != 0xFF)
            return "El archivo no contiene un formato de imagen JPEG válido.";

        return null;
    }

    public async Task<IActionResult> Index(int page = 1)
    {
        try
        {
            var items = await files.GetAsync() ?? [];
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
            var item = await files.GetAsync(id);
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
        return View("Edit");
    }

    [HttpPost]
    [ActionName("Crear")]
    public async Task<IActionResult> CreateAsync(Upload itemToCreate)
    {
        if (ModelState.IsValid)
        {
            var error = ValidaArchivo(itemToCreate.Cover);
            if (error != null)
            {
                ModelState.AddModelError("Cover", error);
                return View("Create", itemToCreate);
            }
            try
            {
                await files.PostAsync(itemToCreate);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (IsApiError(ex))
            {
                var result = HandleApiError(ex, "Cover");
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
            FileRecord? itemToEdit = await files.GetAsync(id);
            if (itemToEdit == null) return NotFound();
            ViewBag.FileId = itemToEdit.FileId;
            ViewBag.Name = itemToEdit.Name;
            return View("Create");
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return ErrorGet(ex);
        }
    }

    [HttpPost]
    [ActionName("Editar")]
    public async Task<IActionResult> EditAsync(int id, Upload itemToEdit)
    {
        if (itemToEdit == null) return NotFound();
        if (id != itemToEdit.FileId) return NotFound();
        ViewBag.FileId = itemToEdit.FileId;
        ViewBag.Name = itemToEdit.Name;
        if (ModelState.IsValid)
        {
            var error = ValidaArchivo(itemToEdit.Cover);
            if (error != null)
            {
                ModelState.AddModelError("Cover", error);
                return View("Edit", itemToEdit);
            }
            try
            {
                await files.PutAsync(itemToEdit);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (IsApiError(ex))
            {
                var result = HandleApiError(ex, "Cover");
                if (result != null) return result;
            }
        }
        return View("Edit", itemToEdit);
    }

    [ActionName("Eliminar")]
    public async Task<IActionResult> Delete(int id, string? error = null)
    {
        try
        {
            var itemToDelete = await files.GetAsync(id);
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
            await files.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return HandleApiError(ex) is { } result
                ? result
                : RedirectToAction("Eliminar", new { id, error = GetMessageFor(ex) });
        }
    }
}
