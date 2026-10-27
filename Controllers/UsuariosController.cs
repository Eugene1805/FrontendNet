using System.Security.Claims;
using FrontendNet.Models;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FrontendNet;

[Authorize(Roles = "Administrador")]
public class UsuariosController(UsuariosClientService usuarios, RolesClientService roles) : AppController
{
    public async Task<IActionResult> Index(int pagina = 1)
    {
        try
        {
            var lista = await usuarios.GetAsync() ?? [];
            return View(Pagina(lista, pagina));
        }
        catch (Exception ex) when (EsErrorApi(ex))
        {
            return ErrorGet(ex);
        }
    }

    public async Task<IActionResult> Detalle(string id)
    {
        try
        {
            var item = await usuarios.GetAsync(id);
            if (item == null) return NotFound();
            return View(item);
        }
        catch (Exception ex) when (EsErrorApi(ex))
        {
            return ErrorGet(ex);
        }
    }

    public async Task<IActionResult> Crear()
    {
        await RolesDropDownListAsync();
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> CrearAsync(UsuarioPwd itemToCreate)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await usuarios.PostAsync(itemToCreate);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (EsErrorApi(ex))
            {
                // 409: el correo ya está registrado → se informa con el mensaje del API.
                var salida = ManejaErrorApi(ex, "Email");
                if (salida != null) return salida;
            }
        }
        await RolesDropDownListAsync(itemToCreate.Rol);
        return View(itemToCreate);
    }

    [HttpGet("[controller]/[action]/{email}")]
    public async Task<IActionResult> EditarAsync(string email)
    {
        try
        {
            var itemToEdit = await usuarios.GetAsync(email);
            if (itemToEdit == null) return NotFound();
            ViewBag.PuedeEditar = PuedeModificar(itemToEdit.Email);
            await RolesDropDownListAsync(itemToEdit.Rol);
            return View(itemToEdit);
        }
        catch (Exception ex) when (EsErrorApi(ex))
        {
            return ErrorGet(ex);
        }
    }

    [HttpPost("[controller]/[action]/{email}")]
    public async Task<IActionResult> EditarAsync(string email, Usuario itemToEdit)
    {
        if (email != itemToEdit.Email) return NotFound();
        // El frontend no es control de seguridad (DD-17), pero evita el intento obvio.
        if (!PuedeModificar(email)) return Forbid();
        if (ModelState.IsValid)
        {
            try
            {
                await usuarios.PutAsync(itemToEdit);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (EsErrorApi(ex))
            {
                var salida = ManejaErrorApi(ex, "Nombre");
                if (salida != null) return salida;
            }
        }
        ViewBag.PuedeEditar = true;
        await RolesDropDownListAsync(itemToEdit.Rol);
        return View(itemToEdit);
    }

    public async Task<IActionResult> Eliminar(string id, string? error = null)
    {
        try
        {
            var itemToDelete = await usuarios.GetAsync(id);
            if (itemToDelete == null) return NotFound();
            ViewData["ErrorMessage"] = error;
            // Ni el usuario actual ni los protegidos pueden eliminarse (D-05).
            ViewBag.PuedeEditar = PuedeModificar(itemToDelete.Email) && !itemToDelete.Protegido;
            return View(itemToDelete);
        }
        catch (Exception ex) when (EsErrorApi(ex))
        {
            return ErrorGet(ex);
        }
    }

    [HttpPost]
    public async Task<IActionResult> Eliminar(string id)
    {
        if (!PuedeModificar(id)) return Forbid();
        try
        {
            await usuarios.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (EsErrorApi(ex))
        {
            // 403: usuario protegido → se muestra el mensaje del API.
            return ManejaErrorApi(ex) is { } salida
                ? salida
                : RedirectToAction(nameof(Eliminar), new { id, error = MensajePara(ex) });
        }
    }

    private bool PuedeModificar(string email) =>
        !string.Equals(User.FindFirstValue(ClaimTypes.Email), email, StringComparison.OrdinalIgnoreCase);

    private async Task RolesDropDownListAsync(object? rolSeleccionado = null)
    {
        List<Rol> listado = [];
        try { listado = await roles.GetAsync() ?? []; }
        catch (Exception ex) when (EsErrorApi(ex)) { ModelState.AddModelError("", MensajePara(ex)); }
        ViewBag.Rol = new SelectList(listado, "Nombre", "Nombre", rolSeleccionado);
    }
}
