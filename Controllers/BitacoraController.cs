using FrontendNet.Models;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrontendNet;

[Authorize(Roles = "Administrador")]
public class BitacoraController(BitacoraClientService bitacora) : AppController
{
    public async Task<IActionResult> IndexAsync(int pagina = 1)
    {
        try
        {
            var lista = await bitacora.GetAsync() ?? [];
            // Más reciente primero; el API devuelve todo sin límite (X-12).
            return View(Pagina(lista.OrderByDescending(b => b.Fecha), pagina));
        }
        catch (Exception ex) when (EsErrorApi(ex))
        {
            return ErrorGet(ex);
        }
    }
}
