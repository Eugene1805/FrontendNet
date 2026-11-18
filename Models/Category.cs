using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FrontendNet.Helpers;
namespace FrontendNet.Models;
public class Category
{
    [Display(Name = "Id")]
    public int? CategoryId { get; set; }
    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "Name")]
    public required string Name { get; set; }
    [Display(Name = "Protected")]
    [JsonConverter(typeof(NumericBooleanConverter))]
    public bool Protected { get; set; } = false;
}