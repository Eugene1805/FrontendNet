using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FrontendNet.Helpers;
namespace FrontendNet.Models;

public class FileRecord
{
    [Display(Name = "ID del archivo")]
    public int? FileId { get; set; }
    [Display(Name = "MIME")]
    public string? Mime { get; set; }
    public string? Name { get; set; }
    [Display(Name = "Tamaño")]
    public int? Size { get; set; }
    [Display(Name = "Repositorio")]
    [JsonConverter(typeof(NumericBooleanConverter))]
    public bool StoredInDatabase { get; set; } = true;
}