using Microsoft.EntityFrameworkCore;
using Proyecto_HobbyHub.Models;

namespace Proyecto_HobbyHub.Security;

public sealed class SocialAccess(HobbyHubContext db)
{
    public IQueryable<Usuario> VisiblePeople(int user)=>db.Usuarios.Where(u=>u.Estado=="Activo"&&!db.BloqueosUsuario.Any(b=>
        b.IdActor==user&&b.IdBloqueado==u.IdUsuario||b.IdActor==u.IdUsuario&&b.IdBloqueado==user));
    public Task<bool> Blocked(int user,int other)=>db.BloqueosUsuario.AnyAsync(b=>b.IdActor==user&&b.IdBloqueado==other||b.IdActor==other&&b.IdBloqueado==user);
    public IQueryable<Publicacione> VisiblePosts(int user) => db.Publicaciones.Where(p => p.Estado == "Visible" &&
        !db.BloqueosUsuario.Any(b=>b.IdActor==user&&b.IdBloqueado==p.IdUsuario||b.IdActor==p.IdUsuario&&b.IdBloqueado==user) &&
        p.IdUsuarioNavigation.Estado == "Activo" &&
        (!p.IdComunidadNavigation.Privada || p.IdComunidadNavigation.IdCreador == user ||
            db.MiembrosComunidad.Any(m => m.IdComunidad == p.IdComunidad && m.IdUsuario == user && m.Estado == "Aceptado")) &&
        (!p.IdUsuarioNavigation.Privado || p.IdUsuario == user ||
            db.Seguimientos.Any(s => s.IdSeguidor == user && s.IdSeguido == p.IdUsuario && s.Aceptado)));
    public Task<bool> CanSeePost(int user, int post) => VisiblePosts(user).AnyAsync(p => p.IdPublicacion == post);
    public Task<bool> CanEnter(int user, int community) => db.Comunidades.AnyAsync(c => c.IdComunidad == community &&
        (!c.Privada || c.IdCreador == user || db.MiembrosComunidad.Any(m => m.IdComunidad == community && m.IdUsuario == user && m.Estado == "Aceptado")));
    public Task<bool> CanPublish(int user, int community) => db.Comunidades.AnyAsync(c => c.IdComunidad == community &&
        (c.IdCreador == user || db.MiembrosComunidad.Any(m => m.IdComunidad == community && m.IdUsuario == user && m.Estado == "Aceptado")));
    public async Task Notify(int target, string type, int? resource, int? actor=null)
    {
        if(actor.HasValue&&await Blocked(actor.Value,target))return;
        if (await db.Usuarios.AnyAsync(u => u.IdUsuario == target && u.Estado == "Activo" && u.RecibirNotificaciones))
            db.Notificaciones.Add(new() { IdUsuario = target, Tipo = type, IdRecurso = resource, FechaUtc = DateTime.UtcNow });
    }
}
