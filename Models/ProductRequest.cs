using System.Globalization;

namespace FrontendNet.Models;

/// <summary>Builds strict JSON payloads expected by POST and PUT /api/products.</summary>
public static class ProductRequest
{
    public static Dictionary<string, object?> CreatePayload(ProductFormModel model)
    {
        var payload = new Dictionary<string, object?>
        {
            ["title"] = model.Title.Trim(),
            ["description"] = model.Description.Trim(),
            ["price"] = model.Price.ToString("0.00", CultureInfo.InvariantCulture),
            ["stock"] = model.Stock
        };

        if (model.CategoryId is > 0)
            payload["categoryId"] = model.CategoryId.Value;

        return payload;
    }

    public static object UpdatePayload(ProductFormModel model) => new
    {
        title = model.Title.Trim(),
        description = model.Description.Trim(),
        price = model.Price.ToString("0.00", CultureInfo.InvariantCulture),
        stock = model.Stock
    };
}
