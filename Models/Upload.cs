using System.ComponentModel.DataAnnotations;
namespace FrontendNet.Models;
public class Upload
{
    [Display(Name = "ID del archivo")]
    public int? ArchivoId { get; set; }
    [Display(Name = "Nombre")]
    public string? Nombre { get; set; }
    [Display(Name = "Portada")]
    [Required(ErrorMessage = "El campo {0} es obligatorio.")]
    [DataType(DataType.Upload)]
    public required IFormFile Portada { get; set; }
}