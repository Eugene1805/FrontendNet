using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace FrontendNet.Models;

public static class OrderStatus
{
    public const string Pending = "PENDING";
    public const string Preparing = "PREPARING";
    public const string Shipped = "SHIPPED";
    public const string Delivered = "DELIVERED";
    public const string Canceled = "CANCELED";
    public const string Pendiente = Pending;
    public const string Pagado = Preparing;
    public const string Enviado = Shipped;
    public const string Entregado = Delivered;
    public const string Cancelado = Canceled;

    public static readonly string[] All = [Pending, Preparing, Shipped, Delivered, Canceled];
    public static readonly string[] Todos = All;

    /// <summary>Transiciones válidas (DD-04): Pendiente → Pagado → Enviado → Entregado; Cancelado antes de enviar.</summary>
    public static IReadOnlyList<string> GetNextStatuses(string status) => status switch
    {
        Pending => [Preparing, Canceled],
        Preparing => [Shipped, Canceled],
        Shipped => [Delivered],
        _ => []
    };
}

/// <summary>Body for POST api/orders.</summary>
public class NewOrder
{
    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(300, MinimumLength = 10, ErrorMessage = "The {0} field must be between {2} and {1} characters.")]
    [DataType(DataType.MultilineText)]
    [Display(Name = "Shipping address")]
    public string AddressId
    {
        get => _direccion;
        set => _direccion = value?.Trim() ?? "";
    }
    private string _direccion = "";
    [JsonIgnore]
    public string DireccionEnvio { get => AddressId; set => AddressId = value; }
    [JsonIgnore]
    public string MetodoPago { get; set; } = "simulated";
}

public class OrderSummary
{
    [Display(Name = "Order")]
    public string OrderId { get; set; } = "";
    [JsonIgnore]
    public string PedidoId { get => OrderId; set => OrderId = value; }
    public DateTime Date { get; set; }
    public string Status { get; set; } = OrderStatus.Pending;
    [JsonIgnore]
    public string Estado { get => Status; set => Status = value; }
    public string BuyerId { get; set; } = "";
    [JsonIgnore]
    public string User { get => BuyerId; set => BuyerId = value; }
    [DisplayFormat(DataFormatString = "{0:C}")]
    public string Total { get; set; } = "0.00";
}

public class Order : OrderSummary
{
    [Display(Name = "Shipping address")]
    public string? DeliveryAddressSnapshot { get; set; }
    [JsonIgnore]
    public string DireccionEnvio { get => DeliveryAddressSnapshot ?? ""; set => DeliveryAddressSnapshot = value; }
    public List<OrderItem> Items { get; set; } = [];
}

public class OrderItem
{
    public int ProductId { get; set; }
    public string Title { get; set; } = "";
    [DisplayFormat(DataFormatString = "{0:C}")]
    public string UnitPrice { get; set; } = "0.00";
    public int Quantity { get; set; }
    [DisplayFormat(DataFormatString = "{0:C}")]
    public string Subtotal { get; set; } = "0.00";
}
