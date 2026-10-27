using System.ComponentModel.DataAnnotations;

using System.Text.Json.Serialization;
using FrontendNet.Helpers;
namespace FrontendNet.Models;
public class Categoria
{
    [Display(Name = "Id")]
    public int? CategoriaId { get; set; }
    [Required(ErrorMessage = "El campo {0} es obligatorio." ) ]
    public required string Nombre { get; set; }
    [Display(Name = "Eliminable")]
    [JsonConverter(typeof(BoolNumericoConverter))]
    public bool Protegida { get; set; } = false;
}