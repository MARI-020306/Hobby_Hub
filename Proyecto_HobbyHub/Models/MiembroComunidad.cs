namespace Proyecto_HobbyHub.Models;

public class MiembroComunidad
{
    public int IdComunidad { get; set; }

    public int IdUsuario { get; set; }

    public DateTime FechaUnion { get; set; }

    public virtual Comunidade Comunidad { get; set; } = null!;

    public virtual Usuario Usuario { get; set; } = null!;
}
