using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FrontendNet.Helpers;

namespace FrontendNet.Models;

public class Category
{
    [Display(Name = "Id")]
    public int? CategoryId { get; set; }

    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [StringLength(120, MinimumLength = 1, ErrorMessage = "El campo {0} debe tener entre {2} y {1} caracteres.")]
    [Display(Name = "Nombre")]
    public required string Name { get; set; }

    [Display(Name = "Protegida")]
    [JsonConverter(typeof(NumericBooleanConverter))]
    public bool Protected { get; set; }
}
