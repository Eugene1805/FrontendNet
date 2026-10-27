using System.Text.Json.Serialization;
namespace FrontendNet.Models;

/// <summary>Cuerpo de escritura de POST/PUT api/productos. El API lee "archivoid" en minúsculas.</summary>
public class ProductoRequest
{
    public required string Titulo { get; set; }
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
    [JsonPropertyName("archivoid")]
    public int? ArchivoId { get; set; }

    public static ProductoRequest De(Producto p) => new()
    {
        Titulo = p.Titulo,
        Descripcion = p.Descripcion,
        Precio = p.Precio,
        ArchivoId = p.ArchivoId
    };
}
