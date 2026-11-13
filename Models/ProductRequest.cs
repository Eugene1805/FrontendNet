namespace FrontendNet.Models;

/// <summary>Write body for POST/PUT api/products.</summary>
public class ProductRequest
{
    public required string Title { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }

    public static ProductRequest Create(Product p) => new()
    {
        Title = p.Title,
        Description = p.Description,
        Price = p.Price,
    };
}
