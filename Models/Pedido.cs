using System.ComponentModel.DataAnnotations;
namespace FrontendNet.Models;

public static class EstadoPedido
{
    public const string Pendiente = "Pendiente";
    public const string Pagado = "Pagado";
    public const string Enviado = "Enviado";
    public const string Entregado = "Entregado";
    public const string Cancelado = "Cancelado";

    public static readonly string[] Todos = [Pendiente, Pagado, Enviado, Entregado, Cancelado];

    /// <summary>Transiciones válidas (DD-04): Pendiente → Pagado → Enviado → Entregado; Cancelado antes de enviar.</summary>
    public static IReadOnlyList<string> Siguientes(string estado) => estado switch
    {
        Pendiente => [Pagado, Cancelado],
        Pagado => [Enviado, Cancelado],
        Enviado => [Entregado],
        _ => []
    };
}

/// <summary>Body de POST api/pedidos.</summary>
public class PedidoNuevo
{
    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [StringLength(300, MinimumLength = 10, ErrorMessage = "El campo {0} debe tener entre {2} y {1} caracteres.")]
    [DataType(DataType.MultilineText)]
    [Display(Name = "Dirección de envío")]
    public string DireccionEnvio { get; set; } = "";
    [Display(Name = "Método de pago")]
    public string MetodoPago { get; set; } = "simulado";
}

public class PedidoResumen
{
    [Display(Name = "Pedido")]
    public int PedidoId { get; set; }
    public DateTime Fecha { get; set; }
    public string Estado { get; set; } = EstadoPedido.Pendiente;
    public string Usuario { get; set; } = "";
    [DisplayFormat(DataFormatString = "{0:C}")]
    public decimal Total { get; set; }
}

public class Pedido : PedidoResumen
{
    [Display(Name = "Dirección de envío")]
    public string DireccionEnvio { get; set; } = "";
    public List<PedidoItem> Items { get; set; } = [];
}

public class PedidoItem
{
    public int ProductoId { get; set; }
    public string Titulo { get; set; } = "";
    [DisplayFormat(DataFormatString = "{0:C}")]
    public decimal PrecioUnitario { get; set; }
    public int Cantidad { get; set; }
    [DisplayFormat(DataFormatString = "{0:C}")]
    public decimal Subtotal { get; set; }
}
