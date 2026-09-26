using System;
using System.Collections.Generic;

namespace Proyecto_HobbyHub.Models;

public partial class Usuario
{
    public int IdUsuario { get; set; }

    public string Nombre { get; set; } = null!;

    public string Correo { get; set; } = null!;

    public byte[] Password { get; set; } = null!;

    public int RolId { get; set; }

    public string Estado { get; set; } = null!;

    public string? Celular { get; set; }

    public string? Direccion { get; set; }

    public DateTime FechaRegistro { get; set; }

    public virtual ICollection<Comentario> Comentarios { get; set; } = new List<Comentario>();

    public virtual ICollection<Comunidade> Comunidades { get; set; } = new List<Comunidade>();

    public virtual ICollection<MiembroComunidad> MembresiasComunidad { get; set; } = new List<MiembroComunidad>();

    public virtual ICollection<Publicacione> Publicaciones { get; set; } = new List<Publicacione>();

    public virtual ICollection<Reporte> ReporteIdUsuarioReportaNavigations { get; set; } = new List<Reporte>();

    public virtual ICollection<Reporte> ReporteIdUsuarioReportadoNavigations { get; set; } = new List<Reporte>();

    public virtual ICollection<UsuarioImagen> UsuarioImagenes { get; set; } = new List<UsuarioImagen>();

    public virtual Role Rol { get; set; } = null!;
}
