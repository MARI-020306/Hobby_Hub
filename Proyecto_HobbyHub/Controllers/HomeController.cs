using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Proyecto_HobbyHub.Models;
using Proyecto_HobbyHub.ViewModels.Social;

namespace Proyecto_HobbyHub.Controllers;

public class HomeController : Controller
{
    private const string AvatarDefault = "https://wallpapers.com/images/hd/minecraft-creeper-face-kx9n07w7stq37dwy.jpg";

    private readonly HobbyHubContext _context;
    private readonly ILogger<HomeController> _logger;

    public HomeController(HobbyHubContext context, ILogger<HomeController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [Authorize]
    public async Task<IActionResult> Index()
    {
        int? idUsuario = ObtenerIdUsuarioActual();
        if (idUsuario is null)
            return RedirectToAction("Login", "Auth");

        await EnsureSocialSchemaAsync();

        var usuario = await _context.Usuarios
            .Include(u => u.UsuarioImagenes)
            .FirstOrDefaultAsync(u => u.IdUsuario == idUsuario.Value);

        if (usuario is null)
            return RedirectToAction("Logout", "Auth");

        var model = new InicioViewModel
        {
            NombreUsuario = usuario.Nombre,
            AvatarUrl = ObtenerAvatar(usuario),
            Publicaciones = await ObtenerPublicacionesAsync(idUsuario.Value, null)
        };

        return View(model);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLike(int idPublicacion, string? returnUrl)
    {
        int? idUsuario = ObtenerIdUsuarioActual();
        if (idUsuario is null)
            return RedirectToAction("Login", "Auth");

        await EnsureSocialSchemaAsync();

        bool existePublicacion = await _context.Publicaciones
            .AnyAsync(p => p.IdPublicacion == idPublicacion && p.Estado == "Visible");

        if (!existePublicacion)
            return RedirigirLocal(returnUrl);

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
        }
        else
        {
            _context.PublicacionLikes.Remove(like);
        }

        await _context.SaveChangesAsync();
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

        if (existePublicacion)
        {
            _context.Comentarios.Add(new Comentario
            {
                IdPublicacion = idPublicacion,
                IdUsuario = idUsuario.Value,
                Contenido = contenido.Trim(),
                Estado = "Visible",
                FechaComentario = DateTime.Now
            });
            await _context.SaveChangesAsync();
        }

        return RedirigirLocal(returnUrl);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    internal async Task<IReadOnlyList<PublicacionItemViewModel>> ObtenerPublicacionesAsync(int idUsuarioActual, int? idUsuarioFiltro)
    {
        var consulta = _context.Publicaciones
            .AsNoTracking()
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
            .Take(30)
            .ToListAsync();

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

    private async Task EnsureSocialSchemaAsync()
    {
        await _context.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH(N'dbo.Publicaciones', N'ImagenUrl') IS NULL
                ALTER TABLE [dbo].[Publicaciones] ADD [ImagenUrl] VARCHAR(500) NULL;

            IF OBJECT_ID(N'[dbo].[PublicacionLikes]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[PublicacionLikes]
                (
                    [IdLike] INT IDENTITY(1, 1) NOT NULL,
                    [IdPublicacion] INT NOT NULL,
                    [IdUsuario] INT NOT NULL,
                    [FechaLike] DATETIME NOT NULL
                        CONSTRAINT [DF_PublicacionLikes_FechaLike] DEFAULT (GETDATE()),
                    CONSTRAINT [PK_PublicacionLikes] PRIMARY KEY ([IdLike]),
                    CONSTRAINT [UQ_PublicacionLikes_Publicacion_Usuario]
                        UNIQUE ([IdPublicacion], [IdUsuario]),
                    CONSTRAINT [FK_PublicacionLikes_Publicaciones]
                        FOREIGN KEY ([IdPublicacion]) REFERENCES [dbo].[Publicaciones]([IdPublicacion]),
                    CONSTRAINT [FK_PublicacionLikes_Usuarios]
                        FOREIGN KEY ([IdUsuario]) REFERENCES [dbo].[Usuarios]([IdUsuario])
                );
            END
            """);
    }
}
