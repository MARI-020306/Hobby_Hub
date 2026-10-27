using System.ComponentModel.DataAnnotations;
using Proyecto_HobbyHub.Models;

namespace Proyecto_HobbyHub.ViewModels;

public sealed record PersonCard(int Id, string Name, string Bio, bool Private)
{
    public string? Photo { get; init; }
    public string? Cover { get; init; }
}
public sealed class PostCard
{
    public Publicacione Post { get; set; } = null!;
    public List<PublicacionImagen> Images { get; set; } = new();
    public List<Comentario> Comments { get; set; } = new();
    public int Likes { get; set; }
    public int CommentCount { get; set; }
    public bool Liked { get; set; }
    public bool Saved { get; set; }
}
public sealed class SocialPage
{
    public string Title { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public int Page { get; set; } = 1;
    public bool More { get; set; }
    public int UserId { get; set; }
    public string Mode { get; set; } = string.Empty;
    public List<PostCard> Posts { get; set; } = new();
    public List<PersonCard> People { get; set; } = new();
    public List<Comunidade> Communities { get; set; } = new();
    public PersonCard? Profile { get; set; }
    public bool CanSeeProfile { get; set; }
    public bool CanSend { get; set; }
    public bool? Following { get; set; }
    public int Followers { get; set; }
    public int FollowingCount { get; set; }
    public List<Seguimiento> Requests { get; set; } = new();
    public List<MensajePrivado> Messages { get; set; } = new();
    public List<Notificacion> Notices { get; set; } = new();
    public List<Reporte> Reports { get; set; } = new();
    public List<AuditoriaSeguridad> Audit { get; set; } = new();
    public List<SesionUsuario> Sessions { get; set; } = new();
    public List<Comentario> Comments { get; set; } = new();
}
public class ComposePost
{
    [Range(1,int.MaxValue)] public int CommunityId { get; set; }
    [Required, StringLength(2000)] public string Content { get; set; } = string.Empty;
    public List<IFormFile> Images { get; set; } = new();
}
public class ProfileSettings
{
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
    [StringLength(500)] public string? Bio { get; set; } = string.Empty;
    [StringLength(200)] public string? Interests { get; set; } = string.Empty;
    public bool Private { get; set; }
    public bool Messages { get; set; } = true;
    public bool Notices { get; set; } = true;
}
public class CommunitySettings
{
    [Range(1,int.MaxValue)] public int Id { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
    [StringLength(2000)] public string? Description { get; set; } = string.Empty;
    [StringLength(2000)] public string? Rules { get; set; } = string.Empty;
    [Required, StringLength(50)] public string Category { get; set; } = "General";
    public bool Private { get; set; }
}
