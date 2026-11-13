using System.ComponentModel.DataAnnotations;
namespace FrontendNet.Models;

public class ProductCategory
{
    [Display(Name = "Categoria")]
    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    public int? CategoryId { get; set; }
    public string? Name { get; set; }
    public Product? Product { get; set; }
}