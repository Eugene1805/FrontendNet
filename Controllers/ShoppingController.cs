using FrontendNet.Models;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FrontendNet;

[Authorize(Roles = "BUYER,Usuario")]
public class ShoppingController(ProductClientService products, CategoryClientService categories) : AppController
{
    // Record (propiedades) y no tupla: SelectList localiza "Valor"/"Texto" por reflexión sobre propiedades.
    public record OrderOption(string Valor, string Texto);

    public static readonly OrderOption[] Ordenes =
    [
        new("titulo", "Título (A-Z)"),
        new("precio_asc", "Precio: menor a mayor"),
        new("precio_desc", "Precio: mayor a menor"),
    ];

    /// <summary>
    /// Catálogo con filtro por categoría, orden y paginación. El API solo filtra por texto (?s=),
    /// así que categoría, orden y paginación se aplican aquí (planeacion 5.4).
    /// </summary>
    public async Task<IActionResult> Index(string? s, int? categoryId, string? order, int page = 1)
    {
        try
        {
            var items = await products.GetAsync(s) ?? [];
            IEnumerable<Product> filtrada = items;
            if (categoryId is > 0)
                filtrada = filtrada.Where(p => p.Categories?.Any(c => c.CategoryId == categoryId) == true);
            filtrada = order switch
            {
                "precio_asc" => filtrada.OrderBy(p => p.Price),
                "precio_desc" => filtrada.OrderByDescending(p => p.Price),
                "titulo" => filtrada.OrderBy(p => p.Title, StringComparer.CurrentCultureIgnoreCase),
                _ => filtrada
            };

            var listadoCategorias = await categories.GetAsync() ?? [];
            ViewBag.Categories = new SelectList(listadoCategorias, "CategoryId", "Name", categoryId);
            ViewBag.Ordenes = new SelectList(Ordenes, "Valor", "Texto", order);
            ViewBag.search = s;
            return View("Index", Paginate(filtrada, page));
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return ErrorGet(ex);
        }
    }

    [ActionName("Detalle")]
    public async Task<IActionResult> Details(int id)
    {
        try
        {
            var item = await products.GetAsync(id);
            if (item == null) return NotFound();
            return View("Details", item);
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return ErrorGet(ex);
        }
    }
}
