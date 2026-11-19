using System.ComponentModel.DataAnnotations;
namespace FrontendNet.Models;

/// <summary>Datos de sesión que se muestran en Perfil (nunca el JWT completo).</summary>
public class ProfileSession
{
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public string Profile { get; set; } = "";
    [System.Text.Json.Serialization.JsonIgnore]
    public string Role { get => Profile; set => Profile = value; }
    public DateTimeOffset? ExpiraUtc { get; set; }
    public string? Emisor { get; set; }
    public string? Audiencia { get; set; }
    public string? RemainingTime { get; set; }
}

public class ProfileName
{
    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [StringLength(100, ErrorMessage = "El campo {0} admite un máximo de {1} caracteres.")]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = "";
}

public class ChangePassword
{
    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña actual")]
    public string CurrentPassword { get; set; } = "";
    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [MinLength(6, ErrorMessage = "El campo {0} debe tener un mínimo de {1} caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña nueva")]
    public string NewPassword { get; set; } = "";
    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [Compare(nameof(NewPassword), ErrorMessage = "La confirmación no coincide con la contraseña nueva.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirmar contraseña nueva")]
    public string Confirmation { get; set; } = "";
}
