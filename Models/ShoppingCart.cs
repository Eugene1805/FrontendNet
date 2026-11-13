using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json.Serialization;
namespace FrontendNet.Models;

/// <summary>Persistent cart response returned by the real API.</summary>
public class ShoppingCart
{
    public List<CartItem> Items { get; set; } = [];
    public string Subtotal { get; set; } = "0.00";
    [JsonIgnore]
    public int TotalItems => Items.Sum(item => item.Quantity);
    [JsonIgnore]
    public decimal Total => decimal.TryParse(Subtotal, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0m;
}

public class CartItem
{
    public int ProductId { get; set; }
    public string Title { get; set; } = "";
    [DisplayFormat(DataFormatString = "{0:C}")]
    public string? UnitPrice { get; set; }
    [JsonIgnore]
    public decimal Price => decimal.TryParse(UnitPrice, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0m;
    [JsonIgnore]
    public int? FileId => null;
    public int Quantity { get; set; }
    public string? Subtotal { get; set; }
    [JsonIgnore]
    public decimal SubtotalValue => decimal.TryParse(Subtotal, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0m;
    public bool Available { get; set; }
}
