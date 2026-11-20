using System.ComponentModel.DataAnnotations;

namespace FrontendNet.Models;

/// <summary>Audit log entry returned by GET /api/audit-logs.</summary>
public class AuditEntry
{
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Display(Name = "Actor (Id)")]
    public string? ActorUserId { get; set; }

    [Display(Name = "Acción")]
    public string? Action { get; set; }

    [Display(Name = "Recurso")]
    public string? TargetType { get; set; }

    [Display(Name = "Id del recurso")]
    public string? TargetId { get; set; }

    [Display(Name = "IP")]
    public string? IpAddress { get; set; }

    [Display(Name = "Resultado")]
    public string? Outcome { get; set; }

    [Display(Name = "Fecha")]
    public DateTime? OccurredAt { get; set; }
}
