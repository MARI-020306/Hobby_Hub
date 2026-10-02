using System;
using System.Collections.Generic;

namespace Proyecto_HobbyHub.Models;

public partial class Publicacione
{
    public int IdPublicacion { get; set; }

    public string Contenido { get; set; } = null!;

    public string? ImagenUrl { get; set; }

    public int IdUsuario { get; set; }

    public int IdComunidad { get; set; }

    public string Estado { get; set; } = null!;

    public DateTime FechaPublicacion { get; set; }

    public virtual ICollection<Comentario> Comentarios { get; set; } = new List<Comentario>();

    public virtual ICollection<PublicacionLike> PublicacionLikes { get; set; } = new List<PublicacionLike>();

    public virtual Comunidade IdComunidadNavigation { get; set; } = null!;

    public virtual Usuario IdUsuarioNavigation { get; set; } = null!;

    public virtual ICollection<Reporte> Reportes { get; set; } = new List<Reporte>();
}
