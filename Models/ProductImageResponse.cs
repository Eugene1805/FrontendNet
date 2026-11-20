namespace FrontendNet.Models;

/// <summary>Response returned by PUT /api/products/:id/image.</summary>
public class ProductImageResponse
{
    public int ProductId { get; set; }
    public string? ImagePublicId { get; set; }
    public string? ImageUrl { get; set; }
}
