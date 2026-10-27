using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Proyecto_HobbyHub.Models;
using Proyecto_HobbyHub.Security;
using Proyecto_HobbyHub.ViewModels;

namespace Proyecto_HobbyHub.Controllers;

[Authorize(Policy=SecurityPolicies.Moderation),AutoValidateAntiforgeryToken]
public class ModeracionController(HobbyHubContext db,SocialAccess access) : Controller
{
    [HttpGet("/Moderacion"),HttpGet("/Moderacion/Index")]
    public async Task<IActionResult> Index(string state="Pendiente",int page=1)
    {
        if(state is not("Pendiente" or "Revision" or "Resuelto"))return BadRequest();
        page=Math.Clamp(page,1,10000);
        var reports=await db.Reportes.AsNoTracking().Where(r=>r.Estado==state).OrderBy(r=>r.FechaReporte).ThenBy(r=>r.IdReporte).Skip((page-1)*30).Take(31).ToListAsync(HttpContext.RequestAborted);
        return View(new SocialPage{Title="Moderación",Mode=state,Page=page,More=reports.Count>30,Reports=reports.Take(30).ToList()});
    }
    public async Task<IActionResult> Detalle(int id)
    {
        var report=await db.Reportes.AsNoTracking().Include(r=>r.IdPublicacionNavigation).Include(r=>r.IdComentarioNavigation).Include(r=>r.Mensaje).SingleOrDefaultAsync(r=>r.IdReporte==id);
        if(report is null)return NotFound();
        int? post=report.IdPublicacion??report.IdComentarioNavigation?.IdPublicacion;
        ViewBag.Images=await db.PublicacionImagenes.AsNoTracking().Where(i=>i.IdPublicacion==post).OrderBy(i=>i.Orden).ToListAsync(HttpContext.RequestAborted);
        return View(report);
    }
    [HttpPost]
    public async Task<IActionResult> Resolver(int id,string operation,string? reason,int days=7)
    {
        if(string.IsNullOrWhiteSpace(reason)||reason.Length>500||days<1||days>30||operation is not("resolver" or "ocultar" or "suspender" or "revision"))return BadRequest();
        await using var tx=await db.Database.BeginTransactionAsync(HttpContext.RequestAborted);
        var report=await db.Reportes.Include(r=>r.IdPublicacionNavigation).Include(r=>r.IdComentarioNavigation).Include(r=>r.Mensaje).Include(r=>r.IdUsuarioReportadoNavigation).SingleOrDefaultAsync(r=>r.IdReporte==id);
        if(report is null)return NotFound();if(report.Estado=="Resuelto")return Conflict("El reporte ya fue resuelto.");
        if(operation=="suspender") {
            var target=report.IdUsuarioReportadoNavigation;if(target is null)return BadRequest();
            var role=await db.Roles.Where(r=>r.IdRol==target.RolId).Select(r=>r.Nombre).SingleAsync(HttpContext.RequestAborted);
            if(role is "Administrador" or "Moderador")return Forbid();target.Estado="Suspendido";target.SuspensionHastaUtc=DateTime.UtcNow.AddDays(days);
        }
        if(operation=="ocultar") {
            if(report.IdPublicacionNavigation is null&&report.IdComentarioNavigation is null&&report.Mensaje is null)return BadRequest();
            if(report.IdPublicacionNavigation is not null)report.IdPublicacionNavigation.Estado="Oculta";
            if(report.IdComentarioNavigation is not null)report.IdComentarioNavigation.Estado="Oculto";
            if(report.Mensaje is not null)report.Mensaje.Estado="Oculto";
        }
        report.Estado=operation=="revision"?"Revision":"Resuelto";report.Resolucion=reason.Trim();
        db.AuditoriasSeguridad.Add(new(){Accion="ModerationAction",IdActor=int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),IdObjetivo=report.IdUsuarioReportado,FechaUtc=DateTime.UtcNow,Exito=true});
        await access.Notify(report.IdUsuarioReporta,"ReporteRevisado",id);await db.SaveChangesAsync(HttpContext.RequestAborted);await tx.CommitAsync(HttpContext.RequestAborted);return RedirectToAction(nameof(Detalle),new{id});
    }
}
