using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Proyecto_HobbyHub.Models;
using Proyecto_HobbyHub.ViewModels.Social;
using Proyecto_HobbyHub.Security;

namespace Proyecto_HobbyHub.Controllers;

public class HomeController : AuthenticatedController
{
    private const string AvatarDefault = "/images/avatar-default.svg";

    private readonly HobbyHubContext _context;
    private readonly ILogger<HomeController> _logger;

    public HomeController(HobbyHubContext context, ILogger<HomeController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [Authorize]
    public IActionResult Index() => RedirectToAction("Inicio", "Social");

    [Authorize]
    public async Task<IActionResult> InicioClasico()
    {
        int? idUsuario = ObtenerIdUsuarioActual();
        if (idUsuario is null)
            return RedirectToAction("Login", "Auth");

        var usuario = await _context.Usuarios
            .Include(u => u.UsuarioImagenes)
            .FirstOrDefaultAsync(u => u.IdUsuario == idUsuario.Value);

        if (usuario is null)
            return await ExpireAuthenticationAsync();

        var model = new InicioViewModel
        {
            NombreUsuario = usuario.Nombre,
            AvatarUrl = ObtenerAvatar(usuario),
            Publicaciones = await ObtenerPublicacionesAsync(idUsuario.Value, null)
        };

        return View("Index",model);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLike(int idPublicacion, string? returnUrl)
    {
        int? idUsuario = ObtenerIdUsuarioActual();
        if (idUsuario is null)
            return RedirectToAction("Login", "Auth");

        bool existePublicacion = await _context.Publicaciones
            .AnyAsync(p => p.IdPublicacion == idPublicacion && p.Estado == "Visible");
        existePublicacion = existePublicacion && await new SocialAccess(_context).CanSeePost(idUsuario.Value,idPublicacion);

        if (!existePublicacion)
            return RedirigirLocal(returnUrl);
        if (!ResourceAccess.Allows(User, new(0, Visible: existePublicacion), ResourceOperation.Interact)) return Forbid();

        var like = await _context.PublicacionLikes
            .FirstOrDefaultAsync(l => l.IdPublicacion == idPublicacion && l.IdUsuario == idUsuario.Value);

        if (like is null)
        {
            _context.PublicacionLikes.Add(new PublicacionLike
            {
                IdPublicacion = idPublicacion,
                IdUsuario = idUsuario.Value,
                FechaLike = DateTime.Now
            });
            var owner=await _context.Publicaciones.Where(p=>p.IdPublicacion==idPublicacion).Select(p=>p.IdUsuario).SingleAsync(HttpContext.RequestAborted);
            if(owner!=idUsuario.Value)await new SocialAccess(_context).Notify(owner,"Like",idPublicacion);
        }
        else
        {
            _context.PublicacionLikes.Remove(like);
        }

        await _context.SaveChangesAsync(HttpContext.RequestAborted);
        return RedirigirLocal(returnUrl);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Comentar(int idPublicacion, string contenido, string? returnUrl)
    {
        int? idUsuario = ObtenerIdUsuarioActual();
        if (idUsuario is null)
            return RedirectToAction("Login", "Auth");

        if (string.IsNullOrWhiteSpace(contenido) || contenido.Trim().Length > 1000)
            return RedirigirLocal(returnUrl);

        bool existePublicacion = await _context.Publicaciones
            .AnyAsync(p => p.IdPublicacion == idPublicacion && p.Estado == "Visible");

        existePublicacion = existePublicacion && await new SocialAccess(_context).CanSeePost(idUsuario.Value,idPublicacion);

        if (existePublicacion)
        {
            if (!ResourceAccess.Allows(User, new(0, Visible: existePublicacion), ResourceOperation.Interact)) return Forbid();
            _context.Comentarios.Add(new Comentario
            {
                IdPublicacion = idPublicacion,
                IdUsuario = idUsuario.Value,
                Contenido = contenido.Trim(),
                Estado = "Visible",
                FechaComentario = DateTime.Now
            });
            await _context.SaveChangesAsync(HttpContext.RequestAborted);
        }

        return RedirigirLocal(returnUrl);
    }

    [AllowAnonymous]
    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous]
    [IgnoreAntiforgeryToken] // Error re-execution can preserve POST; this action never changes data.
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        Response.StatusCode = StatusCodes.Status500InternalServerError;
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [AllowAnonymous, IgnoreAntiforgeryToken]
    [Route("/Home/Estado/{code:int}")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Estado(int code)
    {
        code = code is >= 400 and <= 599 ? code : 404;
        Response.StatusCode = code;
        var (title, description) = code switch
        {
            404 => ("Página no encontrada", "El enlace no existe o el contenido ya no está disponible para tu cuenta."),
            403 => ("Acceso denegado", "Tu cuenta no tiene permiso para realizar esta acción."),
            400 => ("Solicitud inválida", "Revisa los datos enviados y vuelve a intentarlo desde el formulario."),
            405 => ("Acción no disponible", "Utiliza los botones y formularios de la aplicación para realizar esta acción."),
            413 => ("Archivo demasiado grande", "Reduce el tamaño de las imágenes antes de volver a enviarlas."),
            429 => ("Demasiados intentos", "Espera antes de volver a intentarlo."),
            _ => ("No pudimos completar la solicitud", "Inténtalo de nuevo más tarde.")
        };
        return View(new Proyecto_HobbyHub.ViewModels.StatusPageViewModel(code, title, description));
    }

    internal async Task<IReadOnlyList<PublicacionItemViewModel>> ObtenerPublicacionesAsync(int idUsuarioActual, int? idUsuarioFiltro)
    {
        var consulta = new SocialAccess(_context).VisiblePosts(idUsuarioActual)
            .AsNoTracking()
            .AsSplitQuery()
            .Include(p => p.IdUsuarioNavigation)
                .ThenInclude(u => u.UsuarioImagenes)
            .Include(p => p.IdComunidadNavigation)
            .Include(p => p.PublicacionLikes)
            .Include(p => p.Comentarios)
                .ThenInclude(c => c.IdUsuarioNavigation)
            .Where(p => p.Estado == "Visible");

        if (idUsuarioFiltro.HasValue)
            consulta = consulta.Where(p => p.IdUsuario == idUsuarioFiltro.Value);

        var publicaciones = await consulta
            .OrderByDescending(p => p.FechaPublicacion)
            .ThenByDescending(p => p.IdPublicacion)
            .Take(30)
            .ToListAsync(HttpContext.RequestAborted);

        return publicaciones.Select(p => new PublicacionItemViewModel
        {
            IdPublicacion = p.IdPublicacion,
            IdUsuario = p.IdUsuario,
            AutorNombre = p.IdUsuarioNavigation.Nombre,
            AutorAvatarUrl = ObtenerAvatar(p.IdUsuarioNavigation),
            IdComunidad = p.IdComunidad,
            NombreComunidad = p.IdComunidadNavigation.Nombre,
            Contenido = p.Contenido,
            ImagenUrl = p.ImagenUrl,
            FechaPublicacion = p.FechaPublicacion,
            TotalLikes = p.PublicacionLikes.Count,
            LeGustaAlUsuarioActual = p.PublicacionLikes.Any(l => l.IdUsuario == idUsuarioActual),
            TotalComentarios = p.Comentarios.Count(c => c.Estado == "Visible"),
            Comentarios = p.Comentarios
                .Where(c => c.Estado == "Visible")
                .OrderBy(c => c.FechaComentario)
                .Take(5)
                .Select(c => new ComentarioItemViewModel
                {
                    AutorNombre = c.IdUsuarioNavigation.Nombre,
                    Contenido = c.Contenido,
                    FechaComentario = c.FechaComentario
                })
                .ToList()
        }).ToList();
    }

    internal static string ObtenerAvatar(Usuario usuario)
    {
        return usuario.UsuarioImagenes
            .Where(i => i.Tipo == "Perfil" && i.EsPrincipal)
            .OrderByDescending(i => i.FechaSubida)
            .Select(i => i.Url)
            .FirstOrDefault() ?? AvatarDefault;
    }

    private int? ObtenerIdUsuarioActual()
    {
        string? claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claimValue, out int idUsuario) ? idUsuario : null;
    }

    private IActionResult RedirigirLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToAction(nameof(Index));
    }

}
