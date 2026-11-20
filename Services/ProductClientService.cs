using System.Net.Http.Headers;
using FrontendNet.Helpers;
using FrontendNet.Models;

namespace FrontendNet.Services;

public class ProductClientService(HttpClient client)
{
    public async Task<List<Product>?> GetAsync(string? search = null)
    {
        var url = string.IsNullOrWhiteSpace(search)
            ? "api/products"
            : $"api/products?s={UrlHelper.Encode(search.Trim())}";
        return await client.GetFromJsonAsync<List<Product>>(url);
    }

    public async Task<Product?> GetAsync(int id) =>
        await client.GetFromJsonAsync<Product>($"api/products/{id}");

    public async Task<Product?> CreateAsync(ProductFormModel model)
    {
        using var response = await client.PostAsJsonAsync("api/products", ProductRequest.CreatePayload(model));
        return await response.Content.ReadFromJsonAsync<Product>();
    }

    public async Task UpdateAsync(int id, ProductFormModel model)
    {
        using var response = await client.PutAsJsonAsync($"api/products/{id}", ProductRequest.UpdatePayload(model));
    }

    public async Task DeleteAsync(int id)
    {
        using var response = await client.DeleteAsync($"api/products/{id}");
    }

    public async Task AddCategoryAsync(int productId, int categoryId)
    {
        using var response = await client.PostAsJsonAsync($"api/products/{productId}/categories", new { categoryId });
    }

    public async Task RemoveCategoryAsync(int productId, int categoryId)
    {
        using var response = await client.DeleteAsync($"api/products/{productId}/categories/{categoryId}");
    }

    public async Task<ProductImageResponse?> UploadImageAsync(int productId, IFormFile image)
    {
        var safeFileName = Path.GetFileName(image.FileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
            safeFileName = "product.jpg";

        using var memoryStream = new MemoryStream();
        await image.CopyToAsync(memoryStream);

        using var fileContent = new ByteArrayContent(memoryStream.ToArray());
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(image.ContentType);

        using var form = new MultipartFormDataContent();
        form.Add(fileContent, "image", safeFileName);

        using var response = await client.PutAsync($"api/products/{productId}/image", form);
        return await response.Content.ReadFromJsonAsync<ProductImageResponse>();
    }

    public async Task DeleteImageAsync(int productId)
    {
        using var response = await client.DeleteAsync($"api/products/{productId}/image");
    }
}
