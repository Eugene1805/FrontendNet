using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace FrontendNet.Models;

public class UserPassword
{
    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [EmailAddress(ErrorMessage = "El campo {0} no es correo válido.")]
    [Display(Name = "Correo electrónico")]
    public required string Email { get; set; }
    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [MinLength(6, ErrorMessage = "El campo {0} debe tener un mínimo de {1} caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public required string Password { get; set; }
    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [Display(Name = "Nombre")]
    public required string Name { get; set; }
    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [Display(Name = "Rol")]
    public required string Profile { get; set; }
    [JsonIgnore]
    public string Role { get => Profile; set => Profile = value; }
}