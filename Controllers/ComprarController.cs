using FrontendNet.Models;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FrontendNet;

[Authorize(Roles = "Usuario")]
public class ComprarController(ProductoClientService productos, CategoriaClientService categorias) : AppController
{
    // Record (propiedades) y no tupla: SelectList localiza "Valor"/"Texto" por reflexión sobre propiedades.
    public record OpcionOrden(string Valor, string Texto);

    public static readonly OpcionOrden[] Ordenes =
    [
        new("titulo", "Título (A-Z)"),
        new("precio_asc", "Precio: menor a mayor"),
        new("precio_desc", "Precio: mayor a menor"),
    ];

    /// <summary>
    /// Catálogo con filtro por categoría, orden y paginación. El API solo filtra por texto (?s=),
    /// así que categoría, orden y paginación se aplican aquí (planeacion 5.4).
    /// </summary>
    public async Task<IActionResult> Index(string? s, int? categoriaid, string? orden, int pagina = 1)
    {
        try
        {
            var lista = await productos.GetAsync(s) ?? [];
            IEnumerable<Producto> filtrada = lista;
            if (categoriaid is > 0)
                filtrada = filtrada.Where(p => p.Categorias?.Any(c => c.CategoriaId == categoriaid) == true);
            filtrada = orden switch
            {
                "precio_asc" => filtrada.OrderBy(p => p.Precio),
                "precio_desc" => filtrada.OrderByDescending(p => p.Precio),
                "titulo" => filtrada.OrderBy(p => p.Titulo, StringComparer.CurrentCultureIgnoreCase),
                _ => filtrada
            };

            var listadoCategorias = await categorias.GetAsync() ?? [];
            ViewBag.Categorias = new SelectList(listadoCategorias, "CategoriaId", "Nombre", categoriaid);
            ViewBag.Ordenes = new SelectList(Ordenes, "Valor", "Texto", orden);
            ViewBag.search = s;
            return View(Pagina(filtrada, pagina));
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
            var item = await productos.GetAsync(id);
            if (item == null) return NotFound();
            return View(item);
        }
        catch (Exception ex) when (EsErrorApi(ex))
        {
            return ErrorGet(ex);
        }
    }
}
