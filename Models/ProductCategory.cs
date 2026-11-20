using System.ComponentModel.DataAnnotations;

namespace FrontendNet.Models;

public class ProductCategory
{
    [Display(Name = "Categoría")]
    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    public int? CategoryId { get; set; }

    [Display(Name = "Categoría")]
    public string? Name { get; set; }

    public Product? Product { get; set; }
}
