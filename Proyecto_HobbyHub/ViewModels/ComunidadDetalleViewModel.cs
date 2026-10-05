using System.ComponentModel.DataAnnotations;

namespace Proyecto_HobbyHub.ViewModels;

public class ComunidadDetalleViewModel
{
    public int IdComunidad { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public string Descripcion { get; set; } = string.Empty;

    public string Categoria { get; set; } = "General";

    public string? ImagenPortada { get; set; }

    public string Creador { get; set; } = string.Empty;

    public int TotalMiembros { get; set; }

    public bool EsCreadorActual { get; set; }

    public bool PuedePublicar { get; set; }

    public PublicacionFormViewModel NuevaPublicacion { get; set; } = new();

    public IReadOnlyList<PublicacionCardViewModel> Publicaciones { get; set; } = Array.Empty<PublicacionCardViewModel>();
}

public class PublicacionFormViewModel
{
    public int IdComunidad { get; set; }

    [Required(ErrorMessage = "Escribe el contenido de tu publicación.")]
    [StringLength(2000, ErrorMessage = "La publicación no puede superar 2,000 caracteres.")]
    public string Contenido { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "La URL de la imagen no puede superar 500 caracteres.")]
    [Url(ErrorMessage = "Ingresa una URL válida para la imagen.")]
    public string? ImagenUrl { get; set; }
}

public class PublicacionCardViewModel
{
    public int IdPublicacion { get; set; }

    public int IdUsuario { get; set; }

    public string Autor { get; set; } = string.Empty;

    public string Contenido { get; set; } = string.Empty;

    public string? ImagenUrl { get; set; }

    public DateTime FechaPublicacion { get; set; }

    public bool PuedeEliminar { get; set; }

    public bool PuedeReportar { get; set; }
}

public class ReporteUsuarioViewModel
{
    [Range(1, int.MaxValue)]
    public int IdComunidad { get; set; }

    [Range(1, int.MaxValue)]
    public int IdPublicacion { get; set; }

    [Range(1, int.MaxValue)]
    public int IdUsuarioReportado { get; set; }

    [Required(ErrorMessage = "Indica el motivo del reporte.")]
    [StringLength(255, ErrorMessage = "El motivo no puede superar 255 caracteres.")]
    public string Motivo { get; set; } = string.Empty;
}
