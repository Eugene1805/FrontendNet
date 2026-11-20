using FrontendNet.Models;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrontendNet;

[Authorize(Roles = Roles.Admin)]
public class AuditLogController(AuditLogClientService auditLog) : AppController
{
    public async Task<IActionResult> IndexAsync(int? before = null, int limit = 50)
    {
        var clampedLimit = Math.Clamp(limit, 1, 100);
        try
        {
            var items = await auditLog.GetAsync(clampedLimit, before) ?? [];
            ViewBag.Limit = clampedLimit;
            ViewBag.CurrentBefore = before;
            ViewBag.NextBefore = items.Count == clampedLimit ? items[^1].Id : (int?)null;
            return View("Index", items);
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return await HandleGetErrorAsync(ex);
        }
    }
}
