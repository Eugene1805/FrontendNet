using System.ComponentModel.DataAnnotations;
using FrontendNet.Validation;

namespace FrontendNet.Models;

/// <summary>Form model for admin user editing via PUT /api/users/:email.</summary>
public class UserEditModel
{
    [Display(Name = "Id")]
    public string Id { get; set; } = string.Empty;

    [Display(Name = "Correo electrónico")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Perfil")]
    public string Profile { get; set; } = string.Empty;

    [Display(Name = "Estado de vendedor")]
    public string? SellerStatus { get; set; }

    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [StringLength(120, MinimumLength = 1, ErrorMessage = "El campo {0} debe tener entre {2} y {1} caracteres.")]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [Display(Name = "Estado")]
    public string Status { get; set; } = UserStatuses.Active;

    [SecurePassword]
    [DataType(DataType.Password)]
    [Display(Name = "Nueva contraseña (opcional)")]
    public string? Password { get; set; }

    public static UserEditModel FromUser(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        Profile = user.Profile,
        SellerStatus = user.SellerStatus,
        Name = user.Name,
        Status = user.Status
    };
}
