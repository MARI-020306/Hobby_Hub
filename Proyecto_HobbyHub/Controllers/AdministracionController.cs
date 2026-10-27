using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Proyecto_HobbyHub.Models;
using Proyecto_HobbyHub.Security;
using Proyecto_HobbyHub.ViewModels;

namespace Proyecto_HobbyHub.Controllers;

[Authorize(Policy=SecurityPolicies.Administrator),AutoValidateAntiforgeryToken]
public class AdministracionController(HobbyHubContext db) : Controller
{
    [HttpGet("/Administracion"),HttpGet("/Administracion/Index")]
    public async Task<IActionResult> Index(string? q,int page=1)
    {
        string term=(q??"").Trim();if(term.Length>100)return BadRequest();
        var users=await db.Usuarios.AsNoTracking().Include(u=>u.Rol).Where(u=>u.Nombre.Contains(term)).OrderBy(u=>u.IdUsuario).Skip((Math.Clamp(page,1,10000)-1)*30).Take(30).ToListAsync(HttpContext.RequestAborted);
        ViewBag.Roles=await db.Roles.AsNoTracking().OrderBy(r=>r.Nombre).ToListAsync(HttpContext.RequestAborted);ViewBag.Query=term;ViewBag.Page=page;
        return View(users);
    }
    [HttpPost]
    public async Task<IActionResult> Cuenta(int id,int role,string state)
    {
        int me=int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if(id==me||state is not("Activo" or "Suspendido" or "Bloqueado"))return BadRequest();
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if(!await db.Usuarios.AnyAsync(u=>u.IdUsuario==me&&u.Estado=="Activo"&&u.Rol.Nombre=="Administrador"))return Forbid();
        var user=await db.Usuarios.Include(u=>u.Rol).SingleOrDefaultAsync(u=>u.IdUsuario==id);if(user is null)return NotFound();
        var selected=await db.Roles.SingleOrDefaultAsync(r=>r.IdRol==role&&SecurityPolicies.KnownRoles.Contains(r.Nombre));if(selected is null)return BadRequest();
        if(user.Rol.Nombre=="Administrador"&&(state!="Activo"||selected.Nombre!="Administrador")&&await db.Usuarios.CountAsync(u=>u.Estado=="Activo"&&u.Rol.Nombre=="Administrador")<=1)return Conflict("Debe conservarse un administrador activo.");
        user.RolId=role;user.Estado=state;user.SuspensionHastaUtc=null;
        await db.SaveChangesAsync(HttpContext.RequestAborted);await tx.CommitAsync(HttpContext.RequestAborted);return RedirectToAction(nameof(Index));
    }
    public async Task<IActionResult> Auditoria(int? user,[FromQuery(Name="action")] string? auditAction,DateTime? from,DateTime? to,int page=1,bool? success=null)
    {
        if(!ModelState.IsValid||auditAction?.Length>64||(from.HasValue&&to.HasValue&&from.Value.Date>to.Value.Date))return BadRequest();
        page=Math.Clamp(page,1,10000);
        var query=db.AuditoriasSeguridad.AsNoTracking().AsQueryable();
        if(user.HasValue)query=query.Where(a=>a.IdActor==user||a.IdObjetivo==user);
        if(!string.IsNullOrWhiteSpace(auditAction))query=query.Where(a=>a.Accion==auditAction);
        if(from.HasValue)query=query.Where(a=>a.FechaUtc>=from);
        if(to.HasValue){if(to.Value.Date==DateTime.MaxValue.Date)query=query.Where(a=>a.FechaUtc<=DateTime.MaxValue);else{var exclusive=to.Value.Date.AddDays(1);query=query.Where(a=>a.FechaUtc<exclusive);}}
        if(success.HasValue)query=query.Where(a=>a.Exito==success);
        var entries=await query.OrderByDescending(a=>a.IdAuditoria).Skip((Math.Clamp(page,1,10000)-1)*50).Take(51).ToListAsync(HttpContext.RequestAborted);
        return View(new SocialPage{Title="Auditoría de seguridad",Audit=entries.Take(50).ToList(),Page=page,More=entries.Count>50});
    }
    public async Task<IActionResult> Comunidades() => View(await db.Comunidades.AsNoTracking().OrderBy(c=>c.Nombre).Take(100).ToListAsync(HttpContext.RequestAborted));
    [HttpPost]
    public async Task<IActionResult> Comunidad(int id,bool isPrivate)
    {
        var community=await db.Comunidades.FindAsync(id);if(community is null)return NotFound();
        community.Privada=isPrivate;db.AuditoriasSeguridad.Add(new(){Accion="AdminCommunityChanged",IdActor=int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),IdObjetivo=community.IdCreador,Exito=true,FechaUtc=DateTime.UtcNow});
        await db.SaveChangesAsync(HttpContext.RequestAborted);return RedirectToAction(nameof(Comunidades));
    }
}
