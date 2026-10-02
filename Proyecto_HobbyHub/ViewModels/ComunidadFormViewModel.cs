using System.ComponentModel.DataAnnotations;

namespace Proyecto_HobbyHub.ViewModels;

public class ComunidadFormViewModel
{
    public int? IdComunidad { get; set; }

    [Required(ErrorMessage = "El nombre de la comunidad es requerido.")]
    [StringLength(100, ErrorMessage = "El nombre no puede superar 100 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripción es requerida.")]
    [StringLength(500, ErrorMessage = "La descripción no puede superar 500 caracteres.")]
    public string Descripcion { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecciona una categoría.")]
    [StringLength(50)]
    public string Categoria { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "La URL de imagen no puede superar 500 caracteres.")]
    [Url(ErrorMessage = "Ingresa una URL válida para la imagen.")]
    public string? ImagenPortada { get; set; }
}
