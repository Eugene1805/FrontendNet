using System.Net;
using System.Security.Claims;
using FrontendNet.Helpers;
using FrontendNet.Models;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrontendNet;

[Authorize]
public class ProfileController(
    ProfileClientService profile,
    AuthClientService auth,
    ISessionTokenManager tokenManager) : AppController
{
    public async Task<IActionResult> IndexAsync()
    {
        UserProfile? me;
        try
        {
            me = await profile.GetMeAsync();
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return await HandleGetErrorAsync(ex);
        }

        if (!string.IsNullOrWhiteSpace(me?.SellerStatus) &&
            !string.Equals(
                me.SellerStatus,
                User.FindFirstValue(SessionTokenManager.SellerStatusClaimType),
                StringComparison.OrdinalIgnoreCase))
        {
            await tokenManager.UpdateSellerStatusAsync(HttpContext, me.SellerStatus);
        }

        var viewModel = new ProfileSessionViewModel
        {
            Id = me?.Id ?? GetCurrentUserId() ?? string.Empty,
            Email = me?.Email ?? User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            Name = me?.Name ?? User.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
            Profile = me?.Profile ?? User.FindFirstValue(ClaimTypes.Role) ?? string.Empty,
            Status = me?.Status ?? UserStatuses.Active,
            PhoneNumber = me?.PhoneNumber,
            StoreName = me?.StoreName,
            SellerStatus = me?.SellerStatus ?? User.FindFirstValue(SessionTokenManager.SellerStatusClaimType)
        };

        return View("Index", viewModel);
    }

    public async Task<IActionResult> Edit()
    {
        try
        {
            var me = await profile.GetMeAsync();
            if (me is null) return NotFound();

            ViewBag.IsSeller = string.Equals(me.Profile, Roles.Seller, StringComparison.OrdinalIgnoreCase);
            return View("Edit", new ProfileEditModel
            {
                Name = me.Name,
                PhoneNumber = me.PhoneNumber,
                StoreName = me.StoreName
            });
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return await HandleGetErrorAsync(ex);
        }
    }

    [HttpPost]
    public async Task<IActionResult> EditAsync(ProfileEditModel model)
    {
        var isSeller = User.IsInRole(Roles.Seller);
        ViewBag.IsSeller = isSeller;

        if (isSeller && string.IsNullOrWhiteSpace(model.StoreName))
            ModelState.AddModelError(nameof(model.StoreName), "El campo Nombre de la tienda es obligatorio.");

        if (!ModelState.IsValid)
            return View("Edit", model);

        try
        {
            var updated = await profile.UpdateMeAsync(model.Name, model.PhoneNumber, model.StoreName, isSeller);
            await tokenManager.UpdateUserNameAsync(HttpContext, updated?.Name ?? model.Name.Trim());
            TempData["Success"] = "Perfil actualizado correctamente.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            var result = await HandleApiErrorAsync(ex, nameof(model.Name));
            if (result is not null) return result;
        }

        return View("Edit", model);
    }

    public IActionResult ChangePassword() =>
        View("ChangePassword", new ChangePasswordModel());

    [HttpPost]
    public async Task<IActionResult> ChangePasswordAsync(ChangePasswordModel model)
    {
        if (!ModelState.IsValid)
            return View("ChangePassword", new ChangePasswordModel());

        try
        {
            await profile.ChangePasswordAsync(model);
            await auth.SignOutLocalAsync();
            TempData["Success"] = "Contraseña actualizada. Inicia sesión nuevamente con tu nueva contraseña.";
            return RedirectToAction("Index", "Auth");
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            if (GetStatusCode(ex) == HttpStatusCode.Unauthorized)
            {
                ModelState.AddModelError(nameof(model.CurrentPassword), "La contraseña actual es incorrecta.");
            }
            else
            {
                var result = await HandleApiErrorAsync(ex, nameof(model.CurrentPassword));
                if (result is not null) return result;
            }
        }

        // Never echo submitted passwords back into the rendered form.
        return View("ChangePassword", new ChangePasswordModel());
    }
}
