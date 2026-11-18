using FrontendNet.Models;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrontendNet;

[Authorize(Roles = "ADMIN,Administrador")]
public class AuditLogController(AuditLogClientService auditLog) : AppController
{
    public async Task<IActionResult> IndexAsync(int page = 1)
    {
        try
        {
            var items = await auditLog.GetAsync() ?? [];
            // Más reciente primero; el API devuelve todo sin límite (X-12).
            return View("Index", Paginate(items.OrderByDescending(b => b.Date), page));
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return ErrorGet(ex);
        }
    }
}
