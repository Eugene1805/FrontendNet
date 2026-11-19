using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace FrontendNet.Models;

public class Product
{
    [Display(Name = "Id")]
    public int? ProductId { get; set; }
    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [Display(Name = "Título")]
    public required string Title { get; set; }
    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [DataType(DataType.MultilineText)]
    [Display(Name = "Descripción")]
    public string Description { get; set; } = "Sin descripción";
    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [DataType(DataType.Currency)]
    [RegularExpression(@"^\d+(\.\d{0,2})?$", ErrorMessage = "El valor del campo debe ser un precio válido.")]
    [DisplayFormat(DataFormatString = "{0:C}", ApplyFormatInEditMode = false)]
    [Display(Name = "Precio")]
    public decimal Price
    { get; set; }
    // Se lee del API como "fileId"; JsonSerializerDefaults.Web es insensible a mayúsculas.
    // Para escribir se usa ProductRequest.
    public string? SellerId { get; set; }
    public int Stock { get; set; }
    public string? ImagePublicId { get; set; }
    public string? ImageUrl { get; set; }
    [JsonIgnore]
    public int? FileId { get; set; }
    public ICollection<Category>? Categories { get; set; }

}
