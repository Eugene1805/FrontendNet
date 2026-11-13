using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FrontendNet.Helpers;
namespace FrontendNet.Models;

public class User
{
    [Display(Name = "Id")]
    public string? Id { get; set; }
    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [Display(Name = "Nombre")]
    public required string Name { get; set; }
    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [EmailAddress(ErrorMessage = "El campo {0} no es un correo válido.")]
    [Display(Name = "Email")]
    public required string Email { get; set; }
    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [Display(Name = "Rol")]
    public required string Profile { get; set; }
    [JsonIgnore]
    public string Role { get => Profile; set => Profile = value; }
    [Display(Name = "Protegido")]
    [JsonConverter(typeof(NumericBooleanConverter))]
    public string? Status { get; set; }
    public string? SellerStatus { get; set; }
    [JsonIgnore]
    public bool Protected => false;
    [MinLength(6, ErrorMessage = "El campo {0} debe tener un mínimo de {1} caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Nueva contraseña (opcional)")]
    public string? Password { get; set; }
}
