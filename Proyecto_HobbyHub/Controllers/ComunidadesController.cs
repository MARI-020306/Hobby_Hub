using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Proyecto_HobbyHub.Models;
using Proyecto_HobbyHub.ViewModels;

namespace Proyecto_HobbyHub.Controllers;

[Authorize]
[Route("[controller]")]
public class ComunidadesController : Controller
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
    public async Task<IActionResult> Index(string? busqueda, int? editar, bool mostrarFormulario = false)
    {
        int? idUsuario = ObtenerIdUsuarioActual();
        if (idUsuario is null)
            return RedirectToAction("Login", "Auth");

        await EnsureCommunitySchemaAsync();

        var usuarioActual = await ObtenerUsuarioActualAsync(idUsuario.Value);
        if (usuarioActual is null)
            return RedirectToAction("Logout", "Auth");

        string? termino = string.IsNullOrWhiteSpace(busqueda) ? null : busqueda.Trim();
        var consulta = _context.Comunidades
            .AsNoTracking()
            .Include(c => c.IdCreadorNavigation)
            .Include(c => c.Miembros)
            .AsQueryable();

        if (termino is not null)
        {
            consulta = consulta.Where(c => c.Nombre.Contains(termino) ||
                (c.Descripcion != null && c.Descripcion.Contains(termino)));
        }

        var comunidades = await consulta
            .OrderByDescending(c => c.FechaCreacion)
            .ToListAsync();

        bool puedeCrear = EsCreador(usuarioActual);
        var formulario = new ComunidadFormViewModel();

        if (editar.HasValue && puedeCrear)
        {
            var comunidadEditar = comunidades.FirstOrDefault(c =>
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
            Comunidades = comunidades.Select(c => new ComunidadCardViewModel
            {
                IdComunidad = c.IdComunidad,
                Nombre = c.Nombre,
                Descripcion = c.Descripcion ?? "Sin descripción.",
                Categoria = c.Categoria ?? "General",
                ImagenPortada = c.ImagenPortada,
                Creador = c.IdCreadorNavigation.Nombre,
                TotalMiembros = c.Miembros.Count + 1,
                EsCreadorActual = c.IdCreador == idUsuario.Value,
                UsuarioYaUnido = c.Miembros.Any(m => m.IdUsuario == idUsuario.Value)
            }).ToList()
        };

        return View(model);
    }

    [HttpPost("Guardar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar([Bind(Prefix = "Formulario")] ComunidadFormViewModel model)
    {
        int? idUsuario = ObtenerIdUsuarioActual();
        if (idUsuario is null)
            return RedirectToAction("Login", "Auth");

        await EnsureCommunitySchemaAsync();

        var usuarioActual = await ObtenerUsuarioActualAsync(idUsuario.Value);
        if (usuarioActual is null)
            return RedirectToAction("Logout", "Auth");

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

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Unirse/{idComunidad:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unirse(int idComunidad)
    {
        int? idUsuario = ObtenerIdUsuarioActual();
        if (idUsuario is null)
            return RedirectToAction("Login", "Auth");

        await EnsureCommunitySchemaAsync();

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
            TempData["ComunidadError"] = "Ya te uniste a esta comunidad.";
            return RedirectToAction(nameof(Index));
        }

        _context.MiembrosComunidad.Add(new MiembroComunidad
        {
            IdComunidad = idComunidad,
            IdUsuario = idUsuario.Value,
            FechaUnion = DateTime.Now
        });
        await _context.SaveChangesAsync();

        TempData["ComunidadExito"] = "Ahora formas parte de la comunidad.";
        return RedirectToAction(nameof(Index));
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

    private async Task EnsureCommunitySchemaAsync()
    {
        await _context.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH(N'dbo.Comunidades', N'Categoria') IS NULL
                ALTER TABLE [dbo].[Comunidades] ADD [Categoria] VARCHAR(50) NULL;

            IF COL_LENGTH(N'dbo.Comunidades', N'ImagenPortada') IS NULL
                ALTER TABLE [dbo].[Comunidades] ADD [ImagenPortada] VARCHAR(500) NULL;

            IF OBJECT_ID(N'[dbo].[MiembrosComunidad]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[MiembrosComunidad]
                (
                    [IdComunidad] INT NOT NULL,
                    [IdUsuario] INT NOT NULL,
                    [FechaUnion] DATETIME NOT NULL
                        CONSTRAINT [DF_MiembrosComunidad_FechaUnion] DEFAULT (GETDATE()),
                    CONSTRAINT [PK_MiembrosComunidad] PRIMARY KEY ([IdComunidad], [IdUsuario]),
                    CONSTRAINT [FK_MiembrosComunidad_Comunidades]
                        FOREIGN KEY ([IdComunidad]) REFERENCES [dbo].[Comunidades]([IdComunidad]) ON DELETE CASCADE,
                    CONSTRAINT [FK_MiembrosComunidad_Usuarios]
                        FOREIGN KEY ([IdUsuario]) REFERENCES [dbo].[Usuarios]([IdUsuario]) ON DELETE CASCADE
                );
            END
            """);
    }
}
