namespace Proyecto_HobbyHub.Models;

public partial class UsuarioImagen
{
    public int IdImagen { get; set; }

    public int IdUsuario { get; set; }

    public string Url { get; set; } = null!;

    public string Tipo { get; set; } = null!;

    public bool EsPrincipal { get; set; }

    public DateTime FechaSubida { get; set; }

    public virtual Usuario IdUsuarioNavigation { get; set; } = null!;
}
