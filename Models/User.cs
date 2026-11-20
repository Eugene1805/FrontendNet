using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace FrontendNet.Models;

/// <summary>User summary returned by /api/users.</summary>
public class User
{
    [Display(Name = "Id")]
    public string Id { get; set; } = string.Empty;

    [Display(Name = "Nombre")]
    public required string Name { get; set; }

    [Display(Name = "Correo electrónico")]
    public required string Email { get; set; }

    [Display(Name = "Perfil")]
    public required string Profile { get; set; }

    [Display(Name = "Estado")]
    public string Status { get; set; } = UserStatuses.Active;

    [Display(Name = "Estado de vendedor")]
    public string? SellerStatus { get; set; }

    [JsonIgnore]
    public bool IsAdmin => string.Equals(Profile, Roles.Admin, StringComparison.OrdinalIgnoreCase);
}
