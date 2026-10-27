using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Proyecto_HobbyHub.Models;
using Proyecto_HobbyHub.ViewModels;
using Proyecto_HobbyHub.Security;

namespace Proyecto_HobbyHub.Controllers;

public partial class ComunidadesController
{
    [HttpGet("Administrar/{id:int}")]
    public async Task<IActionResult> Administrar(int id,string? q=null)
    {
        int me=ObtenerIdUsuarioActual()??0;
        var community=await _context.Comunidades.AsNoTracking().SingleOrDefaultAsync(c=>c.IdComunidad==id);
        if(community is null)return NotFound();if(community.IdCreador!=me)return Forbid();
        string term=(q??"").Trim();if(term.Length>100)return BadRequest();
        ViewBag.Query=term;
        ViewBag.Candidates=await new SocialAccess(_context).VisiblePeople(me).AsNoTracking().Where(u=>u.IdUsuario!=me&&u.Nombre.Contains(term)&&!_context.MiembrosComunidad.Any(m=>m.IdComunidad==id&&m.IdUsuario==u.IdUsuario))
            .OrderBy(u=>u.Nombre).Take(30).Select(u=>new PersonCard(u.IdUsuario,u.Nombre,"",u.Privado)).ToListAsync(HttpContext.RequestAborted);
        ViewBag.Members=await _context.MiembrosComunidad.AsNoTracking().Include(m=>m.Usuario).Where(m=>m.IdComunidad==id).OrderBy(m=>m.Usuario.Nombre).ToListAsync(HttpContext.RequestAborted);
        return View(new CommunitySettings{Id=id,Name=community.Nombre,Description=community.Descripcion??"",Rules=community.Reglas,Category=community.Categoria??"General",Private=community.Privada});
    }
    [HttpPost("Administrar/{id:int}"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Administrar(int id,CommunitySettings model)
    {
        var c=await _context.Comunidades.SingleOrDefaultAsync(c=>c.IdComunidad==id);if(c is null)return NotFound();
        int me=ObtenerIdUsuarioActual()??0;if(c.IdCreador!=me)return Forbid();
        if(!ModelState.IsValid||id!=model.Id||!CategoriasPermitidas.Contains(model.Category))return BadRequest("Datos de comunidad inválidos.");
        if(await _context.Comunidades.AnyAsync(x=>x.Nombre==model.Name.Trim()&&x.IdComunidad!=id))return Conflict("El nombre ya está en uso.");
        c.Nombre=model.Name.Trim();c.Descripcion=(model.Description??"").Trim();c.Reglas=(model.Rules??"").Trim();c.Categoria=model.Category;c.Privada=model.Private;
        await _context.SaveChangesAsync(HttpContext.RequestAborted);TempData["Notice"]="Comunidad actualizada.";return RedirectToAction(nameof(Administrar),new{id});
    }
    [HttpPost("Miembro/{id:int}"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Miembro(int id,int user,string operation)
    {
        int me=ObtenerIdUsuarioActual()??0;if(!await _context.Comunidades.AnyAsync(c=>c.IdComunidad==id&&c.IdCreador==me))return Forbid();
        if(user==me||!await _context.Usuarios.AnyAsync(u=>u.IdUsuario==user&&u.Estado=="Activo"))return BadRequest();
        var member=await _context.MiembrosComunidad.SingleOrDefaultAsync(m=>m.IdComunidad==id&&m.IdUsuario==user);
        if(operation=="invitar") {
            if(member is not null)return Conflict("La persona ya tiene una membresía o solicitud.");
            _context.MiembrosComunidad.Add(new(){IdComunidad=id,IdUsuario=user,Estado="Invitado",FechaUnion=DateTime.Now});
            await new SocialAccess(_context).Notify(user,"Invitacion",id);
        } else if(member is null)return NotFound();
        else if(operation=="aceptar"&&member.Estado=="Pendiente"){member.Estado="Aceptado";await new SocialAccess(_context).Notify(user,"ComunidadAceptada",id);}
        else if(operation=="expulsar")member.Estado="Bloqueado";
        else if(operation=="desbloquear"&&member.Estado=="Bloqueado")member.Estado="Aceptado";
        else return BadRequest();
        await _context.SaveChangesAsync(HttpContext.RequestAborted);return RedirectToAction(nameof(Administrar),new{id});
    }
    [HttpPost("Abandonar/{id:int}"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Abandonar(int id)
    {
        int me=ObtenerIdUsuarioActual()??0;var membership=await _context.MiembrosComunidad.SingleOrDefaultAsync(m=>m.IdUsuario==me&&m.IdComunidad==id);
        if(membership is null)return NotFound();if(membership.Estado=="Bloqueado")return Forbid();
        _context.MiembrosComunidad.Remove(membership);await _context.SaveChangesAsync(HttpContext.RequestAborted);return RedirectToAction(nameof(Index));
    }
}
