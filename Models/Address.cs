using System.ComponentModel.DataAnnotations;

namespace FrontendNet.Models;

/// <summary>Shipping address returned by /api/addresses.</summary>
public class Address
{
    public string Id { get; set; } = string.Empty;
    public string? BuyerId { get; set; }

    [Display(Name = "Etiqueta")]
    public string? Label { get; set; }

    [Display(Name = "Destinatario")]
    public string RecipientName { get; set; } = string.Empty;

    [Display(Name = "Calle")]
    public string Street { get; set; } = string.Empty;

    [Display(Name = "Número exterior")]
    public string ExteriorNumber { get; set; } = string.Empty;

    [Display(Name = "Número interior")]
    public string? InteriorNumber { get; set; }

    [Display(Name = "Colonia")]
    public string Neighborhood { get; set; } = string.Empty;

    [Display(Name = "Ciudad")]
    public string City { get; set; } = string.Empty;

    [Display(Name = "Estado")]
    public string State { get; set; } = string.Empty;

    [Display(Name = "Código postal")]
    public string PostalCode { get; set; } = string.Empty;

    [Display(Name = "País")]
    public string Country { get; set; } = "MX";

    [Display(Name = "Predeterminada")]
    public bool IsDefault { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public string FormatSingleLine()
    {
        var interior = string.IsNullOrWhiteSpace(InteriorNumber) ? string.Empty : $" Int. {InteriorNumber}";
        return $"{RecipientName} — {Street} {ExteriorNumber}{interior}, {Neighborhood}, {City}, {State}, C.P. {PostalCode}, {Country}";
    }
}

/// <summary>Form model for creating or updating a shipping address.</summary>
public class AddressFormModel
{
    public string? Id { get; set; }

    [StringLength(40, ErrorMessage = "El campo {0} admite un máximo de {1} caracteres.")]
    [RegularExpression(@"^[^\x00-\x1F\x7F]+$", ErrorMessage = "La etiqueta contiene caracteres no permitidos.")]
    [Display(Name = "Etiqueta (opcional)")]
    public string? Label { get; set; }

    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [StringLength(120, MinimumLength = 1, ErrorMessage = "El campo {0} debe tener entre {2} y {1} caracteres.")]
    [RegularExpression(@"^[^\x00-\x1F\x7F]+$", ErrorMessage = "El destinatario contiene caracteres no permitidos.")]
    [Display(Name = "Nombre del destinatario")]
    public string RecipientName { get; set; } = string.Empty;

    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "El campo {0} debe tener entre {2} y {1} caracteres.")]
    [RegularExpression(@"^[^\x00-\x1F\x7F]+$", ErrorMessage = "La calle contiene caracteres no permitidos.")]
    [Display(Name = "Calle")]
    public string Street { get; set; } = string.Empty;

    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [StringLength(20, MinimumLength = 1, ErrorMessage = "El campo {0} debe tener entre {2} y {1} caracteres.")]
    [RegularExpression(@"^[A-Za-z0-9\s#./-]+$", ErrorMessage = "El número exterior solo admite letras, números, espacios y #, ., /, -.")]
    [Display(Name = "Número exterior")]
    public string ExteriorNumber { get; set; } = string.Empty;

    [StringLength(20, ErrorMessage = "El campo {0} admite un máximo de {1} caracteres.")]
    [RegularExpression(@"^[A-Za-z0-9\s#./-]+$", ErrorMessage = "El número interior solo admite letras, números, espacios y #, ., /, -.")]
    [Display(Name = "Número interior (opcional)")]
    public string? InteriorNumber { get; set; }

    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [StringLength(120, MinimumLength = 1, ErrorMessage = "El campo {0} debe tener entre {2} y {1} caracteres.")]
    [RegularExpression(@"^[^\x00-\x1F\x7F]+$", ErrorMessage = "La colonia contiene caracteres no permitidos.")]
    [Display(Name = "Colonia")]
    public string Neighborhood { get; set; } = string.Empty;

    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [StringLength(120, MinimumLength = 1, ErrorMessage = "El campo {0} debe tener entre {2} y {1} caracteres.")]
    [RegularExpression(@"^[^\x00-\x1F\x7F]+$", ErrorMessage = "La ciudad contiene caracteres no permitidos.")]
    [Display(Name = "Ciudad")]
    public string City { get; set; } = string.Empty;

    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [StringLength(120, MinimumLength = 1, ErrorMessage = "El campo {0} debe tener entre {2} y {1} caracteres.")]
    [RegularExpression(@"^[^\x00-\x1F\x7F]+$", ErrorMessage = "El estado contiene caracteres no permitidos.")]
    [Display(Name = "Estado / Entidad")]
    public string State { get; set; } = string.Empty;

    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [StringLength(12, MinimumLength = 3, ErrorMessage = "El campo {0} debe tener entre {2} y {1} caracteres.")]
    [RegularExpression(@"^[A-Za-z0-9][A-Za-z0-9\s-]{1,10}[A-Za-z0-9]$", ErrorMessage = "Ingrese un código postal válido (3 a 12 caracteres alfanuméricos).")]
    [Display(Name = "Código postal")]
    public string PostalCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [StringLength(2, MinimumLength = 2, ErrorMessage = "El código de país debe tener exactamente 2 letras.")]
    [RegularExpression(@"^[A-Za-z]{2}$", ErrorMessage = "El código de país debe tener 2 letras (por ejemplo, MX).")]
    [Display(Name = "País (código de 2 letras)")]
    public string Country { get; set; } = "MX";

    [Display(Name = "Establecer como dirección predeterminada")]
    public bool IsDefault { get; set; }

    public static AddressFormModel FromAddress(Address address) => new()
    {
        Id = address.Id,
        Label = address.Label,
        RecipientName = address.RecipientName,
        Street = address.Street,
        ExteriorNumber = address.ExteriorNumber,
        InteriorNumber = address.InteriorNumber,
        Neighborhood = address.Neighborhood,
        City = address.City,
        State = address.State,
        PostalCode = address.PostalCode,
        Country = address.Country,
        IsDefault = address.IsDefault
    };
}

/// <summary>Address snapshot embedded inside checkout quotes and order records.</summary>
public class AddressSnapshot
{
    public string RecipientName { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string ExteriorNumber { get; set; } = string.Empty;
    public string? InteriorNumber { get; set; }
    public string Neighborhood { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = "MX";

    public string FormatMultiline()
    {
        var interior = string.IsNullOrWhiteSpace(InteriorNumber) ? string.Empty : $" Int. {InteriorNumber}";
        return $"{RecipientName}\n{Street} {ExteriorNumber}{interior}, {Neighborhood}\n{City}, {State}, C.P. {PostalCode}, {Country}";
    }
}
