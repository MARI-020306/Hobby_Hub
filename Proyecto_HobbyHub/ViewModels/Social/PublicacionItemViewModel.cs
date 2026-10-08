namespace Proyecto_HobbyHub.ViewModels.Social;

public class PublicacionItemViewModel
{
    public int IdPublicacion { get; set; }

    public int IdUsuario { get; set; }

    public string AutorNombre { get; set; } = string.Empty;

    public string AutorAvatarUrl { get; set; } = string.Empty;

    public int IdComunidad { get; set; }

    public string NombreComunidad { get; set; } = string.Empty;

    public string Contenido { get; set; } = string.Empty;

    public string? ImagenUrl { get; set; }

    public DateTime FechaPublicacion { get; set; }

    public int TotalLikes { get; set; }

    public bool LeGustaAlUsuarioActual { get; set; }

    public int TotalComentarios { get; set; }

    public IReadOnlyList<ComentarioItemViewModel> Comentarios { get; set; } = Array.Empty<ComentarioItemViewModel>();
}
