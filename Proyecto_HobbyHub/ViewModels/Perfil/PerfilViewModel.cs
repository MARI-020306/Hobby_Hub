using Proyecto_HobbyHub.Models;

namespace Proyecto_HobbyHub.ViewModels.Perfil;

public class PerfilViewModel
{
    public int IdUsuario { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public string Correo { get; set; } = string.Empty;

    public string? Celular { get; set; }

    public string? Direccion { get; set; }

    public string Rol { get; set; } = string.Empty;

    public string ImagenPerfilUrl { get; set; } = string.Empty;

    public string PortadaUrl { get; set; } = string.Empty;

    public int TotalPublicaciones { get; set; }

    public IReadOnlyList<Publicacione> Publicaciones { get; set; } = Array.Empty<Publicacione>();
}
