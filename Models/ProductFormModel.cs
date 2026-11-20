using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace FrontendNet.Models;

/// <summary>Form model used to create or update a seller product.</summary>
public class ProductFormModel : IValidatableObject
{
    [Display(Name = "Id")]
    public int? ProductId { get; set; }

    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "El campo {0} debe tener entre {2} y {1} caracteres.")]
    [Display(Name = "Título")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [StringLength(65535, ErrorMessage = "El campo {0} admite un máximo de {1} caracteres.")]
    [DataType(DataType.MultilineText)]
    [Display(Name = "Descripción")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [Range(typeof(decimal), "0.01", "9999999999.99", ErrorMessage = "El precio debe ser mayor a 0 (entre 0.01 y 9,999,999,999.99).")]
    [RegularExpression(@"^\d{1,10}(\.\d{1,2})?$", ErrorMessage = "El precio debe tener hasta 2 decimales.")]
    [Display(Name = "Precio")]
    public decimal Price { get; set; }

    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [Range(1, int.MaxValue, ErrorMessage = "Las existencias deben ser mayores a 0 (máximo 2,147,483,647).")]
    [Display(Name = "Existencias")]
    public int Stock { get; set; }

    [Display(Name = "Categoría inicial (opcional)")]
    public int? CategoryId { get; set; }

    [Display(Name = "Imagen del producto (opcional: JPEG, PNG o WebP, máx. 2 MB)")]
    public IFormFile? Image { get; set; }

    public string? CurrentImageUrl { get; set; }

    public static ProductFormModel FromProduct(Product product) => new()
    {
        ProductId = product.ProductId,
        Title = product.Title,
        Description = product.Description ?? string.Empty,
        Price = product.Price,
        Stock = product.Stock,
        CurrentImageUrl = product.ImageUrl
    };

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var trimmed = Description?.Trim() ?? string.Empty;
        var utf8Bytes = Encoding.UTF8.GetByteCount(trimmed);
        if (utf8Bytes is < 1 or > 65535)
        {
            yield return new ValidationResult(
                "El campo Descripción debe contener entre 1 y 65535 bytes UTF-8.",
                [nameof(Description)]);
        }
    }
}
