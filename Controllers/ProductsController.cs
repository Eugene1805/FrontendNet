using FrontendNet.Helpers;
using FrontendNet.Models;
using FrontendNet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FrontendNet;

[Authorize(Roles = $"{Roles.Seller},{Roles.Admin}")]
public class ProductsController(
    ProductClientService products,
    CategoryClientService categories,
    IConfiguration configuration) : AppController
{
    public async Task<IActionResult> Index(string? s, int page = 1)
    {
        if (IsUnapprovedSeller())
            return View("SellerPending");

        var search = s?.Trim();
        if (search?.Length > 255)
        {
            TempData["Error"] = "La búsqueda admite un máximo de 255 caracteres.";
            search = search[..255];
        }

        try
        {
            var items = await products.GetAsync(search) ?? [];
            if (User.IsInRole(Roles.Seller))
            {
                var currentUserId = GetCurrentUserId();
                items = items
                    .Where(p => string.Equals(p.SellerId, currentUserId, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            ViewBag.CanManage = User.IsInRole(Roles.Seller);
            ViewBag.Search = search;
            return View("Index", Paginate(items, page));
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return await HandleGetErrorAsync(ex);
        }
    }

    public async Task<IActionResult> Details(int id)
    {
        if (IsUnapprovedSeller())
            return View("SellerPending");

        try
        {
            var item = await products.GetAsync(id);
            if (item is null) return NotFound();

            ViewBag.CanManage = User.IsInRole(Roles.Seller) && IsOwner(item);
            return View("Details", item);
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return await HandleGetErrorAsync(ex);
        }
    }

    [Authorize(Roles = Roles.Seller)]
    public async Task<IActionResult> Create()
    {
        if (IsUnapprovedSeller())
            return View("SellerPending");

        await PopulateCategoriesDropDownAsync();
        return View("Create", new ProductFormModel());
    }

    [HttpPost]
    [Authorize(Roles = Roles.Seller)]
    public async Task<IActionResult> CreateAsync(ProductFormModel model)
    {
        if (IsUnapprovedSeller())
            return View("SellerPending");

        if (model.Image is not null)
        {
            var imageError = ImageUploadValidator.Validate(model.Image, configuration);
            if (imageError is not null)
                ModelState.AddModelError(nameof(model.Image), imageError);
        }

        if (ModelState.IsValid)
        {
            try
            {
                var created = await products.CreateAsync(model);
                if (created?.ProductId is { } newProductId && model.Image is not null)
                {
                    try
                    {
                        await products.UploadImageAsync(newProductId, model.Image);
                        TempData["Success"] = "Producto e imagen guardados correctamente.";
                    }
                    catch (Exception imgEx) when (IsApiError(imgEx))
                    {
                        TempData["Error"] =
                            $"El producto se creó correctamente, pero no se pudo subir la imagen: {GetUserMessage(imgEx)}";
                        return RedirectToAction(nameof(Edit), new { id = newProductId });
                    }
                }
                else
                {
                    TempData["Success"] = "Producto creado correctamente.";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (IsApiError(ex))
            {
                var result = await HandleApiErrorAsync(ex);
                if (result is not null) return result;
            }
        }

        await PopulateCategoriesDropDownAsync(model.CategoryId);
        return View("Create", model);
    }

    [Authorize(Roles = Roles.Seller)]
    public async Task<IActionResult> Edit(int id)
    {
        if (IsUnapprovedSeller())
            return View("SellerPending");

        try
        {
            var item = await products.GetAsync(id);
            if (item is null) return NotFound();
            if (!IsOwner(item)) return Forbid();

            return View("Edit", ProductFormModel.FromProduct(item));
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return await HandleGetErrorAsync(ex);
        }
    }

    [HttpPost]
    [Authorize(Roles = Roles.Seller)]
    public async Task<IActionResult> EditAsync(int id, ProductFormModel model)
    {
        if (IsUnapprovedSeller())
            return View("SellerPending");

        if (id != model.ProductId) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                await products.UpdateAsync(id, model);
                TempData["Success"] = "Producto actualizado correctamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (IsApiError(ex))
            {
                var result = await HandleApiErrorAsync(ex);
                if (result is not null) return result;
            }
        }

        try
        {
            var current = await products.GetAsync(id);
            if (current is not null)
                model.CurrentImageUrl = current.ImageUrl;
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            // Keep existing form model if secondary lookup fails.
        }

        return View("Edit", model);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Seller)]
    public async Task<IActionResult> UploadImageAsync(int id, IFormFile? image)
    {
        if (IsUnapprovedSeller())
            return View("SellerPending");

        var validationError = ImageUploadValidator.Validate(image, configuration);
        if (validationError is not null)
        {
            TempData["Error"] = validationError;
            return RedirectToAction(nameof(Edit), new { id });
        }

        try
        {
            await products.UploadImageAsync(id, image!);
            TempData["Success"] = "Imagen del producto actualizada correctamente.";
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            var result = await HandleApiErrorAsync(ex);
            if (result is not null) return result;
            TempData["Error"] = GetUserMessage(ex);
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [Authorize(Roles = Roles.Seller)]
    public async Task<IActionResult> DeleteImageAsync(int id)
    {
        if (IsUnapprovedSeller())
            return View("SellerPending");

        try
        {
            await products.DeleteImageAsync(id);
            TempData["Success"] = "Imagen del producto eliminada correctamente.";
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            var result = await HandleApiErrorAsync(ex);
            if (result is not null) return result;
            TempData["Error"] = GetUserMessage(ex);
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    [Authorize(Roles = Roles.Seller)]
    public async Task<IActionResult> Delete(int id, string? error = null)
    {
        if (IsUnapprovedSeller())
            return View("SellerPending");

        try
        {
            var item = await products.GetAsync(id);
            if (item is null) return NotFound();
            if (!IsOwner(item)) return Forbid();

            ViewData["ErrorMessage"] = error;
            return View("Delete", item);
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return await HandleGetErrorAsync(ex);
        }
    }

    [HttpPost]
    [ActionName("Delete")]
    [Authorize(Roles = Roles.Seller)]
    public async Task<IActionResult> DeleteConfirmedAsync(int id)
    {
        if (IsUnapprovedSeller())
            return View("SellerPending");

        try
        {
            await products.DeleteAsync(id);
            TempData["Success"] = "Producto eliminado correctamente.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            var result = await HandleApiErrorAsync(ex);
            if (result is not null) return result;
            return RedirectToAction(nameof(Delete), new { id, error = GetUserMessage(ex) });
        }
    }

    [Authorize(Roles = Roles.Seller)]
    public async Task<IActionResult> Categories(int id)
    {
        if (IsUnapprovedSeller())
            return View("SellerPending");

        try
        {
            var item = await products.GetAsync(id);
            if (item is null) return NotFound();
            if (!IsOwner(item)) return Forbid();

            ViewData["ProductId"] = item.ProductId;
            return View("Categories", item);
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return await HandleGetErrorAsync(ex);
        }
    }

    [Authorize(Roles = Roles.Seller)]
    public async Task<IActionResult> AddCategory(int id)
    {
        if (IsUnapprovedSeller())
            return View("SellerPending");

        try
        {
            var product = await products.GetAsync(id);
            if (product is null) return NotFound();
            if (!IsOwner(product)) return Forbid();

            await PopulateCategoriesDropDownAsync();
            return View("AddCategory", new ProductCategory { Product = product });
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return await HandleGetErrorAsync(ex);
        }
    }

    [HttpPost]
    [Authorize(Roles = Roles.Seller)]
    public async Task<IActionResult> AddCategoryAsync(int id, int? categoryId)
    {
        if (IsUnapprovedSeller())
            return View("SellerPending");

        Product? product = null;
        try
        {
            product = await products.GetAsync(id);
            if (product is null) return NotFound();
            if (!IsOwner(product)) return Forbid();

            if (categoryId is not > 0)
            {
                ModelState.AddModelError(nameof(ProductCategory.CategoryId), "Seleccione una categoría.");
            }
            else
            {
                var category = await categories.GetAsync(categoryId.Value);
                if (category is null) return NotFound();

                await products.AddCategoryAsync(id, categoryId.Value);
                TempData["Success"] = "Categoría agregada al producto.";
                return RedirectToAction(nameof(Categories), new { id });
            }
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            var result = await HandleApiErrorAsync(ex, nameof(ProductCategory.CategoryId));
            if (result is not null) return result;
        }

        await PopulateCategoriesDropDownAsync(categoryId);
        return View("AddCategory", new ProductCategory { Product = product, CategoryId = categoryId });
    }

    [Authorize(Roles = Roles.Seller)]
    public async Task<IActionResult> RemoveCategory(int id, int categoryId, string? error = null)
    {
        if (IsUnapprovedSeller())
            return View("SellerPending");

        try
        {
            var product = await products.GetAsync(id);
            if (product is null) return NotFound();
            if (!IsOwner(product)) return Forbid();

            var category = await categories.GetAsync(categoryId);
            if (category is null) return NotFound();

            ViewData["ErrorMessage"] = error;
            return View("RemoveCategory", new ProductCategory
            {
                Product = product,
                CategoryId = categoryId,
                Name = category.Name
            });
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            return await HandleGetErrorAsync(ex);
        }
    }

    [HttpPost]
    [ActionName("RemoveCategory")]
    [Authorize(Roles = Roles.Seller)]
    public async Task<IActionResult> RemoveCategoryConfirmedAsync(int id, int categoryId)
    {
        if (IsUnapprovedSeller())
            return View("SellerPending");

        try
        {
            await products.RemoveCategoryAsync(id, categoryId);
            TempData["Success"] = "Categoría removida del producto.";
            return RedirectToAction(nameof(Categories), new { id });
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            var result = await HandleApiErrorAsync(ex);
            if (result is not null) return result;
            return RedirectToAction(nameof(RemoveCategory), new { id, categoryId, error = GetUserMessage(ex) });
        }
    }

    private bool IsOwner(Product product) =>
        string.Equals(product.SellerId, GetCurrentUserId(), StringComparison.OrdinalIgnoreCase);

    private async Task PopulateCategoriesDropDownAsync(object? selectedCategory = null)
    {
        List<Category> items = [];
        try
        {
            items = await categories.GetAsync() ?? [];
        }
        catch (Exception ex) when (IsApiError(ex))
        {
            ModelState.AddModelError(string.Empty, GetUserMessage(ex));
        }

        ViewBag.Categories = new SelectList(items, "CategoryId", "Name", selectedCategory);
    }
}
