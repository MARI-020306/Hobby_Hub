using System;
using System.Collections.Generic;

namespace Proyecto_HobbyHub.Models;

public partial class Comunidade
{
    public int IdComunidad { get; set; }

    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    public string? Categoria { get; set; }

    public string? ImagenPortada { get; set; }

    public int IdCreador { get; set; }

    public DateTime FechaCreacion { get; set; }

    public virtual Usuario IdCreadorNavigation { get; set; } = null!;

    public virtual ICollection<Publicacione> Publicaciones { get; set; } = new List<Publicacione>();

    public virtual ICollection<MiembroComunidad> Miembros { get; set; } = new List<MiembroComunidad>();
}
