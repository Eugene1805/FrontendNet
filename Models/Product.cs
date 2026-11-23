using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace FrontendNet.Models;

public class Product
{
    [Display(Name = "Id")]
    public int? ProductId { get; set; }

    public string? SellerId { get; set; }

    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "El campo {0} debe tener entre {2} y {1} caracteres.")]
    [Display(Name = "Título")]
    public required string Title { get; set; }

    [DataType(DataType.MultilineText)]
    [Display(Name = "Descripción")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [DataType(DataType.Currency)]
    [DisplayFormat(DataFormatString = "{0:C}", ApplyFormatInEditMode = false)]
    [Display(Name = "Precio")]
    public decimal Price { get; set; }

    [Display(Name = "Existencias")]
    public int Stock { get; set; }

    public string? ImagePublicId { get; set; }

    [Display(Name = "Imagen")]
    public string? ImageUrl { get; set; }

    [Display(Name = "Estado de imagen")]
    public bool? HasImage { get; set; }

    [JsonIgnore]
    public bool HasEffectiveImage => HasImage ?? !string.IsNullOrWhiteSpace(ImagePublicId);

    public ICollection<Category>? Categories { get; set; }
}
