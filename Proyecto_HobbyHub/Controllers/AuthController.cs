using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Proyecto_HobbyHub.Models;
using Proyecto_HobbyHub.ViewModels;

namespace Proyecto_HobbyHub.Controllers
{
    public class AuthController : Controller
    {
        private static readonly string[] RolesRegistroPermitidos = ["Usuario", "Creador"];

        private readonly HobbyHubContext _context;

        public AuthController(HobbyHubContext context)
        {
            _context = context;
        }

        // GET: /Auth/Login
        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            return View();
        }

        // POST: /Auth/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            string correoHash = SecurityHelper.HashEmail(model.Email);

            var usuario = await _context.Usuarios
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.Correo == correoHash);

            // Si no existe el usuario
            if (usuario == null)
            {
                ViewBag.Error =
                    "Correo electrónico o contraseña incorrectos.";

                return View(model);
            }

            if (!SecurityHelper.VerifyPassword(model.Password, usuario.Password))
            {
                ViewBag.Error =
                    "Correo electrónico o contraseña incorrectos.";

                return View(model);
            }

            // Verificar estado de la cuenta
            if (usuario.Estado != "Activo")
            {
                ViewBag.Error =
                    $"Tu cuenta se encuentra en estado '{usuario.Estado}'. " +
                    "Contacta al administrador.";

                return View(model);
            }

            // Crear claims
            var claims = new List<Claim>
    {
        new Claim(
            ClaimTypes.NameIdentifier,
            usuario.IdUsuario.ToString()),

        new Claim(
            ClaimTypes.Name,
            usuario.Nombre ?? "Usuario"),

        new Claim(
            ClaimTypes.Role,
            usuario.Rol?.Nombre ?? "Usuario")
    };

            var claimsIdentity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity));

            // Login correcto
            return RedirectToAction("Index", "Home");
        }

        // GET: /Auth/Registro
        [HttpGet]
        [Route("Auth/Registro")]
        public async Task<IActionResult> Registro()
        {
            var model = new RegistroViewModel();
            await CargarRolesDisponiblesAsync(model);
            return View(model);
        }

        // POST: /Auth/Registro
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registro(RegistroViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await CargarRolesDisponiblesAsync(model);
                return View(model);
            }

            string correoHash = SecurityHelper.HashEmail(model.Correo);

            // Verificar si ya existe un usuario con ese correo
            var usuarioExiste = await _context.Usuarios
                .AnyAsync(u => u.Correo == correoHash);

            if (usuarioExiste)
            {
                ModelState.AddModelError(
                    "Correo",
                    "El correo electrónico ya está registrado.");

                await CargarRolesDisponiblesAsync(model);
                return View(model);
            }

            string? celularNormalizado =
                !string.IsNullOrWhiteSpace(model.Celular)
                    ? model.Celular.Trim()
                    : null;

            string? direccionNormalizada =
                !string.IsNullOrWhiteSpace(model.Direccion)
                    ? model.Direccion.Trim()
                    : null;

            var rolUsuario = await _context.Roles
                .FirstOrDefaultAsync(r => r.IdRol == model.RolId &&
                    RolesRegistroPermitidos.Contains(r.Nombre));

            if (rolUsuario is null)
            {
                ModelState.AddModelError("RolId", "Selecciona un rol válido.");
                await CargarRolesDisponiblesAsync(model);
                return View(model);
            }

            var nuevoUsuario = new Usuario
            {
                Nombre = model.Nombre,

                Correo = correoHash,

                Celular = celularNormalizado is null
                    ? null
                    : SecurityHelper.HashPersonalData(celularNormalizado),

                Direccion = direccionNormalizada is null
                    ? null
                    : SecurityHelper.HashPersonalData(direccionNormalizada),

                Password = SecurityHelper.HashPasswordToBytes(model.Password),

                RolId = rolUsuario.IdRol,

                Estado = "Activo",

                FechaRegistro = DateTime.Now
            };

            _context.Usuarios.Add(nuevoUsuario);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Registro exitoso. Por favor inicia sesión.";

            return RedirectToAction("Login");
        }

        private async Task CargarRolesDisponiblesAsync(RegistroViewModel model)
        {
            model.RolesDisponibles = await _context.Roles
                .Where(r => RolesRegistroPermitidos.Contains(r.Nombre))
                .OrderBy(r => r.Nombre)
                .Select(r => new SelectListItem
                {
                    Value = r.IdRol.ToString(),
                    Text = r.Nombre
                })
                .ToListAsync();
        }

        // GET: /Auth/Logout
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction("Login");
        }
    }
}
