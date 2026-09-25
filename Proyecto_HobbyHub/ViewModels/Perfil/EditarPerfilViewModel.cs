using System.ComponentModel.DataAnnotations;

namespace Proyecto_HobbyHub.ViewModels.Perfil;

public class EditarPerfilViewModel
{
    [Required(ErrorMessage = "El nombre es requerido.")]
    [StringLength(100, ErrorMessage = "El nombre no puede superar 100 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(10, ErrorMessage = "El celular no puede superar 10 caracteres.")]
    public string? Celular { get; set; }

    [StringLength(255, ErrorMessage = "La direccion no puede superar 255 caracteres.")]
    public string? Direccion { get; set; }

    [StringLength(500, ErrorMessage = "La URL de perfil no puede superar 500 caracteres.")]
    [Url(ErrorMessage = "Ingresa una URL valida para la imagen de perfil.")]
    public string? ImagenPerfilUrl { get; set; }

    [StringLength(500, ErrorMessage = "La URL de portada no puede superar 500 caracteres.")]
    [Url(ErrorMessage = "Ingresa una URL valida para la portada.")]
    public string? PortadaUrl { get; set; }
}