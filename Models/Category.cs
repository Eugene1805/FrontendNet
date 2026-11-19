using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FrontendNet.Helpers;
namespace FrontendNet.Models;
public class Category
{
    [Display(Name = "Id")]
    public int? CategoryId { get; set; }
    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [Display(Name = "Nombre")]
    public required string Name { get; set; }
    [Display(Name = "Protegida")]
    [JsonConverter(typeof(NumericBooleanConverter))]
    public bool Protected { get; set; } = false;
}