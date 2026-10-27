namespace Proyecto_HobbyHub.Models;

public class RecuperacionCuenta
{
    public Guid IdRecuperacion { get; set; }
    public int IdUsuario { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public Guid VersionSeguridad { get; set; }
    public DateTime CreadaUtc { get; set; }
    public DateTime ExpiraUtc { get; set; }
    public DateTime? ConsumidaUtc { get; set; }
    public Usuario Usuario { get; set; } = null!;
}
