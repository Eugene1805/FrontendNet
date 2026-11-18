using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace FrontendNet.Models;

public class Product
{
    [Display(Name = "Id")]
    public int? ProductId { get; set; }
    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "Title")]
    public required string Title { get; set; }
    [Required(ErrorMessage = "The {0} field is required.")]
    [DataType(DataType.MultilineText)]
    [Display(Name = "Description")]
    public string Description { get; set; } = "No description";
    [Required(ErrorMessage = "The {0} field is required.")]
    [DataType(DataType.Currency)]
    [RegularExpression(@"^\d+(\.\d{0,2})?$", ErrorMessage = "The field value must be a valid price.")]
    [DisplayFormat(DataFormatString = "{0:C}", ApplyFormatInEditMode = false)]
    [Display(Name = "Price")]
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
