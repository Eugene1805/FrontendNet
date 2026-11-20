using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FrontendNet.Helpers;

namespace FrontendNet.Models;

public static class OrderStatus
{
    public const string Pending = "PENDING";
    public const string Preparing = "PREPARING";
    public const string Shipped = "SHIPPED";
    public const string Delivered = "DELIVERED";
    public const string Canceled = "CANCELED";

    public static readonly string[] All = [Pending, Preparing, Shipped, Delivered, Canceled];

    /// <summary>Allowed transitions for seller/admin staff.</summary>
    public static IReadOnlyList<string> GetNextStatusesForStaff(string? status) => status?.ToUpperInvariant() switch
    {
        Pending => [Preparing, Canceled],
        Preparing => [Shipped, Canceled],
        Shipped => [Delivered],
        _ => []
    };

    public static bool CanBuyerCancel(string? status) =>
        string.Equals(status, Pending, StringComparison.OrdinalIgnoreCase);
}

public class OrderSummary
{
    [Display(Name = "Pedido")]
    public string Id { get; set; } = string.Empty;

    [Display(Name = "Comprador")]
    public string BuyerId { get; set; } = string.Empty;

    [Display(Name = "Vendedor")]
    public string SellerId { get; set; } = string.Empty;

    public string? CheckoutId { get; set; }

    [Display(Name = "Estado")]
    public string Status { get; set; } = OrderStatus.Pending;

    [Display(Name = "Total")]
    public string Total { get; set; } = "0.00";

    [Display(Name = "Dirección de envío")]
    public AddressSnapshot? DeliveryAddressSnapshot { get; set; }

    [Display(Name = "Fecha de creación")]
    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    [JsonIgnore]
    public decimal TotalValue => DisplayFormatter.ParseDecimal(Total);
}

public class Order : OrderSummary
{
    public List<OrderItem> Items { get; set; } = [];
    public List<OrderHistoryEntry> History { get; set; } = [];
}

public class OrderItem
{
    public string? Id { get; set; }
    public string? OrderId { get; set; }
    public int ProductId { get; set; }
    public string ProductTitle { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string UnitPrice { get; set; } = "0.00";

    [JsonIgnore]
    public decimal UnitPriceValue => DisplayFormatter.ParseDecimal(UnitPrice);

    [JsonIgnore]
    public decimal SubtotalValue => UnitPriceValue * Quantity;
}

public class OrderHistoryEntry
{
    public string? Id { get; set; }
    public string? OrderId { get; set; }
    public string? PreviousStatus { get; set; }
    public string NewStatus { get; set; } = string.Empty;
    public string? ChangedByUserId { get; set; }
    public string? Reason { get; set; }
    public DateTime OccurredAt { get; set; }
}

public sealed class CheckoutQuoteResponse
{
    public string CheckoutId { get; set; } = string.Empty;
    public string Quote { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public AddressSnapshot? Address { get; set; }
    public List<CheckoutQuotedOrder> Orders { get; set; } = [];
}

public sealed class CheckoutQuotedOrder
{
    public string SellerId { get; set; } = string.Empty;
    public string Total { get; set; } = "0.00";
    public List<CheckoutQuotedItem> Items { get; set; } = [];

    [JsonIgnore]
    public decimal TotalValue => DisplayFormatter.ParseDecimal(Total);
}

public sealed class CheckoutQuotedItem
{
    public int ProductId { get; set; }
    public string SellerId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Price { get; set; } = "0.00";
    public int Quantity { get; set; }

    [JsonIgnore]
    public decimal PriceValue => DisplayFormatter.ParseDecimal(Price);

    [JsonIgnore]
    public decimal SubtotalValue => PriceValue * Quantity;
}

public sealed class CheckoutConfirmResponse
{
    public bool Replayed { get; set; }
    public List<Order> Orders { get; set; } = [];
}

public sealed class CheckoutViewModel
{
    public ShoppingCart Cart { get; set; } = new();
    public List<Address> Addresses { get; set; } = [];
    public string? SelectedAddressId { get; set; }
    public CheckoutQuoteResponse? Quote { get; set; }
    public string? QuoteError { get; set; }
}
