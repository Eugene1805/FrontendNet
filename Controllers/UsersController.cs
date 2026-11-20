using System.Security.Claims;
using FrontendNet.Helpers;
using FrontendNet.Models;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FrontendNet;

[Authorize(Roles = Roles.Admin)]
public class UsersController(
    UsersClientService users,
    AuthClientService auth,
    ISessionTokenManager tokenManager) : AppController
{
    public async Task<IActionResult> Index(int page = 1)
    {
        try
        {
            var items = await users.GetAsync() ?? [];
            return View("Index", Paginate(items, page));
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return await HandleGetErrorAsync(ex);
        }
    }

    public async Task<IActionResult> Details(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return NotFound();

        try
        {
            var item = await users.GetAsync(email);
            if (item is null) return NotFound();
            return View("Details", item);
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return await HandleGetErrorAsync(ex);
        }
    }

    public async Task<IActionResult> Edit(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return NotFound();

        try
        {
            var item = await users.GetAsync(email);
            if (item is null) return NotFound();

            var isSelf = IsCurrentUser(item.Email);
            ViewBag.IsSelf = isSelf;
            ViewBag.CanEdit = !item.IsAdmin || isSelf;
            PopulateStatusDropDown(item.Status);

            return View("Edit", UserEditModel.FromUser(item));
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return await HandleGetErrorAsync(ex);
        }
    }

    [HttpPost]
    public async Task<IActionResult> EditAsync(UserEditModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Email)) return NotFound();

        var isSelf = IsCurrentUser(model.Email);
        ViewBag.IsSelf = isSelf;

        User? existing;
        try
        {
            existing = await users.GetAsync(model.Email);
            if (existing is null) return NotFound();
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return await HandleGetErrorAsync(ex);
        }

        if (existing.IsAdmin && !isSelf)
            return Forbid();

        ViewBag.CanEdit = true;
        model.Id = existing.Id;
        model.Profile = existing.Profile;
        model.SellerStatus = existing.SellerStatus;

        if (isSelf && !string.Equals(model.Status, UserStatuses.Active, StringComparison.OrdinalIgnoreCase))
            ModelState.AddModelError(nameof(model.Status), "No puedes suspender ni deshabilitar tu propia cuenta.");

        if (ModelState.IsValid)
        {
            var passwordChanged = !string.IsNullOrEmpty(model.Password);
            var payload = new UserUpdateRequest
            {
                Name = model.Name.Trim(),
                Status = isSelf ? null : model.Status,
                Password = passwordChanged ? model.Password : null
            };

            try
            {
                await users.UpdateAsync(model.Email, payload);

                if (isSelf && passwordChanged)
                {
                    await auth.SignOutLocalAsync();
                    TempData["Success"] = "Contraseña actualizada. Inicia sesión nuevamente.";
                    return RedirectToAction("Index", "Auth");
                }

                if (isSelf)
                    await tokenManager.UpdateUserNameAsync(HttpContext, payload.Name!);

                TempData["Success"] = "Usuario actualizado correctamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (IsApiError(ex))
            {
                var result = await HandleApiErrorAsync(ex, nameof(model.Name));
                if (result is not null) return result;
            }
        }

        PopulateStatusDropDown(model.Status);
        model.Password = null;
        return View("Edit", model);
    }

    public async Task<IActionResult> Delete(string email, string? error = null)
    {
        if (string.IsNullOrWhiteSpace(email)) return NotFound();

        try
        {
            var item = await users.GetAsync(email);
            if (item is null) return NotFound();

            ViewData["ErrorMessage"] = error;
            ViewBag.CanDelete = !item.IsAdmin && !IsCurrentUser(item.Email);
            return View("Delete", item);
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return await HandleGetErrorAsync(ex);
        }
    }

    [HttpPost]
    [ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmedAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return NotFound();
        if (IsCurrentUser(email)) return Forbid();

        try
        {
            await users.DeleteAsync(email);
            TempData["Success"] = "Usuario eliminado correctamente.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            var result = await HandleApiErrorAsync(ex);
            if (result is not null) return result;
            return RedirectToAction(nameof(Delete), new { email, error = GetUserMessage(ex) });
        }
    }

    [HttpPost]
    public async Task<IActionResult> ApproveSellerAsync(string userId, string email)
    {
        try
        {
            await users.ApproveSellerAsync(userId);
            TempData["Success"] = "Cuenta de vendedor aprobada correctamente.";
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            var result = await HandleApiErrorAsync(ex);
            if (result is not null) return result;
            TempData["Error"] = GetUserMessage(ex);
        }

        return RedirectToAction(nameof(Details), new { email });
    }

    [HttpPost]
    public async Task<IActionResult> RejectSellerAsync(string userId, string email, string? rejectionReason)
    {
        var trimmed = rejectionReason?.Trim() ?? string.Empty;
        if (trimmed.Length is < 1 or > 255)
        {
            TempData["Error"] = "Indique el motivo de rechazo (entre 1 y 255 caracteres).";
            return RedirectToAction(nameof(Details), new { email });
        }

        try
        {
            await users.RejectSellerAsync(userId, trimmed);
            TempData["Success"] = "Solicitud de vendedor rechazada.";
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            var result = await HandleApiErrorAsync(ex);
            if (result is not null) return result;
            TempData["Error"] = GetUserMessage(ex);
        }

        return RedirectToAction(nameof(Details), new { email });
    }

    [HttpPost]
    public async Task<IActionResult> SuspendSellerAsync(string userId, string email, string? reason)
    {
        var trimmed = reason?.Trim() ?? string.Empty;
        if (trimmed.Length is < 1 or > 255)
        {
            TempData["Error"] = "Indique el motivo de suspensión (entre 1 y 255 caracteres).";
            return RedirectToAction(nameof(Details), new { email });
        }

        try
        {
            await users.SuspendSellerAsync(userId, trimmed);
            TempData["Success"] = "Cuenta de vendedor suspendida.";
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            var result = await HandleApiErrorAsync(ex);
            if (result is not null) return result;
            TempData["Error"] = GetUserMessage(ex);
        }

        return RedirectToAction(nameof(Details), new { email });
    }

    private bool IsCurrentUser(string email) =>
        string.Equals(User.FindFirstValue(ClaimTypes.Email), email, StringComparison.OrdinalIgnoreCase);

    private void PopulateStatusDropDown(string? selectedStatus)
    {
        var items = UserStatuses.All
            .Select(s => new SelectListItem(DisplayFormatter.UserStatusLabel(s), s))
            .ToList();
        ViewBag.UserStatuses = new SelectList(items, "Value", "Text", selectedStatus);
    }
}
