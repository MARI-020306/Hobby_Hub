using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Proyecto_HobbyHub.Models;
using Proyecto_HobbyHub.ViewModels.Social;

namespace Proyecto_HobbyHub.Controllers
{
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
            if (idUsuario == null)
                return RedirectToAction("Login", "Auth");

            var usuario = await _context.Usuarios
                .Include(u => u.UsuarioImagenes)
                .FirstOrDefaultAsync(u => u.IdUsuario == idUsuario.Value);

            if (usuario == null)
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
        public async Task<IActionResult> CrearPublicacion(InicioViewModel model)
        {
            int? idUsuario = ObtenerIdUsuarioActual();
            if (idUsuario == null)
                return RedirectToAction("Login", "Auth");

            if (string.IsNullOrWhiteSpace(model.Contenido))
                ModelState.AddModelError(nameof(model.Contenido), "Escribe algo para publicar.");

            if (!ModelState.IsValid)
            {
                var usuario = await _context.Usuarios
                    .Include(u => u.UsuarioImagenes)
                    .FirstOrDefaultAsync(u => u.IdUsuario == idUsuario.Value);

                model.NombreUsuario = usuario?.Nombre ?? "Usuario";
                model.AvatarUrl = usuario != null ? ObtenerAvatar(usuario) : AvatarDefault;
                model.Publicaciones = await ObtenerPublicacionesAsync(idUsuario.Value, null);
                return View("Index", model);
            }

            int idComunidad = await ObtenerComunidadGeneralAsync(idUsuario.Value);

            var publicacion = new Publicacione
            {
                Contenido = model.Contenido.Trim(),
                ImagenUrl = NormalizarOpcional(model.ImagenUrl),
                IdUsuario = idUsuario.Value,
                IdComunidad = idComunidad,
                Estado = "Visible",
                FechaPublicacion = DateTime.Now
            };

            _context.Publicaciones.Add(publicacion);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLike(int idPublicacion, string? returnUrl)
        {
            int? idUsuario = ObtenerIdUsuarioActual();
            if (idUsuario == null)
                return RedirectToAction("Login", "Auth");

            bool existePublicacion = await _context.Publicaciones
                .AnyAsync(p => p.IdPublicacion == idPublicacion && p.Estado == "Visible");

            if (!existePublicacion)
                return RedirigirLocal(returnUrl);

            var like = await _context.PublicacionLikes
                .FirstOrDefaultAsync(l => l.IdPublicacion == idPublicacion && l.IdUsuario == idUsuario.Value);

            if (like == null)
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
            if (idUsuario == null)
                return RedirectToAction("Login", "Auth");

            if (!string.IsNullOrWhiteSpace(contenido))
            {
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
            var query = _context.Publicaciones
                .AsNoTracking()
                .Include(p => p.IdUsuarioNavigation)
                    .ThenInclude(u => u.UsuarioImagenes)
                .Include(p => p.PublicacionLikes)
                .Include(p => p.Comentarios)
                    .ThenInclude(c => c.IdUsuarioNavigation)
                .Where(p => p.Estado == "Visible");

            if (idUsuarioFiltro.HasValue)
                query = query.Where(p => p.IdUsuario == idUsuarioFiltro.Value);

            var publicaciones = await query
                .OrderByDescending(p => p.FechaPublicacion)
                .Take(30)
                .ToListAsync();

            return publicaciones.Select(p => new PublicacionItemViewModel
            {
                IdPublicacion = p.IdPublicacion,
                IdUsuario = p.IdUsuario,
                AutorNombre = p.IdUsuarioNavigation.Nombre,
                AutorAvatarUrl = ObtenerAvatar(p.IdUsuarioNavigation),
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

        private async Task<int> ObtenerComunidadGeneralAsync(int idUsuario)
        {
            var comunidad = await _context.Comunidades
                .FirstOrDefaultAsync(c => c.Nombre == "General");

            if (comunidad != null)
                return comunidad.IdComunidad;

            comunidad = new Comunidade
            {
                Nombre = "General",
                Descripcion = "Publicaciones generales de HobbyHub",
                IdCreador = idUsuario,
                FechaCreacion = DateTime.Now
            };

            _context.Comunidades.Add(comunidad);
            await _context.SaveChangesAsync();

            return comunidad.IdComunidad;
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

        private static string? NormalizarOpcional(string? valor)
        {
            return string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
        }
    }
}