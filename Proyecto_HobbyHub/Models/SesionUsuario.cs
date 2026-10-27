namespace Proyecto_HobbyHub.Models;

public class SesionUsuario
{
    public Guid IdSesion { get; set; }
    public int IdUsuario { get; set; }
    public Guid VersionSeguridad { get; set; }
    public DateTime FechaCreacionUtc { get; set; }
    public DateTime ExpiraUtc { get; set; }
    public DateTime? RevocadaUtc { get; set; }
    public Usuario Usuario { get; set; } = null!;
}
