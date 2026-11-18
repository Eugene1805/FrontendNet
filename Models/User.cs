using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FrontendNet.Helpers;
namespace FrontendNet.Models;

public class User
{
    [Display(Name = "Id")]
    public string? Id { get; set; }
    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "Name")]
    public required string Name { get; set; }
    [Required(ErrorMessage = "The {0} field is required.")]
    [EmailAddress(ErrorMessage = "The {0} field is not a valid email address.")]
    [Display(Name = "Email")]
    public required string Email { get; set; }
    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "Role")]
    public required string Profile { get; set; }
    [JsonIgnore]
    public string Role { get => Profile; set => Profile = value; }
    [Display(Name = "Protected")]
    [JsonConverter(typeof(NumericBooleanConverter))]
    public string? Status { get; set; }
    public string? SellerStatus { get; set; }
    [JsonIgnore]
    public bool Protected => false;
    [MinLength(6, ErrorMessage = "The {0} field must be at least {1} characters.")]
    [DataType(DataType.Password)]
    [Display(Name = "New password (optional)")]
    public string? Password { get; set; }
}
