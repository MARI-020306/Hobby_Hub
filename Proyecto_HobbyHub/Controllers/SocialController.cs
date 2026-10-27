using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Proyecto_HobbyHub.Models;
using Proyecto_HobbyHub.Security;
using Proyecto_HobbyHub.ViewModels;

namespace Proyecto_HobbyHub.Controllers;

[Authorize, AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class SocialController(HobbyHubContext db, SocialAccess access, ImageUploadService images) : Controller
{
    private int Me => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static int Page(int page) => Math.Clamp(page,1,10000);
    private async Task<List<PostCard>> Cards(IQueryable<Publicacione> query, int page = 1)
    {
        var posts = await query.AsNoTracking().Include(p => p.IdUsuarioNavigation).Include(p => p.IdComunidadNavigation)
            .OrderByDescending(p => p.FechaPublicacion).ThenByDescending(p => p.IdPublicacion).Skip((Page(page)-1)*20).Take(21).ToListAsync(HttpContext.RequestAborted);
        var ids=posts.Select(p=>p.IdPublicacion).ToArray();
        var gallery=await db.PublicacionImagenes.AsNoTracking().Where(i=>ids.Contains(i.IdPublicacion)).OrderBy(i=>i.Orden).ToListAsync(HttpContext.RequestAborted);
        var likes=await db.PublicacionLikes.Where(l=>ids.Contains(l.IdPublicacion)).GroupBy(l=>l.IdPublicacion).Select(g=>new{Id=g.Key,Count=g.Count()}).ToDictionaryAsync(g=>g.Id,g=>g.Count);
        var comments=await db.Comentarios.Where(c=>ids.Contains(c.IdPublicacion)&&c.Estado=="Visible").GroupBy(c=>c.IdPublicacion).Select(g=>new{Id=g.Key,Count=g.Count()}).ToDictionaryAsync(g=>g.Id,g=>g.Count);
        var liked=await db.PublicacionLikes.Where(l=>ids.Contains(l.IdPublicacion)&&l.IdUsuario==Me).Select(l=>l.IdPublicacion).ToListAsync(HttpContext.RequestAborted);
        var saved=await db.PublicacionesGuardadas.Where(g=>ids.Contains(g.IdPublicacion)&&g.IdUsuario==Me).Select(g=>g.IdPublicacion).ToListAsync(HttpContext.RequestAborted);
        var result = posts.Select(post=>new PostCard{Post=post,Images=gallery.Where(i=>i.IdPublicacion==post.IdPublicacion).ToList(),Likes=likes.GetValueOrDefault(post.IdPublicacion),CommentCount=comments.GetValueOrDefault(post.IdPublicacion),Liked=liked.Contains(post.IdPublicacion),Saved=saved.Contains(post.IdPublicacion)}).ToList();
        return result;
    }
    public async Task<IActionResult> Inicio(string mode="para-ti", int page=1)
    {
        var query=access.VisiblePosts(Me);
        var person=await db.Usuarios.AsNoTracking().SingleAsync(u => u.IdUsuario==Me);
        if (mode=="siguiendo") query=query.Where(p => p.IdUsuario==Me || db.Seguimientos.Any(s => s.IdSeguidor==Me && s.IdSeguido==p.IdUsuario && s.Aceptado) ||
            db.MiembrosComunidad.Any(m => m.IdUsuario==Me && m.IdComunidad==p.IdComunidad && m.Estado=="Aceptado") || p.IdComunidadNavigation.IdCreador==Me);
        else if (!string.IsNullOrWhiteSpace(person.Intereses)) {
            var categories=person.Intereses.Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
            query=query.Where(p => categories.Contains(p.IdComunidadNavigation.Categoria!) || p.IdUsuario==Me ||
                db.Seguimientos.Any(s => s.IdSeguidor==Me && s.IdSeguido==p.IdUsuario && s.Aceptado) ||
                db.MiembrosComunidad.Any(m => m.IdUsuario==Me && m.IdComunidad==p.IdComunidad && m.Estado=="Aceptado"));
        }
        var cards=await Cards(query,page);
        return View("Feed",new SocialPage { Title="Tu mundo de hobbies",Posts=cards.Take(20).ToList(),More=cards.Count>20,Page=Page(page),Mode=mode });
    }
    public async Task<IActionResult> Explorar(string? q,int page=1)
    {
        string term=(q??"").Trim(); if(term.Length>100) return BadRequest();
        var users=await access.VisiblePeople(Me).AsNoTracking().Where(u => u.Nombre.Contains(term)||u.Intereses.Contains(term))
            .OrderBy(u => u.Nombre).Skip((Page(page)-1)*20).Take(20).Select(u => new PersonCard(u.IdUsuario,u.Nombre,u.Biografia,u.Privado)).ToListAsync(HttpContext.RequestAborted);
        var communities=await db.Comunidades.AsNoTracking().Where(c => c.Nombre.Contains(term)||c.Categoria!.Contains(term)).OrderBy(c => c.Nombre).Skip((Page(page)-1)*20).Take(20).ToListAsync(HttpContext.RequestAborted);
        var communityIds=communities.Select(c=>c.IdComunidad).ToArray();
        ViewBag.MembershipStates=await db.MiembrosComunidad.AsNoTracking().Where(m=>m.IdUsuario==Me&&communityIds.Contains(m.IdComunidad)).ToDictionaryAsync(m=>m.IdComunidad,m=>m.Estado);
        var posts=await Cards(access.VisiblePosts(Me).Where(p => p.Contenido.Contains(term)),page);
        return View(new SocialPage {Title="Explorar",Query=term,People=users,Communities=communities,Posts=posts.Take(20).ToList(),Page=Page(page),More=posts.Count>20||users.Count==20||communities.Count==20});
    }
    public async Task<IActionResult> Publicacion(int id,int page=1)
    {
        var cards=await Cards(access.VisiblePosts(Me).Where(p => p.IdPublicacion==id)); if(cards.Count==0) return NotFound();
        cards[0].Comments=await db.Comentarios.AsNoTracking().Include(c => c.IdUsuarioNavigation).Where(c => c.IdPublicacion==id && c.Estado=="Visible" && c.IdUsuarioNavigation.Estado=="Activo")
            .OrderBy(c => c.FechaComentario).ThenBy(c => c.IdComentario).Skip((Page(page)-1)*50).Take(51).ToListAsync(HttpContext.RequestAborted);
        return View(new SocialPage {Title="Publicación",Posts=cards,Page=Page(page),More=cards[0].Comments.Count>50});
    }
    public async Task<IActionResult> Nueva()
    {
        ViewBag.Communities=await db.Comunidades.AsNoTracking().Where(c => c.IdCreador==Me || db.MiembrosComunidad.Any(m => m.IdUsuario==Me && m.IdComunidad==c.IdComunidad && m.Estado=="Aceptado")).ToListAsync(HttpContext.RequestAborted);
        return View(new ComposePost());
    }
    [HttpPost, RequestSizeLimit(32*1024*1024)]
    public async Task<IActionResult> Nueva(ComposePost model)
    {
        if (model.Images.Count>6) ModelState.AddModelError("Images","Máximo seis imágenes por publicación.");
        if (!ModelState.IsValid) { await Nueva(); return View(model); }
        if (!await access.CanPublish(Me,model.CommunityId)) return Forbid();
        var files=new List<StoredImage>();
        try {
            foreach(var file in model.Images) files.Add(await images.Store(file,HttpContext.RequestAborted));
            await using var tx=await db.Database.BeginTransactionAsync(HttpContext.RequestAborted);
            var post=new Publicacione {IdUsuario=Me,IdComunidad=model.CommunityId,Contenido=model.Content.Trim(),Estado="Visible",FechaPublicacion=DateTime.Now};
            db.Publicaciones.Add(post); await db.SaveChangesAsync(HttpContext.RequestAborted);
            for(int i=0;i<files.Count;i++) db.PublicacionImagenes.Add(new() {IdImagen=Guid.NewGuid(),IdPublicacion=post.IdPublicacion,Archivo=files[i].File,Tipo=files[i].Mime,Orden=i});
            var owner=await db.Comunidades.Where(c=>c.IdComunidad==model.CommunityId).Select(c=>c.IdCreador).SingleAsync(HttpContext.RequestAborted);
            var recipients=await db.MiembrosComunidad.Where(m=>m.IdComunidad==model.CommunityId && m.Estado=="Aceptado").Select(m=>m.IdUsuario).ToListAsync(HttpContext.RequestAborted);
            recipients.Add(owner);
            foreach(var recipient in recipients.Distinct().Where(u=>u!=Me)) await access.Notify(recipient,"Publicacion",post.IdPublicacion,Me);
            await db.SaveChangesAsync(HttpContext.RequestAborted); await tx.CommitAsync(HttpContext.RequestAborted); return RedirectToAction(nameof(Publicacion),new {id=post.IdPublicacion});
        } catch(Exception e) when(e is InvalidDataException or OverflowException) {
            foreach(var file in files) images.Remove(file.File);
            ModelState.AddModelError("Images",e is InvalidDataException ? e.Message : "Imagen inválida."); await Nueva(); return View(model);
        } catch { foreach(var file in files) images.Remove(file.File); throw; }
    }
    public async Task<IActionResult> Imagen(Guid id)
    {
        var image=await db.PublicacionImagenes.AsNoTracking().SingleOrDefaultAsync(i=>i.IdImagen==id);
        if(image is null) return NotFound();
        if(!await access.CanSeePost(Me,image.IdPublicacion)&&!User.IsInRole("Administrador")){
            bool reported=User.IsInRole("Moderador")&&await db.Reportes.AnyAsync(r=>r.IdPublicacion==image.IdPublicacion||r.IdComentarioNavigation!=null&&r.IdComentarioNavigation.IdPublicacion==image.IdPublicacion);
            if(!reported)return NotFound();
        }
        string file=images.PathFor(image.Archivo); if(!System.IO.File.Exists(file)) return NotFound();
        Response.Headers.XContentTypeOptions="nosniff"; return PhysicalFile(file,image.Tipo);
    }
    [HttpPost,RequestSizeLimit(6*1024*1024)]
    public async Task<IActionResult> FotoPerfil(IFormFile image,string purpose)
    {
        if(image is null)return BadRequest("Selecciona una fotografía.");
        if(purpose is not("Perfil" or "Portada"))return BadRequest();
        StoredImage file;
        try{file=await images.Store(image,HttpContext.RequestAborted);}catch(InvalidDataException e){return BadRequest(e.Message);}
        try{
            var previous=await db.UsuarioImagenes.Where(i=>i.IdUsuario==Me&&i.Tipo==purpose&&i.EsPrincipal).ToListAsync(HttpContext.RequestAborted);foreach(var p in previous)p.EsPrincipal=false;
            db.UsuarioImagenes.Add(new(){IdUsuario=Me,Url=Url.Action(nameof(Foto),new{id=file.File})!,Tipo=purpose,EsPrincipal=true,FechaSubida=DateTime.Now});
            await db.SaveChangesAsync(HttpContext.RequestAborted);return RedirectToAction("Index","Perfil");
        }catch{images.Remove(file.File);throw;}
    }
    public async Task<IActionResult> Foto(string id)
    {
        string route=Url.Action(nameof(Foto),new{id})!;
        var picture=await db.UsuarioImagenes.AsNoTracking().Include(i=>i.IdUsuarioNavigation).FirstOrDefaultAsync(i=>i.Url==route);
        if(picture is null||picture.IdUsuarioNavigation.Estado!="Activo")return NotFound();
        if(await access.Blocked(Me,picture.IdUsuario))return NotFound();
        if(picture.IdUsuarioNavigation.Privado&&picture.IdUsuario!=Me&&!await db.Seguimientos.AnyAsync(s=>s.IdSeguidor==Me&&s.IdSeguido==picture.IdUsuario&&s.Aceptado))return NotFound();
        string file;try{file=images.PathFor(id);}catch(InvalidDataException){return NotFound();}
        if(!System.IO.File.Exists(file))return NotFound();Response.Headers.XContentTypeOptions="nosniff";return PhysicalFile(file,id.EndsWith(".png")?"image/png":"image/jpeg");
    }
    [HttpPost]
    public async Task<IActionResult> RetirarImagen(Guid id)
    {
        var image=await db.PublicacionImagenes.FindAsync(id);if(image is null)return NotFound();
        if(!await db.Publicaciones.AnyAsync(p=>p.IdPublicacion==image.IdPublicacion&&p.IdUsuario==Me))return Forbid();
        db.PublicacionImagenes.Remove(image);await db.SaveChangesAsync(HttpContext.RequestAborted);images.Remove(image.Archivo);return RedirectToAction(nameof(Publicacion),new{id=image.IdPublicacion});
    }
    [HttpPost]
    public async Task<IActionResult> Guardar(int id)
    {
        if(!await access.CanSeePost(Me,id)) return NotFound();
        var saved=await db.PublicacionesGuardadas.FindAsync(Me,id);
        if(saved is null) db.PublicacionesGuardadas.Add(new(){IdUsuario=Me,IdPublicacion=id,FechaUtc=DateTime.UtcNow}); else db.PublicacionesGuardadas.Remove(saved);
        await db.SaveChangesAsync(HttpContext.RequestAborted); return RedirectToAction(nameof(Publicacion),new{id});
    }
    public async Task<IActionResult> Guardados(int page=1)
    {
        var cards=await Cards(access.VisiblePosts(Me).Where(p=>db.PublicacionesGuardadas.Any(g=>g.IdUsuario==Me&&g.IdPublicacion==p.IdPublicacion)),page);
        return View("Feed",new SocialPage{Title="Tus guardados",Posts=cards.Take(20).ToList(),Page=Page(page),More=cards.Count>20,Mode="guardados"});
    }
    [HttpPost]
    public async Task<IActionResult> Comentar(int id,string? content,int? parent)
    {
        if(string.IsNullOrWhiteSpace(content)||content.Length>1000) return BadRequest();
        if(!await access.CanSeePost(Me,id)) return NotFound();
        if(parent.HasValue&&!await db.Comentarios.AnyAsync(c=>c.IdComentario==parent&&c.IdPublicacion==id&&c.Estado=="Visible")) return BadRequest();
        var post=await db.Publicaciones.AsNoTracking().SingleAsync(p=>p.IdPublicacion==id);
        db.Comentarios.Add(new(){IdUsuario=Me,IdPublicacion=id,IdComentarioPadre=parent,Contenido=content.Trim(),Estado="Visible",FechaComentario=DateTime.Now});
        if(post.IdUsuario!=Me) await access.Notify(post.IdUsuario,"Comentario",id,Me);
        if(parent.HasValue) {var author=await db.Comentarios.Where(c=>c.IdComentario==parent).Select(c=>c.IdUsuario).SingleAsync(HttpContext.RequestAborted);if(author!=Me&&author!=post.IdUsuario)await access.Notify(author,"Respuesta",id,Me);}
        await db.SaveChangesAsync(HttpContext.RequestAborted); return RedirectToAction(nameof(Publicacion),new{id});
    }
    [HttpPost]
    public async Task<IActionResult> RetirarComentario(int id)
    {
        var comment=await db.Comentarios.FindAsync(id); if(comment is null) return NotFound();
        if(comment.IdUsuario!=Me) return Forbid(); comment.Estado="Eliminado";
        await db.SaveChangesAsync(HttpContext.RequestAborted);return RedirectToAction(nameof(Publicacion),new{id=comment.IdPublicacion});
    }
    public async Task<IActionResult> Persona(int id,int page=1)
    {
        var user=await access.VisiblePeople(Me).AsNoTracking().Where(u=>u.IdUsuario==id).Select(u=>new PersonCard(u.IdUsuario,u.Nombre,u.Biografia,u.Privado)).SingleOrDefaultAsync();
        if(user is null) return NotFound(); var follow=await db.Seguimientos.FindAsync(Me,id);
        bool canSee=!user.Private||id==Me||follow?.Aceptado==true;
        if(canSee){
            var pictures=await db.UsuarioImagenes.AsNoTracking().Where(i=>i.IdUsuario==id&&i.EsPrincipal).ToListAsync(HttpContext.RequestAborted);
            user=user with{Photo=pictures.FirstOrDefault(i=>i.Tipo=="Perfil")?.Url,Cover=pictures.FirstOrDefault(i=>i.Tipo=="Portada")?.Url};
        }
        var cards=canSee?await Cards(access.VisiblePosts(Me).Where(p=>p.IdUsuario==id),page):new();
        return View(new SocialPage{Title=user.Name,Profile=user,CanSeeProfile=canSee,Following=follow?.Aceptado,Posts=cards.Take(20).ToList(),Page=Page(page),More=cards.Count>20,
            Followers=await db.Seguimientos.CountAsync(s=>s.IdSeguido==id&&s.Aceptado),FollowingCount=await db.Seguimientos.CountAsync(s=>s.IdSeguidor==id&&s.Aceptado)});
    }
    [HttpPost]
    public async Task<IActionResult> Seguir(int id)
    {
        if(await access.Blocked(Me,id))return Forbid();
        if(id==Me) return BadRequest(); var person=await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(u=>u.IdUsuario==id&&u.Estado=="Activo");if(person is null)return NotFound();
        var follow=await db.Seguimientos.FindAsync(Me,id);
        if(follow is null){db.Seguimientos.Add(new(){IdSeguidor=Me,IdSeguido=id,Aceptado=!person.Privado,FechaUtc=DateTime.UtcNow});await access.Notify(id,person.Privado?"SolicitudSeguimiento":"Seguidor",Me,Me);}
        else db.Seguimientos.Remove(follow);
        await db.SaveChangesAsync(HttpContext.RequestAborted);return RedirectToAction(nameof(Persona),new{id});
    }
    public async Task<IActionResult> Seguidores()
    {
        var links=await db.Seguimientos.Where(s=>s.IdSeguido==Me).ToListAsync(HttpContext.RequestAborted);
        var ids=links.Select(s=>s.IdSeguidor).ToArray();
        return View(new SocialPage{Title="Seguidores y solicitudes",Requests=links,People=await db.Usuarios.Where(u=>ids.Contains(u.IdUsuario)).Select(u=>new PersonCard(u.IdUsuario,u.Nombre,u.Biografia,u.Privado)).ToListAsync(HttpContext.RequestAborted)});
    }
    [HttpPost]
    public async Task<IActionResult> Solicitud(int id,bool accept)
    {
        var request=await db.Seguimientos.FindAsync(id,Me);if(request is null)return NotFound();
        if(accept)request.Aceptado=true;else db.Seguimientos.Remove(request);
        if(accept)await access.Notify(id,"SeguimientoAceptado",Me,Me);
        await db.SaveChangesAsync(HttpContext.RequestAborted);return RedirectToAction(nameof(Seguidores));
    }
    public async Task<IActionResult> Ajustes()
    {
        var u=await db.Usuarios.AsNoTracking().SingleAsync(u=>u.IdUsuario==Me);
        return View(new ProfileSettings{Name=u.Nombre,Bio=u.Biografia,Interests=u.Intereses,Private=u.Privado,Messages=u.PermitirMensajes,Notices=u.RecibirNotificaciones});
    }
    [HttpPost]
    public async Task<IActionResult> Ajustes(ProfileSettings model)
    {
        if(!ModelState.IsValid)return View(model);var u=await db.Usuarios.SingleAsync(u=>u.IdUsuario==Me);
        u.Nombre=model.Name.Trim();u.Biografia=(model.Bio??"").Trim();u.Intereses=(model.Interests??"").Trim();u.Privado=model.Private;u.PermitirMensajes=model.Messages;u.RecibirNotificaciones=model.Notices;
        await db.SaveChangesAsync(HttpContext.RequestAborted);TempData["Notice"]="Preferencias guardadas.";return RedirectToAction(nameof(Ajustes));
    }
    public async Task<IActionResult> Mensajes(int? id,int page=1)
    {
        if(id==Me)return BadRequest();
        var query=db.MensajesPrivados.AsNoTracking().Where(m=>m.Estado=="Visible"&&(m.IdRemitente==Me||m.IdDestinatario==Me));
        if(id.HasValue){if(!await db.Usuarios.AnyAsync(u=>u.IdUsuario==id))return NotFound();query=query.Where(m=>(m.IdRemitente==Me&&m.IdDestinatario==id)||(m.IdRemitente==id&&m.IdDestinatario==Me));}
        var messages=await query.OrderByDescending(m=>m.IdMensaje).Skip((Page(page)-1)*50).Take(51).ToListAsync(HttpContext.RequestAborted);
        var ids=messages.SelectMany(m=>new[]{m.IdRemitente,m.IdDestinatario}).Append(id??Me).Distinct().ToArray();
        bool canSend=id.HasValue&&!await access.Blocked(Me,id.Value)&&await db.Usuarios.AnyAsync(u=>u.IdUsuario==id&&u.Estado=="Activo"&&u.PermitirMensajes&&(!u.Privado||db.Seguimientos.Any(s=>s.IdSeguidor==Me&&s.IdSeguido==u.IdUsuario&&s.Aceptado)));
        return View(new SocialPage{Title="Mensajes privados",CanSend=canSend,UserId=id??0,Messages=messages.Take(50).OrderBy(m=>m.IdMensaje).ToList(),Page=Page(page),More=messages.Count>50,
            People=await db.Usuarios.Where(u=>ids.Contains(u.IdUsuario)).Select(u=>new PersonCard(u.IdUsuario,u.Nombre,u.Biografia,u.Privado)).ToListAsync(HttpContext.RequestAborted)});
    }
    [HttpPost]
    public async Task<IActionResult> Enviar(int id,string? content)
    {
        if(id==Me||string.IsNullOrWhiteSpace(content)||content.Length>2000)return BadRequest();
        if(await access.Blocked(Me,id))return Forbid();
        var recipient=await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(u=>u.IdUsuario==id&&u.Estado=="Activo");
        if(recipient is null)return NotFound();
        if(!recipient.PermitirMensajes||(recipient.Privado&&!await db.Seguimientos.AnyAsync(s=>s.IdSeguidor==Me&&s.IdSeguido==id&&s.Aceptado)))return Forbid();
        db.MensajesPrivados.Add(new(){IdRemitente=Me,IdDestinatario=id,Contenido=content.Trim(),FechaUtc=DateTime.UtcNow});
        await access.Notify(id,"Mensaje",Me,Me);await db.SaveChangesAsync(HttpContext.RequestAborted);return RedirectToAction(nameof(Mensajes),new{id});
    }
    [HttpPost]
    public async Task<IActionResult> LeerMensajes(int id)
    {
        await db.MensajesPrivados.Where(m=>m.IdDestinatario==Me&&m.IdRemitente==id&&m.LeidoUtc==null).ExecuteUpdateAsync(s=>s.SetProperty(m=>m.LeidoUtc,DateTime.UtcNow));
        return RedirectToAction(nameof(Mensajes),new{id});
    }
    public async Task<IActionResult> Notificaciones(bool unread=false,int page=1)
    {
        var query=db.Notificaciones.Where(n=>n.IdUsuario==Me&&(!unread||!n.Leida));
        var notices=await query.OrderByDescending(n=>n.IdNotificacion).Skip((Page(page)-1)*30).Take(31).ToListAsync(HttpContext.RequestAborted);
        return View(new SocialPage{Title="Notificaciones",Notices=notices.Take(30).ToList(),More=notices.Count>30,Page=Page(page),Mode=unread?"unread":"all"});
    }
    [HttpPost]
    public async Task<IActionResult> LeerNotificacion(long id)
    {
        var notice=await db.Notificaciones.SingleOrDefaultAsync(n=>n.IdNotificacion==id&&n.IdUsuario==Me);if(notice is null)return NotFound();
        notice.Leida=true;await db.SaveChangesAsync(HttpContext.RequestAborted);return RedirectToAction(nameof(Notificaciones));
    }
    public async Task<IActionResult> Reportes() => View(new SocialPage{Title="Mis reportes",Reports=await db.Reportes.Where(r=>r.IdUsuarioReporta==Me).OrderByDescending(r=>r.IdReporte).Take(100).ToListAsync(HttpContext.RequestAborted)});
    [HttpPost]
    public async Task<IActionResult> Reportar(string type,int id,string? reason)
    {
        if(string.IsNullOrWhiteSpace(reason)||reason.Length>255)return BadRequest();
        int target;int? post=null,comment=null;
        if(type=="publicacion"){if(!await access.CanSeePost(Me,id))return NotFound();post=id;target=await db.Publicaciones.Where(p=>p.IdPublicacion==id).Select(p=>p.IdUsuario).SingleAsync(HttpContext.RequestAborted);}
        else if(type=="comentario"){var c=await db.Comentarios.AsNoTracking().SingleOrDefaultAsync(c=>c.IdComentario==id&&c.Estado=="Visible");if(c is null||!await access.CanSeePost(Me,c.IdPublicacion))return NotFound();comment=id;target=c.IdUsuario;}
        else if(type=="usuario"){if(!await db.Usuarios.AnyAsync(u=>u.IdUsuario==id&&u.Estado=="Activo"))return NotFound();target=id;}
        else return BadRequest();
        if(target==Me)return BadRequest();
        await using var reportTransaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, HttpContext.RequestAborted);
        if(await db.Reportes.AnyAsync(r=>r.IdUsuarioReporta==Me&&r.IdPublicacion==post&&r.IdComentario==comment&&r.IdMensaje==null&&r.IdUsuarioReportado==target&&r.Estado=="Pendiente"))return Conflict("Ya existe un reporte pendiente.");
        db.Reportes.Add(new(){IdUsuarioReporta=Me,IdUsuarioReportado=target,IdPublicacion=post,IdComentario=comment,Motivo=reason.Trim(),Estado="Pendiente",FechaReporte=DateTime.Now});
        await db.SaveChangesAsync(HttpContext.RequestAborted);await reportTransaction.CommitAsync(HttpContext.RequestAborted);return RedirectToAction(nameof(Reportes));
    }
    [HttpPost]
    public async Task<IActionResult> ReportarMensaje(long id,string? reason)
    {
        if(string.IsNullOrWhiteSpace(reason)||reason.Length>255)return BadRequest();
        var message=await db.MensajesPrivados.AsNoTracking().SingleOrDefaultAsync(m=>m.IdMensaje==id&&m.IdDestinatario==Me&&m.Estado=="Visible");
        if(message is null)return NotFound();
        await using var reportTransaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, HttpContext.RequestAborted);
        if(await db.Reportes.AnyAsync(r=>r.IdMensaje==id&&r.IdUsuarioReporta==Me&&r.Estado=="Pendiente"))return Conflict("Ya existe un reporte pendiente.");
        db.Reportes.Add(new(){IdMensaje=id,IdUsuarioReporta=Me,IdUsuarioReportado=message.IdRemitente,Motivo=reason.Trim(),Estado="Pendiente",FechaReporte=DateTime.Now});
        await db.SaveChangesAsync(HttpContext.RequestAborted);await reportTransaction.CommitAsync(HttpContext.RequestAborted);return RedirectToAction(nameof(Reportes));
    }
    public async Task<IActionResult> Bloqueados()
    {
        var ids=db.BloqueosUsuario.Where(b=>b.IdActor==Me).Select(b=>b.IdBloqueado);
        return View(new SocialPage{Title="Personas bloqueadas",People=await db.Usuarios.Where(u=>ids.Contains(u.IdUsuario)).Select(u=>new PersonCard(u.IdUsuario,u.Nombre,"",u.Privado)).ToListAsync(HttpContext.RequestAborted)});
    }
    [HttpPost]
    public async Task<IActionResult> Bloquear(int id,bool unblock=false)
    {
        if(id==Me||!await db.Usuarios.AnyAsync(u=>u.IdUsuario==id))return BadRequest();
        await using var tx=await db.Database.BeginTransactionAsync(HttpContext.RequestAborted);
        var block=await db.BloqueosUsuario.FindAsync(Me,id);
        if(unblock){if(block is null)return NotFound();db.BloqueosUsuario.Remove(block);}
        else{
            if(block is not null)return Conflict();
            db.BloqueosUsuario.Add(new(){IdActor=Me,IdBloqueado=id,FechaUtc=DateTime.UtcNow});
            var follows=await db.Seguimientos.Where(s=>s.IdSeguidor==Me&&s.IdSeguido==id||s.IdSeguidor==id&&s.IdSeguido==Me).ToListAsync(HttpContext.RequestAborted);db.Seguimientos.RemoveRange(follows);
        }
        db.AuditoriasSeguridad.Add(new(){Accion=unblock?"UserUnblocked":"UserBlocked",IdActor=Me,IdObjetivo=Me,Exito=true,FechaUtc=DateTime.UtcNow});
        await db.SaveChangesAsync(HttpContext.RequestAborted);await tx.CommitAsync(HttpContext.RequestAborted);return RedirectToAction(nameof(Bloqueados));
    }
    public async Task<IActionResult> Actividad(int page=1)
    {
        var audit=await db.AuditoriasSeguridad.AsNoTracking().Where(a=>a.IdActor==Me||a.IdObjetivo==Me).OrderByDescending(a=>a.IdAuditoria).Skip((Page(page)-1)*30).Take(31).ToListAsync(HttpContext.RequestAborted);
        var sessions=await db.SesionesUsuario.AsNoTracking().Where(s=>s.IdUsuario==Me&&s.RevocadaUtc==null&&s.ExpiraUtc>DateTime.UtcNow).OrderByDescending(s=>s.FechaCreacionUtc).ToListAsync(HttpContext.RequestAborted);
        return View(new SocialPage{Title="Actividad y sesiones",Audit=audit.Take(30).ToList(),Sessions=sessions,Page=Page(page),More=audit.Count>30});
    }
    public async Task<IActionResult> Reacciones(int page=1)
    {
        var cards=await Cards(access.VisiblePosts(Me).Where(p=>db.PublicacionLikes.Any(l=>l.IdUsuario==Me&&l.IdPublicacion==p.IdPublicacion)),page);
        return View("Feed",new SocialPage{Title="Publicaciones que te gustan",Posts=cards.Take(20).ToList(),Page=Page(page),More=cards.Count>20,Mode="reacciones"});
    }
    public async Task<IActionResult> MisComentarios(int page=1)
    {
        var visible=access.VisiblePosts(Me).Select(p=>p.IdPublicacion);
        var comments=await db.Comentarios.AsNoTracking().Where(c=>c.IdUsuario==Me&&c.Estado=="Visible"&&visible.Contains(c.IdPublicacion))
            .OrderByDescending(c=>c.IdComentario).Skip((Page(page)-1)*30).Take(31).ToListAsync(HttpContext.RequestAborted);
        return View(new SocialPage{Title="Mis comentarios y respuestas",Comments=comments.Take(30).ToList(),Page=Page(page),More=comments.Count>30});
    }
    [HttpPost]
    public async Task<IActionResult> CerrarSesion(Guid id)
    {
        var session=await db.SesionesUsuario.SingleOrDefaultAsync(s=>s.IdSesion==id&&s.IdUsuario==Me);if(session is null)return NotFound();
        session.RevocadaUtc=DateTime.UtcNow;db.AuditoriasSeguridad.Add(new(){Accion="SessionRevoked",IdActor=Me,IdObjetivo=Me,Exito=true,FechaUtc=DateTime.UtcNow});
        await db.SaveChangesAsync(HttpContext.RequestAborted);return RedirectToAction(nameof(Actividad));
    }
}
