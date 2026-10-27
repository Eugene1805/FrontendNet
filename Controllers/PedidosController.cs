using FrontendNet.Models;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FrontendNet;

/// <summary>
/// Usuario: checkout, sus pedidos y cancelar. Administrador: todos los pedidos y cambio de estado.
/// El API (o la simulación) decide qué pedidos puede ver cada rol; un pedido ajeno responde 404.
/// </summary>
[Authorize(Roles = "Usuario,Administrador")]
public class PedidosController(IPedidosService pedidos, ICarritoService carrito) : AppController
{
    public async Task<IActionResult> Index(string? estado, int pagina = 1)
    {
        try
        {
            var lista = await pedidos.GetAsync(estado);
            ViewBag.Estado = estado;
            ViewBag.Estados = new SelectList(EstadoPedido.Todos, estado);
            return View(Pagina(lista, pagina));
        }
        catch (Exception ex) when (EsErrorApi(ex))
        {
            return ErrorGet(ex);
        }
    }

    public async Task<IActionResult> Detalle(int id)
    {
        try
        {
            var pedido = await pedidos.GetAsync(id);
            if (pedido == null) return NotFound();
            return View(pedido);
        }
        catch (Exception ex) when (EsErrorApi(ex))
        {
            return ErrorGet(ex);
        }
    }

    [Authorize(Roles = "Usuario")]
    public async Task<IActionResult> Checkout()
    {
        try
        {
            var actual = await carrito.GetAsync();
            if (actual.Items.Count == 0)
            {
                TempData["Error"] = "El carrito está vacío.";
                return RedirectToAction("Index", "Carrito");
            }
            ViewBag.Carrito = actual;
            return View(new PedidoNuevo());
        }
        catch (Exception ex) when (EsErrorApi(ex))
        {
            return ErrorGet(ex);
        }
    }

    [HttpPost]
    [Authorize(Roles = "Usuario")]
    public async Task<IActionResult> CheckoutAsync(PedidoNuevo model)
    {
        // El único método de pago disponible es el simulado (DD-04); el cliente no decide otra cosa.
        model.MetodoPago = "simulado";
        try
        {
            if (ModelState.IsValid)
            {
                var pedido = await pedidos.CrearAsync(model);
                TempData["Exito"] = $"Pedido #{pedido.PedidoId} creado correctamente.";
                return RedirectToAction(nameof(Detalle), new { id = pedido.PedidoId });
            }
        }
        catch (Exception ex) when (EsErrorApi(ex))
        {
            var salida = ManejaErrorApi(ex);
            if (salida != null) return salida;
        }
        try
        {
            ViewBag.Carrito = await carrito.GetAsync();
        }
        catch (Exception ex) when (EsErrorApi(ex))
        {
            return ErrorGet(ex);
        }
        return View(model);
    }

    [HttpPost]
    [Authorize(Roles = "Usuario")]
    public async Task<IActionResult> Cancelar(int id)
    {
        try
        {
            await pedidos.CancelarAsync(id);
            TempData["Exito"] = "Pedido cancelado.";
        }
        catch (Exception ex) when (EsErrorApi(ex))
        {
            var salida = ManejaErrorApi(ex);
            if (salida != null) return salida;
            TempData["Error"] = MensajePara(ex);
        }
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Estado(int id, string estado)
    {
        try
        {
            await pedidos.CambiaEstadoAsync(id, estado);
            TempData["Exito"] = $"Estado actualizado a {estado}.";
        }
        catch (Exception ex) when (EsErrorApi(ex))
        {
            var salida = ManejaErrorApi(ex);
            if (salida != null) return salida;
            TempData["Error"] = MensajePara(ex);
        }
        return RedirectToAction(nameof(Detalle), new { id });
    }
}
