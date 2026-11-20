using System.ComponentModel.DataAnnotations;

namespace FrontendNet.Models;

public class Login
{
    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [EmailAddress(ErrorMessage = "El campo {0} no es un correo electrónico válido.")]
    [StringLength(254, ErrorMessage = "El campo {0} admite un máximo de {1} caracteres.")]
    [Display(Name = "Correo electrónico")]
    public required string Email { get; set; }

    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public required string Password { get; set; }
}
