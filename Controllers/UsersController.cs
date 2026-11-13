using System.Security.Claims;
using FrontendNet.Models;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FrontendNet;

[Authorize(Roles = "ADMIN")]
public class UsersController(UsersClientService users, RolesClientService roles) : AppController
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
            return ErrorGet(ex);
        }
    }

    [ActionName("Detalle")]
    public async Task<IActionResult> Details(string id)
    {
        try
        {
            var item = await users.GetAsync(id);
            if (item == null) return NotFound();
            return View("Details", item);
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return ErrorGet(ex);
        }
    }

    [ActionName("Crear")]
    public async Task<IActionResult> Create()
    {
        await RolesDropDownListAsync();
        return View("Create");
    }

    [HttpPost]
    [ActionName("Crear")]
    public async Task<IActionResult> CreateAsync(UserPassword itemToCreate)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await users.PostAsync(itemToCreate);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (IsApiError(ex))
            {
                // 409: el correo ya está registrado → se informa con el mensaje del API.
                var result = HandleApiError(ex, "Email");
                if (result != null) return result;
            }
        }
        await RolesDropDownListAsync(itemToCreate.Role);
        return View("Create", itemToCreate);
    }

    [HttpGet("BUYERs/[action]/{email}")]
    [ActionName("Editar")]
    public async Task<IActionResult> EditAsync(string email)
    {
        try
        {
            var itemToEdit = await users.GetAsync(email);
            if (itemToEdit == null) return NotFound();
            ViewBag.PuedeEditar = PuedeModificar(itemToEdit.Email);
            await RolesDropDownListAsync(itemToEdit.Role);
            return View("Edit", itemToEdit);
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return ErrorGet(ex);
        }
    }

    [HttpPost("BUYERs/[action]/{email}")]
    [ActionName("Editar")]
    public async Task<IActionResult> EditAsync(string email, User itemToEdit)
    {
        if (email != itemToEdit.Email) return NotFound();
        // El frontend no es control de seguridad (DD-17), pero evita el intento obvio.
        if (!PuedeModificar(email)) return Forbid();
        if (ModelState.IsValid)
        {
            try
            {
                await users.PutAsync(itemToEdit);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (IsApiError(ex))
            {
                var result = HandleApiError(ex, "Nombre");
                if (result != null) return result;
            }
        }
        ViewBag.PuedeEditar = true;
        await RolesDropDownListAsync(itemToEdit.Role);
        return View("Edit", itemToEdit);
    }

    [ActionName("Eliminar")]
    public async Task<IActionResult> Delete(string id, string? error = null)
    {
        try
        {
            var itemToDelete = await users.GetAsync(id);
            if (itemToDelete == null) return NotFound();
            ViewData["ErrorMessage"] = error;
            // Ni el usuario actual ni los protegidos pueden eliminarse (D-05).
            ViewBag.PuedeEditar = PuedeModificar(itemToDelete.Email) && !itemToDelete.Protected;
            return View("Delete", itemToDelete);
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return ErrorGet(ex);
        }
    }

    [HttpPost]
    [ActionName("Eliminar")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!PuedeModificar(id)) return Forbid();
        try
        {
            await users.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            // 403: usuario protegido → se muestra el mensaje del API.
            return HandleApiError(ex) is { } result
                ? result
                : RedirectToAction("Eliminar", new { id, error = GetMessageFor(ex) });
        }
    }

    private bool PuedeModificar(string email) =>
        !string.Equals(User.FindFirstValue(ClaimTypes.Email), email, StringComparison.OrdinalIgnoreCase);

    private async Task RolesDropDownListAsync(object? rolSeleccionado = null)
    {
        List<Role> items = [];
        try { items = await roles.GetAsync() ?? []; }
        catch (Exception ex) when (IsApiError(ex)) { ModelState.AddModelError("", GetMessageFor(ex)); }
        ViewBag.Role = new SelectList(items, "Name", "Name", rolSeleccionado);
    }
}
