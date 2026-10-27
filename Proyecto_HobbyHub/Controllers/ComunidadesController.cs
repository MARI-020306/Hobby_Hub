using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Proyecto_HobbyHub.Models;
using Proyecto_HobbyHub.ViewModels;
using Proyecto_HobbyHub.Security;

namespace Proyecto_HobbyHub.Controllers;

[Authorize]
[Route("[controller]")]
public partial class ComunidadesController : AuthenticatedController
{
    private static readonly string[] CategoriasPermitidas =
    [
        "Videojuegos", "Música", "Fotografía", "Maquillaje", "Lectura", "Deportes", "General"
    ];

    private readonly HobbyHubContext _context;

    public ComunidadesController(HobbyHubContext context)
    {
        _context = context;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(string? busqueda, int? editar, bool mostrarFormulario = false, int page = 1)
    {
        int? idUsuario = ObtenerIdUsuarioActual();
        if (idUsuario is null)
            return RedirectToAction("Login", "Auth");

        var usuarioActual = await ObtenerUsuarioActualAsync(idUsuario.Value);
        if (usuarioActual is null)
            return await ExpireAuthenticationAsync();

        string? termino = string.IsNullOrWhiteSpace(busqueda) ? null : busqueda.Trim();
        if(termino?.Length>100)return BadRequest();
        page=Math.Clamp(page,1,10000);
        var consulta = _context.Comunidades
            .AsNoTracking()
            .AsQueryable();

        if (termino is not null)
        {
            consulta = consulta.Where(c => c.Nombre.Contains(termino) ||
                (c.Descripcion != null && c.Descripcion.Contains(termino)));
        }

        var comunidades = await consulta
            .OrderByDescending(c => c.FechaCreacion)
            .ThenByDescending(c => c.IdComunidad)
            .Skip((page-1)*20).Take(21)
            .Select(c => new ComunidadCardViewModel
            {
                IdComunidad=c.IdComunidad,Nombre=c.Nombre,Descripcion=c.Descripcion??"Sin descripción.",
                Categoria=c.Categoria??"General",ImagenPortada=c.ImagenPortada,Creador=c.IdCreadorNavigation.Nombre,
                TotalMiembros=c.Miembros.Count(m=>m.Estado=="Aceptado"&&m.IdUsuario!=c.IdCreador)+1,
                EsCreadorActual=c.IdCreador==idUsuario.Value,
                UsuarioYaUnido=c.Miembros.Any(m=>m.IdUsuario==idUsuario.Value&&m.Estado=="Aceptado"),Privada=c.Privada,
                EstadoMembresia=c.Miembros.Where(m=>m.IdUsuario==idUsuario.Value).Select(m=>m.Estado).FirstOrDefault()
            })
            .ToListAsync(HttpContext.RequestAborted);

        bool puedeCrear = EsCreador(usuarioActual);
        var formulario = new ComunidadFormViewModel();

        if (editar.HasValue && puedeCrear)
        {
            var comunidadEditar = await _context.Comunidades.AsNoTracking().FirstOrDefaultAsync(c =>
                c.IdComunidad == editar.Value && c.IdCreador == idUsuario.Value);

            if (comunidadEditar is not null)
            {
                formulario = new ComunidadFormViewModel
                {
                    IdComunidad = comunidadEditar.IdComunidad,
                    Nombre = comunidadEditar.Nombre,
                    Descripcion = comunidadEditar.Descripcion ?? string.Empty,
                    Categoria = comunidadEditar.Categoria ?? "General",
                    ImagenPortada = comunidadEditar.ImagenPortada
                };
            }
            else
            {
                TempData["ComunidadError"] = "Solo puedes editar las comunidades que creaste.";
            }
        }

        var model = new ComunidadesIndexViewModel
        {
            Busqueda = termino,
            PuedeCrear = puedeCrear,
            MostrarFormulario = puedeCrear && (mostrarFormulario || editar.HasValue),
            Formulario = formulario,
            Comunidades = comunidades.Take(20).ToList(),Page=page,More=comunidades.Count>20
        };

        return View(model);
    }

    [Authorize(Policy = SecurityPolicies.CreateCommunity)]
    [HttpPost("Guardar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar([Bind(Prefix = "Formulario")] ComunidadFormViewModel model)
    {
        int? idUsuario = ObtenerIdUsuarioActual();
        if (idUsuario is null)
            return RedirectToAction("Login", "Auth");

        var usuarioActual = await ObtenerUsuarioActualAsync(idUsuario.Value);
        if (usuarioActual is null)
            return await ExpireAuthenticationAsync();

        if (!EsCreador(usuarioActual))
            return Forbid();

        if (!ModelState.IsValid || !CategoriasPermitidas.Contains(model.Categoria))
        {
            TempData["ComunidadError"] = "Revisa el nombre, la descripción y la categoría de la comunidad.";
            return RedirectToAction(nameof(Index), new { mostrarFormulario = true, editar = model.IdComunidad });
        }

        string nombre = model.Nombre.Trim();
        bool nombreEnUso = await _context.Comunidades.AnyAsync(c =>
            c.Nombre == nombre && c.IdComunidad != model.IdComunidad);

        if (nombreEnUso)
        {
            TempData["ComunidadError"] = "Ya existe una comunidad con ese nombre.";
            return RedirectToAction(nameof(Index), new { mostrarFormulario = true, editar = model.IdComunidad });
        }

        if (model.IdComunidad.HasValue)
        {
            var comunidad = await _context.Comunidades.FirstOrDefaultAsync(c =>
                c.IdComunidad == model.IdComunidad.Value && c.IdCreador == idUsuario.Value);

            if (comunidad is null)
                return NotFound();

            if (!ResourceAccess.Allows(User, new(comunidad.IdCreador), ResourceOperation.EditCommunity))
                return Forbid();

            comunidad.Nombre = nombre;
            comunidad.Descripcion = model.Descripcion.Trim();
            comunidad.Categoria = model.Categoria;
            comunidad.ImagenPortada = NormalizarOpcional(model.ImagenPortada);
            TempData["ComunidadExito"] = "Comunidad actualizada correctamente.";
        }
        else
        {
            _context.Comunidades.Add(new Comunidade
            {
                Nombre = nombre,
                Descripcion = model.Descripcion.Trim(),
                Categoria = model.Categoria,
                ImagenPortada = NormalizarOpcional(model.ImagenPortada),
                IdCreador = idUsuario.Value,
                FechaCreacion = DateTime.Now
            });
            TempData["ComunidadExito"] = "Comunidad creada correctamente.";
        }

        await _context.SaveChangesAsync(HttpContext.RequestAborted);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Unirse/{idComunidad:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unirse(int idComunidad)
    {
        int? idUsuario = ObtenerIdUsuarioActual();
        if (idUsuario is null)
            return RedirectToAction("Login", "Auth");

        var comunidad = await _context.Comunidades
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.IdComunidad == idComunidad);

        if (comunidad is null)
            return NotFound();

        if (comunidad.IdCreador == idUsuario.Value)
        {
            TempData["ComunidadError"] = "Ya eres la persona creadora de esta comunidad.";
            return RedirectToAction(nameof(Index));
        }

        bool yaEsMiembro = await _context.MiembrosComunidad.AnyAsync(m =>
            m.IdComunidad == idComunidad && m.IdUsuario == idUsuario.Value);

        if (yaEsMiembro)
        {
            var invitation=await _context.MiembrosComunidad.SingleAsync(m=>m.IdComunidad==idComunidad&&m.IdUsuario==idUsuario.Value);
            if(invitation.Estado=="Invitado") {invitation.Estado="Aceptado";await _context.SaveChangesAsync(HttpContext.RequestAborted);TempData["ComunidadExito"]="Invitación aceptada.";return RedirectToAction(nameof(Detalle),new{idComunidad});}
            TempData["ComunidadError"] = "Ya te uniste a esta comunidad.";
            return RedirectToAction(nameof(Index));
        }

        _context.MiembrosComunidad.Add(new MiembroComunidad
        {
            IdComunidad = idComunidad,
            IdUsuario = idUsuario.Value,
            FechaUnion = DateTime.Now
            ,Estado = comunidad.Privada ? "Pendiente" : "Aceptado"
        });
        await new SocialAccess(_context).Notify(comunidad.IdCreador,comunidad.Privada?"SolicitudComunidad":"Miembro",idComunidad);
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        TempData["ComunidadExito"] = "Ahora formas parte de la comunidad.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Eliminar/{idComunidad:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarComunidad(int idComunidad)
    {
        int? idUsuario = ObtenerIdUsuarioActual();
        if (idUsuario is null)
            return RedirectToAction("Login", "Auth");

        var comunidad = await _context.Comunidades
            .FirstOrDefaultAsync(c => c.IdComunidad == idComunidad);

        if (comunidad is null)
            return NotFound();

        if (!ResourceAccess.Allows(User, new(comunidad.IdCreador), ResourceOperation.RemoveCommunity))
            return Forbid();

        var publicaciones = await _context.Publicaciones
            .Where(p => p.IdComunidad == idComunidad)
            .ToListAsync(HttpContext.RequestAborted);
        var idsPublicaciones = publicaciones.Select(p => p.IdPublicacion).ToList();

        var comentarios = await _context.Comentarios
            .Where(c => idsPublicaciones.Contains(c.IdPublicacion))
            .ToListAsync(HttpContext.RequestAborted);
        var idsComentarios = comentarios.Select(c => c.IdComentario).ToList();

        var likes = await _context.PublicacionLikes
            .Where(l => idsPublicaciones.Contains(l.IdPublicacion))
            .ToListAsync(HttpContext.RequestAborted);

        var reportes = await _context.Reportes
            .Where(r =>
                (r.IdPublicacion.HasValue && idsPublicaciones.Contains(r.IdPublicacion.Value)) ||
                (r.IdComentario.HasValue && idsComentarios.Contains(r.IdComentario.Value)))
            .ToListAsync(HttpContext.RequestAborted);

        var membresias = await _context.MiembrosComunidad
            .Where(m => m.IdComunidad == idComunidad)
            .ToListAsync(HttpContext.RequestAborted);
        if(reportes.Any(r=>r.Estado is "Pendiente" or "Revision"))
            return Conflict("Esta comunidad tiene reportes pendientes. Deben revisarse antes de eliminar su contenido.");

        var gallery=await _context.PublicacionImagenes.Where(i=>idsPublicaciones.Contains(i.IdPublicacion)).ToListAsync(HttpContext.RequestAborted);
        var saved=await _context.PublicacionesGuardadas.Where(g=>idsPublicaciones.Contains(g.IdPublicacion)).ToListAsync(HttpContext.RequestAborted);

        await using var transaccion = await _context.Database.BeginTransactionAsync(HttpContext.RequestAborted);
        // Preserve resolved report history after its related content is removed.
        foreach(var report in reportes){report.IdPublicacion=null;report.IdComentario=null;}
        _context.PublicacionImagenes.RemoveRange(gallery);
        _context.PublicacionesGuardadas.RemoveRange(saved);
        _context.PublicacionLikes.RemoveRange(likes);
        _context.Comentarios.RemoveRange(comentarios);
        _context.Publicaciones.RemoveRange(publicaciones);
        _context.MiembrosComunidad.RemoveRange(membresias);
        _context.Comunidades.Remove(comunidad);

        await _context.SaveChangesAsync(HttpContext.RequestAborted);
        await transaccion.CommitAsync(HttpContext.RequestAborted);

        TempData["ComunidadExito"] = "La comunidad y sus publicaciones fueron eliminadas.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Detalle/{idComunidad:int}")]
    public async Task<IActionResult> Detalle(int idComunidad)
    {
        int? idUsuario = ObtenerIdUsuarioActual();
        if (idUsuario is null)
            return RedirectToAction("Login", "Auth");

        var comunidad = await _context.Comunidades
            .AsNoTracking()
            .Include(c => c.IdCreadorNavigation)
            .Include(c => c.Miembros)
            .FirstOrDefaultAsync(c => c.IdComunidad == idComunidad);

        if (comunidad is null)
            return NotFound();

        bool esCreadorActual = comunidad.IdCreador == idUsuario.Value;
        bool esMiembro = comunidad.Miembros.Any(m => m.IdUsuario == idUsuario.Value && m.Estado == "Aceptado");
        if (comunidad.Privada && !esCreadorActual && !esMiembro) return Forbid();
        bool puedePublicar = esCreadorActual || esMiembro;

        var publicaciones = await new SocialAccess(_context).VisiblePosts(idUsuario.Value)
            .AsNoTracking()
            .Include(p => p.IdUsuarioNavigation)
            .Where(p => p.IdComunidad == idComunidad && p.Estado == "Visible")
            .OrderByDescending(p => p.FechaPublicacion)
            .ToListAsync(HttpContext.RequestAborted);

        var model = new ComunidadDetalleViewModel
        {
            IdComunidad = comunidad.IdComunidad,
            Nombre = comunidad.Nombre,
            Reglas = comunidad.Reglas,
            Descripcion = comunidad.Descripcion ?? "Esta comunidad aún no tiene una descripción.",
            Categoria = comunidad.Categoria ?? "General",
            ImagenPortada = comunidad.ImagenPortada,
            Creador = comunidad.IdCreadorNavigation.Nombre,
            TotalMiembros = ContarMiembros(comunidad),
            EsCreadorActual = esCreadorActual,
            PuedePublicar = puedePublicar,
            NuevaPublicacion = new PublicacionFormViewModel { IdComunidad = idComunidad },
            Publicaciones = publicaciones.Select(p => new PublicacionCardViewModel
            {
                IdPublicacion = p.IdPublicacion,
                IdUsuario = p.IdUsuario,
                Autor = p.IdUsuarioNavigation.Nombre,
                Contenido = p.Contenido,
                ImagenUrl = p.ImagenUrl,
                FechaPublicacion = p.FechaPublicacion,
                PuedeEliminar = esCreadorActual || p.IdUsuario == idUsuario.Value,
                PuedeEditar = p.IdUsuario == idUsuario.Value,
                PuedeReportar = esCreadorActual && p.IdUsuario != idUsuario.Value
            }).ToList()
        };

        return View(model);
    }

    [HttpPost("Publicar/{idComunidad:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publicar(int idComunidad, [Bind(Prefix = "NuevaPublicacion")] PublicacionFormViewModel model)
    {
        int? idUsuario = ObtenerIdUsuarioActual();
        if (idUsuario is null)
            return RedirectToAction("Login", "Auth");

        if (model.IdComunidad != idComunidad || !ModelState.IsValid)
        {
            TempData["PublicacionError"] = "Revisa el texto (máximo 2,000 caracteres) y el enlace de la imagen.";
            return RedirectToAction(nameof(Detalle), new { idComunidad });
        }

        var comunidad = await _context.Comunidades
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.IdComunidad == idComunidad);

        if (comunidad is null)
            return NotFound();

        bool esMiembro = await _context.MiembrosComunidad.AnyAsync(m =>
            m.IdComunidad == idComunidad && m.IdUsuario == idUsuario.Value && m.Estado == "Aceptado");

        if (!ResourceAccess.Allows(User, new(0, comunidad.IdCreador, esMiembro), ResourceOperation.Publish))
        {
            TempData["PublicacionError"] = "Únete a la comunidad antes de publicar.";
            return RedirectToAction(nameof(Detalle), new { idComunidad });
        }

        _context.Publicaciones.Add(new Publicacione
        {
            Contenido = model.Contenido.Trim(),
            ImagenUrl = NormalizarOpcional(model.ImagenUrl),
            IdComunidad = idComunidad,
            IdUsuario = idUsuario.Value,
            Estado = "Visible",
            FechaPublicacion = DateTime.Now
        });
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        TempData["PublicacionExito"] = "Tu publicación ya aparece en la comunidad.";
        return RedirectToAction(nameof(Detalle), new { idComunidad });
    }

    [HttpPost("EliminarPublicacion/{idPublicacion:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarPublicacion(int idPublicacion, int idComunidad)
    {
        int? idUsuario = ObtenerIdUsuarioActual();
        if (idUsuario is null)
            return RedirectToAction("Login", "Auth");

        var publicacion = await _context.Publicaciones
            .FirstOrDefaultAsync(p => p.IdPublicacion == idPublicacion && p.IdComunidad == idComunidad);

        if (publicacion is null)
            return NotFound();

        bool esCreadorActual = await _context.Comunidades.AnyAsync(c =>
            c.IdComunidad == idComunidad && c.IdCreador == idUsuario.Value);

        if (!ResourceAccess.Allows(User, new(publicacion.IdUsuario, esCreadorActual ? idUsuario.Value : 0), ResourceOperation.RemovePublication))
            return Forbid();

        publicacion.Estado = "Eliminada";
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        TempData["PublicacionExito"] = "La publicación fue retirada de la comunidad.";
        return RedirectToAction(nameof(Detalle), new { idComunidad });
    }

    [HttpPost("EditarPublicacion/{idPublicacion:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditarPublicacion(int idPublicacion, EditarPublicacionViewModel model)
    {
        int? idUsuario = ObtenerIdUsuarioActual();
        if (idUsuario is null)
            return RedirectToAction("Login", "Auth");

        if (!ModelState.IsValid)
        {
            TempData["PublicacionError"] = "Revisa el texto (máximo 2,000 caracteres) y el enlace de la imagen.";
            return RedirectToAction(nameof(Detalle), new { idComunidad = model.IdComunidad });
        }

        var publicacion = await _context.Publicaciones
            .FirstOrDefaultAsync(p => p.IdPublicacion == idPublicacion &&
                p.IdComunidad == model.IdComunidad &&
                p.Estado == "Visible");

        if (publicacion is null)
            return NotFound();

        if (!ResourceAccess.Allows(User, new(publicacion.IdUsuario, Visible: publicacion.Estado == "Visible"), ResourceOperation.EditPublication))
            return Forbid();
        if (!await new SocialAccess(_context).CanPublish(idUsuario.Value, model.IdComunidad))
            return Forbid();

        publicacion.Contenido = model.Contenido.Trim();
        publicacion.ImagenUrl = NormalizarOpcional(model.ImagenUrl);
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        TempData["PublicacionExito"] = "Tu publicación fue actualizada.";
        return RedirectToAction(nameof(Detalle), new { idComunidad = model.IdComunidad });
    }

    [HttpPost("ReportarUsuario")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReportarUsuario(ReporteUsuarioViewModel model)
    {
        int? idUsuario = ObtenerIdUsuarioActual();
        if (idUsuario is null)
            return RedirectToAction("Login", "Auth");

        if (!ModelState.IsValid)
        {
            TempData["PublicacionError"] = "Indica el motivo del reporte (máximo 255 caracteres).";
            return RedirectToAction(nameof(Detalle), new { idComunidad = model.IdComunidad });
        }

        var publicacion = await _context.Publicaciones
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.IdPublicacion == model.IdPublicacion &&
                p.IdComunidad == model.IdComunidad &&
                p.IdUsuario == model.IdUsuarioReportado &&
                p.Estado == "Visible");

        if (publicacion is null)
            return NotFound();

        bool esCreadorActual = await _context.Comunidades.AnyAsync(c =>
            c.IdComunidad == model.IdComunidad && c.IdCreador == idUsuario.Value);
        if (!await new SocialAccess(_context).CanSeePost(idUsuario.Value, publicacion.IdPublicacion))
            return Forbid();

        if (!ResourceAccess.Allows(User, new(publicacion.IdUsuario, esCreadorActual ? idUsuario.Value : 0,
            Visible: publicacion.Estado == "Visible"), ResourceOperation.Report))
            return Forbid();

        if (model.IdUsuarioReportado == idUsuario.Value)
            return BadRequest();

        await using var reportTransaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, HttpContext.RequestAborted);
        bool reportePendiente = await _context.Reportes.AnyAsync(r =>
            r.IdPublicacion == model.IdPublicacion &&
            r.IdUsuarioReporta == idUsuario.Value &&
            r.Estado == "Pendiente");

        if (reportePendiente)
        {
            TempData["PublicacionError"] = "Ya enviaste un reporte pendiente para esta publicación.";
            return RedirectToAction(nameof(Detalle), new { idComunidad = model.IdComunidad });
        }

        _context.Reportes.Add(new Reporte
        {
            IdUsuarioReporta = idUsuario.Value,
            IdUsuarioReportado = model.IdUsuarioReportado,
            IdPublicacion = model.IdPublicacion,
            Motivo = model.Motivo.Trim(),
            Estado = "Pendiente",
            FechaReporte = DateTime.Now
        });
        await _context.SaveChangesAsync(HttpContext.RequestAborted);
        await reportTransaction.CommitAsync(HttpContext.RequestAborted);

        TempData["PublicacionExito"] = "El reporte fue registrado y quedó pendiente de revisión.";
        return RedirectToAction(nameof(Detalle), new { idComunidad = model.IdComunidad });
    }

    private async Task<Usuario?> ObtenerUsuarioActualAsync(int idUsuario)
    {
        return await _context.Usuarios
            .Include(u => u.Rol)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.IdUsuario == idUsuario);
    }

    private static bool EsCreador(Usuario usuario)
    {
        return string.Equals(usuario.Rol?.Nombre, "Creador", StringComparison.OrdinalIgnoreCase);
    }

    private int? ObtenerIdUsuarioActual()
    {
        string? claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claimValue, out int idUsuario) ? idUsuario : null;
    }

    private static string? NormalizarOpcional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static int ContarMiembros(Comunidade comunidad)
    {
        return comunidad.Miembros.Where(m => m.Estado == "Aceptado")
            .Select(m => m.IdUsuario)
            .Append(comunidad.IdCreador)
            .Distinct()
            .Count();
    }

}
