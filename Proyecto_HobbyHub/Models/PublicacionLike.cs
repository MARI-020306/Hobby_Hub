using System;

namespace Proyecto_HobbyHub.Models;

public partial class PublicacionLike
{
    public int IdLike { get; set; }

    public int IdPublicacion { get; set; }

    public int IdUsuario { get; set; }

    public DateTime FechaLike { get; set; }

    public virtual Publicacione IdPublicacionNavigation { get; set; } = null!;

    public virtual Usuario IdUsuarioNavigation { get; set; } = null!;
}