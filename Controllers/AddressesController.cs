using FrontendNet.Models;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrontendNet;

[Authorize(Roles = Roles.Buyer)]
public class AddressesController(AddressesClientService addresses) : AppController
{
    public async Task<IActionResult> Index()
    {
        try
        {
            return View("Index", await addresses.GetAsync());
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return await HandleGetErrorAsync(ex);
        }
    }

    public IActionResult Create(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        return View("Create", new AddressFormModel());
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync(AddressFormModel model, string? returnUrl = null)
    {
        ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        if (!ModelState.IsValid)
            return View("Create", model);

        try
        {
            var created = await addresses.CreateAsync(model);
            TempData["Success"] = "Dirección guardada correctamente.";
            if (Url.IsLocalUrl(returnUrl))
            {
                if (created is not null && returnUrl!.Contains("/Orders/Checkout", StringComparison.OrdinalIgnoreCase))
                    return RedirectToAction("Checkout", "Orders", new { addressId = created.Id });
                return LocalRedirect(returnUrl!);
            }
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            var result = await HandleApiErrorAsync(ex);
            if (result is not null) return result;
        }

        return View("Create", model);
    }

    public async Task<IActionResult> Edit(string id)
    {
        try
        {
            var items = await addresses.GetAsync();
            var target = items.FirstOrDefault(a => a.Id == id);
            if (target is null) return NotFound();

            return View("Edit", AddressFormModel.FromAddress(target));
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return await HandleGetErrorAsync(ex);
        }
    }

    [HttpPost]
    public async Task<IActionResult> EditAsync(string id, AddressFormModel model)
    {
        if (!string.Equals(id, model.Id, StringComparison.Ordinal))
            return NotFound();

        if (!ModelState.IsValid)
            return View("Edit", model);

        try
        {
            await addresses.UpdateAsync(id, model);
            TempData["Success"] = "Dirección actualizada correctamente.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            var result = await HandleApiErrorAsync(ex);
            if (result is not null) return result;
        }

        return View("Edit", model);
    }

    public async Task<IActionResult> Delete(string id, string? error = null)
    {
        try
        {
            var items = await addresses.GetAsync();
            var target = items.FirstOrDefault(a => a.Id == id);
            if (target is null) return NotFound();

            ViewData["ErrorMessage"] = error;
            return View("Delete", target);
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return await HandleGetErrorAsync(ex);
        }
    }

    [HttpPost]
    [ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmedAsync(string id)
    {
        try
        {
            await addresses.DeleteAsync(id);
            TempData["Success"] = "Dirección eliminada correctamente.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            var result = await HandleApiErrorAsync(ex);
            if (result is not null) return result;
            return RedirectToAction(nameof(Delete), new { id, error = GetUserMessage(ex) });
        }
    }

    [HttpPost]
    public async Task<IActionResult> SetDefaultAsync(string id)
    {
        try
        {
            await addresses.SetDefaultAsync(id);
            TempData["Success"] = "Dirección predeterminada actualizada.";
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            var result = await HandleApiErrorAsync(ex);
            if (result is not null) return result;
            TempData["Error"] = GetUserMessage(ex);
        }

        return RedirectToAction(nameof(Index));
    }
}
