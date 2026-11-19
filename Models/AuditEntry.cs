using System.ComponentModel.DataAnnotations;
namespace FrontendNet.Models;

public class AuditEntry
{
    [Display(Name = "ID de la bitácora")]
    public int? AuditLogId { get; set; }
    [Display(Name = "Acción")]
    public string? Action { get; set; }
    [Display(Name = "Elemento Id")]
    public string? ElementId { get; set; }
    [Display(Name = "IP")]
    public string? IP { get; set; }
    [Display(Name = "Usuario")]
    public string? User { get; set; }
    [Display(Name = "Fecha")]
    public DateTime? Date { get; set; }
}