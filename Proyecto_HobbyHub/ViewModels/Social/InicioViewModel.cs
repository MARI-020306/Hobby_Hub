using System.ComponentModel.DataAnnotations;

namespace Proyecto_HobbyHub.ViewModels.Social;

public class InicioViewModel
{
    public string NombreUsuario { get; set; } = string.Empty;

    public string AvatarUrl { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escribe algo para publicar.")]
    [StringLength(2000, ErrorMessage = "La publicacion no puede superar 2000 caracteres.")]
    public string Contenido { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "La URL de imagen no puede superar 500 caracteres.")]
    [Url(ErrorMessage = "Ingresa una URL valida para la imagen.")]
    public string? ImagenUrl { get; set; }

    public IReadOnlyList<PublicacionItemViewModel> Publicaciones { get; set; } = Array.Empty<PublicacionItemViewModel>();
}