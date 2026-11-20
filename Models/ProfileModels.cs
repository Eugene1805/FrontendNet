using System.ComponentModel.DataAnnotations;
using FrontendNet.Validation;

namespace FrontendNet.Models;

/// <summary>Response returned by GET and PUT /api/users/me.</summary>
public class UserProfile
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Profile { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? StoreName { get; set; }
    public string? SellerStatus { get; set; }
}

/// <summary>Safe session and profile data displayed on the Profile view (never exposes the raw JWT).</summary>
public class ProfileSessionViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Profile { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? StoreName { get; set; }
    public string? SellerStatus { get; set; }
    public DateTimeOffset? ExpiresUtc { get; set; }
    public string? Issuer { get; set; }
    public string? Audience { get; set; }
    public string? RemainingTime { get; set; }
}

public class ProfileEditModel
{
    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [StringLength(120, MinimumLength = 1, ErrorMessage = "El campo {0} debe tener entre {2} y {1} caracteres.")]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    [StringLength(20, ErrorMessage = "El campo {0} admite un máximo de {1} caracteres.")]
    [RegularExpression(@"^\+?[0-9][0-9 ()-]{5,19}$", ErrorMessage = "Ingrese un número de teléfono válido.")]
    [Display(Name = "Teléfono (opcional)")]
    public string? PhoneNumber { get; set; }

    [StringLength(120, MinimumLength = 1, ErrorMessage = "El campo {0} debe tener entre {2} y {1} caracteres.")]
    [Display(Name = "Nombre de la tienda")]
    public string? StoreName { get; set; }
}

public class ChangePasswordModel
{
    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña actual")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [SecurePassword]
    [DataType(DataType.Password)]
    [Display(Name = "Nueva contraseña")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [Compare(nameof(NewPassword), ErrorMessage = "La confirmación no coincide con la nueva contraseña.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirmar nueva contraseña")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
