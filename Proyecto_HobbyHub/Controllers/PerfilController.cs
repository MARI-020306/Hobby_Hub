using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Proyecto_HobbyHub.Models;
using Proyecto_HobbyHub.ViewModels.Perfil;
using Proyecto_HobbyHub.ViewModels.Social;

namespace Proyecto_HobbyHub.Controllers;

[Authorize]
[Route("[controller]")]
public class PerfilController : Controller
{
    private const string ImagenPerfilDefault = "https://wallpapers.com/images/hd/minecraft-creeper-face-kx9n07w7stq37dwy.jpg";
    private const string PortadaDefault = "https://dotesports.com/wp-content/uploads/2023/06/cherry-blossom-grove-in-minecraft.png";

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

        await EnsureUsuarioImagenesAsync();

        var usuario = await _context.Usuarios
            .Include(u => u.Rol)
            .Include(u => u.UsuarioImagenes)
            .Include(u => u.Publicaciones)
                .ThenInclude(p => p.PublicacionLikes)
            .Include(u => u.Publicaciones)
                .ThenInclude(p => p.Comentarios)
                    .ThenInclude(c => c.IdUsuarioNavigation)
            .FirstOrDefaultAsync(u => u.IdUsuario == idUsuario.Value);

        if (usuario == null)
            return RedirectToAction("Logout", "Auth");

        var publicaciones = usuario.Publicaciones
            .Where(p => p.Estado == "Visible")
            .OrderByDescending(p => p.FechaPublicacion)
            .Take(10)
            .Select(p => new PublicacionItemViewModel
            {
                IdPublicacion = p.IdPublicacion,
                IdUsuario = p.IdUsuario,
                AutorNombre = usuario.Nombre,
                AutorAvatarUrl = ObtenerImagen(usuario, "Perfil", ImagenPerfilDefault),
                Contenido = p.Contenido,
                ImagenUrl = p.ImagenUrl,
                FechaPublicacion = p.FechaPublicacion,
                TotalLikes = p.PublicacionLikes.Count,
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
            .ToList();

        var model = new PerfilViewModel
        {
            IdUsuario = usuario.IdUsuario,
            Nombre = usuario.Nombre,
            Correo = usuario.Correo,
            Celular = usuario.Celular,
            Direccion = usuario.Direccion,
            Rol = usuario.Rol?.Nombre ?? "Usuario",
            ImagenPerfilUrl = ObtenerImagen(usuario, "Perfil", ImagenPerfilDefault),
            PortadaUrl = ObtenerImagen(usuario, "Portada", PortadaDefault),
            TotalPublicaciones = usuario.Publicaciones.Count,
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

        await EnsureUsuarioImagenesAsync();

        var usuario = await _context.Usuarios
            .Include(u => u.UsuarioImagenes)
            .FirstOrDefaultAsync(u => u.IdUsuario == idUsuario.Value);

        if (usuario == null)
            return RedirectToAction("Logout", "Auth");

        var model = new EditarPerfilViewModel
        {
            Nombre = usuario.Nombre,
            Celular = usuario.Celular,
            Direccion = usuario.Direccion,
            ImagenPerfilUrl = ObtenerImagen(usuario, "Perfil", string.Empty),
            PortadaUrl = ObtenerImagen(usuario, "Portada", string.Empty)
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

        await EnsureUsuarioImagenesAsync();

        var usuario = await _context.Usuarios
            .Include(u => u.UsuarioImagenes)
            .FirstOrDefaultAsync(u => u.IdUsuario == idUsuario.Value);

        if (usuario == null)
            return RedirectToAction("Logout", "Auth");

        usuario.Nombre = model.Nombre.Trim();
        usuario.Celular = NormalizarOpcional(model.Celular);
        usuario.Direccion = NormalizarOpcional(model.Direccion);

        ActualizarImagen(usuario, "Perfil", model.ImagenPerfilUrl);
        ActualizarImagen(usuario, "Portada", model.PortadaUrl);

        await _context.SaveChangesAsync();

        TempData["PerfilActualizado"] = "Tu perfil se actualizo correctamente.";

        return RedirectToAction(nameof(Index));
    }

    private int? ObtenerIdUsuarioActual()
    {
        string? claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claimValue, out int idUsuario) ? idUsuario : null;
    }

    private static string? NormalizarOpcional(string? valor)
    {
        return string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
    }

    private async Task EnsureUsuarioImagenesAsync()
    {
        await _context.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[dbo].[UsuarioImagenes]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[UsuarioImagenes]
                (
                    [IdImagen] INT IDENTITY(1, 1) NOT NULL,
                    [IdUsuario] INT NOT NULL,
                    [Url] VARCHAR(500) NOT NULL,
                    [Tipo] VARCHAR(20) NOT NULL,
                    [EsPrincipal] BIT NOT NULL
                        CONSTRAINT [DF_UsuarioImagenes_EsPrincipal] DEFAULT ((1)),
                    [FechaSubida] DATETIME NOT NULL
                        CONSTRAINT [DF_UsuarioImagenes_FechaSubida] DEFAULT (GETDATE()),
                    CONSTRAINT [PK_UsuarioImagenes] PRIMARY KEY ([IdImagen]),
                    CONSTRAINT [FK_UsuarioImagenes_Usuarios]
                        FOREIGN KEY ([IdUsuario]) REFERENCES [dbo].[Usuarios]([IdUsuario])
                );
            END
            """);
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
