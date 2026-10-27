using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Proyecto_HobbyHub.Models;
using Proyecto_HobbyHub.ViewModels.Perfil;
using Proyecto_HobbyHub.ViewModels.Social;
using Proyecto_HobbyHub.Security;

namespace Proyecto_HobbyHub.Controllers;

[Authorize]
[Route("[controller]")]
public class PerfilController : AuthenticatedController
{
    private const string ImagenPerfilDefault = "/images/avatar-default.svg";
    private const string PortadaDefault = "/images/cover-default.svg";

    private readonly HobbyHubContext _context;

    public PerfilController(HobbyHubContext context)
    {
        _context = context;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index()
    {
        int? idUsuario = ObtenerIdUsuarioActual();
        if (idUsuario == null)
            return RedirectToAction("Login", "Auth");

        var usuario = await _context.Usuarios
            .AsNoTracking().AsSplitQuery()
            .Include(u => u.Rol)
            .Include(u => u.UsuarioImagenes)
            .FirstOrDefaultAsync(u => u.IdUsuario == idUsuario.Value, HttpContext.RequestAborted);

        if (usuario == null)
            return await ExpireAuthenticationAsync();

        string avatar = ObtenerImagen(usuario, "Perfil", ImagenPerfilDefault);
        var publicaciones = await new SocialAccess(_context).VisiblePosts(idUsuario.Value)
            .AsNoTracking()
            .Where(p => p.IdUsuario == idUsuario.Value)
            .OrderByDescending(p => p.FechaPublicacion).ThenByDescending(p => p.IdPublicacion)
            .Take(10)
            .Select(p => new PublicacionItemViewModel
            {
                IdPublicacion = p.IdPublicacion,
                IdUsuario = p.IdUsuario,
                AutorNombre = usuario.Nombre,
                AutorAvatarUrl = avatar,
                IdComunidad = p.IdComunidad,
                NombreComunidad = p.IdComunidadNavigation.Nombre,
                Contenido = p.Contenido,
                ImagenUrl = p.ImagenUrl,
                FechaPublicacion = p.FechaPublicacion,
                TotalLikes = p.PublicacionLikes.Count(),
                LeGustaAlUsuarioActual = p.PublicacionLikes.Any(l => l.IdUsuario == idUsuario.Value),
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
            })
            .ToListAsync(HttpContext.RequestAborted);

        var model = new PerfilViewModel
        {
            IdUsuario = usuario.IdUsuario,
            Nombre = usuario.Nombre,
            Correo = "Dato protegido",
            Celular = OcultarDatoProtegido(usuario.Celular),
            Direccion = OcultarDatoProtegido(usuario.Direccion),
            Rol = usuario.Rol?.Nombre ?? "Usuario",
            ImagenPerfilUrl = ObtenerImagen(usuario, "Perfil", ImagenPerfilDefault),
            PortadaUrl = ObtenerImagen(usuario, "Portada", PortadaDefault),
            TotalPublicaciones = publicaciones.Count,
            Publicaciones = publicaciones
        };

        return View(model);
    }

    [HttpGet("Editar")]
    public async Task<IActionResult> Editar()
    {
        int? idUsuario = ObtenerIdUsuarioActual();
        if (idUsuario == null)
            return RedirectToAction("Login", "Auth");

        var usuario = await _context.Usuarios
            .Include(u => u.UsuarioImagenes)
            .FirstOrDefaultAsync(u => u.IdUsuario == idUsuario.Value);

        if (usuario == null)
            return await ExpireAuthenticationAsync();

        var model = new EditarPerfilViewModel
        {
            Nombre = usuario.Nombre,
            // Los valores se guardan como hash, por eso no se vuelven a mostrar ni se
            // reutilizan como valor de entrada.
            Celular = null,
            Direccion = null,
            ImagenPerfilUrl = OptionalExternalImage(ObtenerImagen(usuario, "Perfil", string.Empty)),
            PortadaUrl = OptionalExternalImage(ObtenerImagen(usuario, "Portada", string.Empty))
        };

        return View(model);
    }

    [HttpPost("Editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(EditarPerfilViewModel model)
    {
        int? idUsuario = ObtenerIdUsuarioActual();
        if (idUsuario == null)
            return RedirectToAction("Login", "Auth");

        if (!ModelState.IsValid)
            return View(model);

        var usuario = await _context.Usuarios
            .Include(u => u.UsuarioImagenes)
            .FirstOrDefaultAsync(u => u.IdUsuario == idUsuario.Value);

        if (usuario == null)
            return await ExpireAuthenticationAsync();

        if (!ResourceAccess.Allows(User, new(usuario.IdUsuario), ResourceOperation.EditProfile)) return Forbid();
        usuario.Nombre = model.Nombre.Trim();

        string? celularNormalizado = NormalizarOpcional(model.Celular);
        if (celularNormalizado is not null)
            usuario.Celular = SecurityHelper.HashPersonalData(celularNormalizado);

        string? direccionNormalizada = NormalizarOpcional(model.Direccion);
        if (direccionNormalizada is not null)
            usuario.Direccion = SecurityHelper.HashPersonalData(direccionNormalizada);

        ActualizarImagen(usuario, "Perfil", model.ImagenPerfilUrl);
        ActualizarImagen(usuario, "Portada", model.PortadaUrl);

        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        TempData["PerfilActualizado"] = "Tu perfil se actualizo correctamente.";

        return RedirectToAction(nameof(Index));
    }

    private int? ObtenerIdUsuarioActual()
    {
        string? claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claimValue, out int idUsuario) ? idUsuario : null;
    }

    private static string? OptionalExternalImage(string value) => value.StartsWith("https://",StringComparison.OrdinalIgnoreCase)?value:null;

    private static string? NormalizarOpcional(string? valor)
    {
        return string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
    }

    private static string? OcultarDatoProtegido(string? valor)
    {
        return string.IsNullOrWhiteSpace(valor) ? null : "Dato protegido";
    }

    private static string ObtenerImagen(Usuario usuario, string tipo, string valorDefault)
    {
        return usuario.UsuarioImagenes
            .Where(i => i.Tipo == tipo && i.EsPrincipal)
            .OrderByDescending(i => i.FechaSubida)
            .Select(i => i.Url)
            .FirstOrDefault() ?? valorDefault;
    }

    private void ActualizarImagen(Usuario usuario, string tipo, string? url)
    {
        string? urlNormalizada = NormalizarOpcional(url);
        var imagen = usuario.UsuarioImagenes
            .FirstOrDefault(i => i.Tipo == tipo && i.EsPrincipal);

        if (string.IsNullOrWhiteSpace(urlNormalizada))
        {
            if(imagen?.Url.StartsWith("/Social/Foto/",StringComparison.OrdinalIgnoreCase)==true)return;
            if (imagen is not null)
                _context.UsuarioImagenes.Remove(imagen);

            return;
        }

        if (imagen is null)
        {
            usuario.UsuarioImagenes.Add(new UsuarioImagen
            {
                Tipo = tipo,
                Url = urlNormalizada,
                EsPrincipal = true,
                FechaSubida = DateTime.Now
            });

            return;
        }

        imagen.Url = urlNormalizada;
        imagen.FechaSubida = DateTime.Now;
    }

}
