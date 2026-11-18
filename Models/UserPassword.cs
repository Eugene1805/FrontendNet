using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace FrontendNet.Models;

public class UserPassword
{
    [Required(ErrorMessage = "The {0} field is required.")]
    [EmailAddress(ErrorMessage = "The {0} field is not a valid email address.")]
    [Display(Name = "Email")]
    public required string Email { get; set; }
    [Required(ErrorMessage = "The {0} field is required.")]
    [MinLength(6, ErrorMessage = "The {0} field must be at least {1} characters.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public required string Password { get; set; }
    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "Name")]
    public required string Name { get; set; }
    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "Role")]
    public required string Profile { get; set; }
    [JsonIgnore]
    public string Role { get => Profile; set => Profile = value; }
}