using System.ComponentModel.DataAnnotations;
namespace FrontendNet.Models;

/// <summary>Datos de sesión que se muestran en Perfil (nunca el JWT completo).</summary>
public class PerfilSesion
{
    public string Email { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Rol { get; set; } = "";
    public DateTimeOffset? ExpiraUtc { get; set; }
    public string? Emisor { get; set; }
    public string? Audiencia { get; set; }
    public string? TiempoRestante { get; set; }
}

public class PerfilNombre
{
    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [StringLength(100, ErrorMessage = "El campo {0} admite un máximo de {1} caracteres.")]
    [Display(Name = "Nombre")]
    public string Nombre { get; set; } = "";
}

public class CambioPassword
{
    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña actual")]
    public string PasswordActual { get; set; } = "";
    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [MinLength(6, ErrorMessage = "El campo {0} debe tener un mínimo de {1} caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña nueva")]
    public string PasswordNueva { get; set; } = "";
    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [Compare(nameof(PasswordNueva), ErrorMessage = "La confirmación no coincide con la contraseña nueva.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirmar contraseña nueva")]
    public string Confirmacion { get; set; } = "";
}
