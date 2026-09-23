using System;
using System.Collections.Generic;

namespace Proyecto_HobbyHub.Models;

public partial class Comentario
{
    public int IdComentario { get; set; }

    public string Contenido { get; set; } = null!;

    public int IdUsuario { get; set; }

    public int IdPublicacion { get; set; }

    public string Estado { get; set; } = null!;

    public DateTime FechaComentario { get; set; }

    public virtual Publicacione IdPublicacionNavigation { get; set; } = null!;

    public virtual Usuario IdUsuarioNavigation { get; set; } = null!;

    public virtual ICollection<Reporte> Reportes { get; set; } = new List<Reporte>();
}
