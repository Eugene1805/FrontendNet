using FrontendNet.Models;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FrontendNet;

[Authorize(Roles = Roles.Buyer)]
public class ShoppingController(ProductClientService products, CategoryClientService categories) : AppController
{
    public record SortOption(string Value, string Text);

    public static readonly SortOption[] SortOptions =
    [
        new("title", "Título (A-Z)"),
        new("price_asc", "Precio: menor a mayor"),
        new("price_desc", "Precio: mayor a menor")
    ];

    public async Task<IActionResult> Index(string? s, int? categoryId, string? order, int page = 1)
    {
        var search = s?.Trim();
        if (search?.Length > 255)
        {
            TempData["Error"] = "La búsqueda admite un máximo de 255 caracteres.";
            search = search[..255];
        }

        try
        {
            var items = await products.GetAsync(search) ?? [];
            IEnumerable<Product> filtered = items;

            if (categoryId is > 0)
                filtered = filtered.Where(p => p.Categories?.Any(c => c.CategoryId == categoryId) == true);

            filtered = order switch
            {
                "price_asc" => filtered.OrderBy(p => p.Price),
                "price_desc" => filtered.OrderByDescending(p => p.Price),
                "title" => filtered.OrderBy(p => p.Title, StringComparer.CurrentCultureIgnoreCase),
                _ => filtered
            };

            var categoryList = await categories.GetAsync() ?? [];
            ViewBag.Categories = new SelectList(categoryList, "CategoryId", "Name", categoryId);
            ViewBag.SortOptions = new SelectList(SortOptions, "Value", "Text", order);
            ViewBag.Search = search;

            return View("Index", Paginate(filtered, page));
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return await HandleGetErrorAsync(ex);
        }
    }

    public async Task<IActionResult> Details(int id)
    {
        try
        {
            var item = await products.GetAsync(id);
            if (item is null) return NotFound();
            return View("Details", item);
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return await HandleGetErrorAsync(ex);
        }
    }
}
