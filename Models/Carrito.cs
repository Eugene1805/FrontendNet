using System.ComponentModel.DataAnnotations;
namespace FrontendNet.Models;

/// <summary>CarritoResponse del contrato propuesto (docs/planeacion_frontend.md §5.1).</summary>
public class Carrito
{
    public List<CarritoItem> Items { get; set; } = [];
    public int TotalArticulos { get; set; }
    public decimal Total { get; set; }
}

public class CarritoItem
{
    public int ProductoId { get; set; }
    public string Titulo { get; set; } = "";
    [DisplayFormat(DataFormatString = "{0:C}")]
    public decimal Precio { get; set; }
    public int? ArchivoId { get; set; }
    public int Cantidad { get; set; }
    [DisplayFormat(DataFormatString = "{0:C}")]
    public decimal Subtotal { get; set; }
}
