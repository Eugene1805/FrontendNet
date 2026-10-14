using System.ComponentModel.DataAnnotations;
namespace FrontendNet.Models;

public class ProductoCategoria
{
    [Display(Name = "Categoria")]
    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    public int? CategoriaId { get; set; }
    public string? Nombre { get; set; }
    public Producto? Producto { get; set; }
}