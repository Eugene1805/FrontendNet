using System.ComponentModel.DataAnnotations;

namespace FrontendNet.Models;
public class Categoria
{
    [Display(Name = "Id")]
    public int? CategoriaId { get; set; }
    [Required(ErrorMessage = "El campo {0} es obligatorio." ) ]
    public required string Nombre { get; set; }
    [Display(Name = "Eliminable")]
    public bool Protegida { get; set; } = false;
}