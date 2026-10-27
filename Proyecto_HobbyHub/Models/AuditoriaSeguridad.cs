namespace Proyecto_HobbyHub.Models;

public class AuditoriaSeguridad
{
    public long IdAuditoria { get; set; }
    public int? IdActor { get; set; }
    public int? IdObjetivo { get; set; }
    public string Accion { get; set; } = string.Empty;
    public bool Exito { get; set; }
    public DateTime FechaUtc { get; set; }
    public string? CorrelacionId { get; set; }
    public string? TipoRecurso { get; set; }
    public string? IdRecurso { get; set; }
    // Deliberately no arbitrary payload, email, token, password, IP or request URL.
}
