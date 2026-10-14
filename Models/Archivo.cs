using System.ComponentModel.DataAnnotations;
namespace FrontendNet.Models;

public class Archivo
{
    [Display(Name = "ID del archivo")]
    public int? ArchivoId { get; set; }
    [Display(Name = "MIME")]
    public string? Mime { get; set; }
    public string? Nombre { get; set; }
    [Display(Name = "Tamaño")]
    public int? Size { get; set; }
    [Display(Name = "Repositorio")]
    public bool InDb { get; set; } = true;
}