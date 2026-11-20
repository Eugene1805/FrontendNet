using System.Text.Json.Serialization;
using FrontendNet.Helpers;

namespace FrontendNet.Models;

/// <summary>Persistent cart response returned by /api/cart.</summary>
public class ShoppingCart
{
    public string? Id { get; set; }
    public List<CartItem> Items { get; set; } = [];
    public string Subtotal { get; set; } = "0.00";

    [JsonIgnore]
    public int TotalItems => Items.Sum(item => item.Quantity);

    [JsonIgnore]
    public decimal TotalValue => DisplayFormatter.ParseDecimal(Subtotal);
}

public class CartItem
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public string? Title { get; set; }
    public string? UnitPrice { get; set; }
    public string? Subtotal { get; set; }
    public bool Available { get; set; }

    [JsonIgnore]
    public decimal UnitPriceValue => DisplayFormatter.ParseDecimal(UnitPrice);

    [JsonIgnore]
    public decimal SubtotalValue => DisplayFormatter.ParseDecimal(Subtotal);
}
