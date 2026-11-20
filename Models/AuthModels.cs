using System.ComponentModel.DataAnnotations;
using FrontendNet.Validation;

namespace FrontendNet.Models;

public class RegisterRequestModel : IValidatableObject
{
    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [Display(Name = "Tipo de cuenta")]
    public string Profile { get; set; } = Roles.Buyer;

    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [StringLength(120, MinimumLength = 1, ErrorMessage = "El campo {0} debe tener entre {2} y {1} caracteres.")]
    [Display(Name = "Nombre completo")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [EmailAddress(ErrorMessage = "El campo {0} no es un correo electrónico válido.")]
    [StringLength(254, ErrorMessage = "El campo {0} admite un máximo de {1} caracteres.")]
    [Display(Name = "Correo electrónico")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [SecurePassword]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [Compare(nameof(Password), ErrorMessage = "La confirmación no coincide con la contraseña.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirmar contraseña")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [StringLength(20, ErrorMessage = "El campo {0} admite un máximo de {1} caracteres.")]
    [RegularExpression(@"^\+?[0-9][0-9 ()-]{5,19}$", ErrorMessage = "Ingrese un número de teléfono válido.")]
    [Display(Name = "Teléfono (opcional)")]
    public string? PhoneNumber { get; set; }

    [StringLength(120, ErrorMessage = "El campo {0} admite un máximo de {1} caracteres.")]
    [Display(Name = "Nombre de la tienda")]
    public string? StoreName { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var normalizedProfile = Profile?.Trim().ToUpperInvariant();
        if (normalizedProfile != Roles.Buyer && normalizedProfile != Roles.Seller)
        {
            yield return new ValidationResult(
                "Seleccione un tipo de cuenta válido (Comprador o Vendedor).",
                [nameof(Profile)]);
        }

        if (normalizedProfile == Roles.Seller && string.IsNullOrWhiteSpace(StoreName))
        {
            yield return new ValidationResult(
                "El campo Nombre de la tienda es obligatorio para cuentas de vendedor.",
                [nameof(StoreName)]);
        }
    }
}

public class VerifyRegistrationModel
{
    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [EmailAddress(ErrorMessage = "El campo {0} no es un correo electrónico válido.")]
    [StringLength(254, ErrorMessage = "El campo {0} admite un máximo de {1} caracteres.")]
    [Display(Name = "Correo electrónico")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "El código debe tener exactamente 6 dígitos numéricos.")]
    [Display(Name = "Código de verificación")]
    public string Code { get; set; } = string.Empty;
}

public class VerifyRegistrationResult
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Profile { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? SellerStatus { get; set; }
}

public class ForgotPasswordModel
{
    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [EmailAddress(ErrorMessage = "El campo {0} no es un correo electrónico válido.")]
    [StringLength(254, ErrorMessage = "El campo {0} admite un máximo de {1} caracteres.")]
    [Display(Name = "Correo electrónico")]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordModel
{
    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [EmailAddress(ErrorMessage = "El campo {0} no es un correo electrónico válido.")]
    [StringLength(254, ErrorMessage = "El campo {0} admite un máximo de {1} caracteres.")]
    [Display(Name = "Correo electrónico")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "El código debe tener exactamente 6 dígitos numéricos.")]
    [Display(Name = "Código de recuperación")]
    public string Code { get; set; } = string.Empty;

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

public class ApiMessageResponse
{
    public string? Message { get; set; }
}
