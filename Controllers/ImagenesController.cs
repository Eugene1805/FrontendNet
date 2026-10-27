using FrontendNet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrontendNet;

/// <summary>
/// Proxy de imágenes (DD-05): el navegador pide /api/archivos/{id} al frontend y este la obtiene del API.
/// Evita depender de CORP/CORS del API, no expone su URL y funciona en LAN con una sola dirección.
/// </summary>
[AllowAnonymous]
public class ImagenesController(ArchivosClientService archivos) : Controller
{
    [HttpGet("api/archivos/{id:int}")]
    public async Task<IActionResult> Imagen(int id)
    {
        try
        {
            var imagen = await archivos.GetBinarioAsync(id);
            // Solo se reenvían imágenes: cualquier otro tipo de contenido se rechaza.
            if (imagen == null || !imagen.Value.Mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return NotFound();
            Response.Headers.CacheControl = "public, max-age=300";
            return File(imagen.Value.Contenido, imagen.Value.Mime);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return StatusCode(StatusCodes.Status502BadGateway);
        }
    }
}
