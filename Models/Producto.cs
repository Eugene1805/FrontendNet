using System.ComponentModel.DataAnnotations;

namespace FrontendNet.Models;

public class Producto
{
    [Display(Name = "Id")]
    public int? ProductoId { get; set; }
    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    public required string Titulo { get; set; }
    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [DataType(DataType.MultilineText)]
    public string Descripcion { get; set; } = "Sin descripción";
    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [DataType(DataType.Currency)]
    [RegularExpression(@"^\d+(\.\d{0,2})?$", ErrorMessage = "El valor del campo debe ser un precio válido.")]
    [DisplayFormat(DataFormatString = "{0:C}", ApplyFormatInEditMode = false)]
    [Display(Name = "Precio")]
    public decimal Precio
    { get; set; }
    // Se lee del API como "archivoid"; JsonSerializerDefaults.Web es insensible a mayúsculas.
    // Para escribir se usa ProductoRequest.
    [Display(Name = "Portada")]
    public int? ArchivoId
    {
        get; set;
    }
    public ICollection<Categoria>? Categorias { get; set; }

}
