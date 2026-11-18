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
    [Required(ErrorMessage = "The {0} field is required.")]
    [StringLength(100, ErrorMessage = "The {0} field admits a maximum of {1} characters.")]
    [Display(Name = "Name")]
    public string Name { get; set; } = "";
}

public class ChangePassword
{
    [Required(ErrorMessage = "The {0} field is required.")]
    [DataType(DataType.Password)]
    [Display(Name = "Current password")]
    public string CurrentPassword { get; set; } = "";
    [Required(ErrorMessage = "The {0} field is required.")]
    [MinLength(6, ErrorMessage = "The {0} field must be at least {1} characters.")]
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string NewPassword { get; set; } = "";
    [Required(ErrorMessage = "The {0} field is required.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Confirmation does not match new password.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm new password")]
    public string Confirmation { get; set; } = "";
}
