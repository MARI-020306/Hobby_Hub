using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Proyecto_HobbyHub.Models;
using Proyecto_HobbyHub.ViewModels;

namespace Proyecto_HobbyHub.Controllers
{
    public class AuthController : Controller
    {
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

            string correoNormalizado = model.Email.Trim().ToLower();

            var usuario = await _context.Usuarios
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.Correo == correoNormalizado);

            // Si no existe el usuario
            if (usuario == null)
            {
                ViewBag.Error =
                    "Correo electrónico o contraseña incorrectos.";

                return View(model);
            }

            if (string.IsNullOrWhiteSpace(usuario.Password) ||
                !BCrypt.Net.BCrypt.Verify(model.Password, usuario.Password))
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
            ViewBag.Roles = await _context.Roles.ToListAsync();

            return View();
        }

        // POST: /Auth/Registro
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registro(RegistroViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Roles = await _context.Roles.ToListAsync();
                return View(model);
            }

            string correoNormalizado = model.Correo.Trim().ToLower();

            // Verificar si ya existe un usuario con ese correo
            var usuarioExiste = await _context.Usuarios
                .AnyAsync(u => u.Correo == correoNormalizado);

            if (usuarioExiste)
            {
                ModelState.AddModelError(
                    "Correo",
                    "El correo electrónico ya está registrado.");

                ViewBag.Roles = await _context.Roles.ToListAsync();

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

            var nuevoUsuario = new Usuario
            {
                Nombre = model.Nombre,

                Correo = correoNormalizado,

                Celular = celularNormalizado,

                Direccion = direccionNormalizada,

                Password = BCrypt.Net.BCrypt.HashPassword(model.Password),

                RolId = model.RolId,

                Estado = "Activo",

                FechaRegistro = DateTime.Now
            };

            _context.Usuarios.Add(nuevoUsuario);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Registro exitoso. Por favor inicia sesión.";

            return RedirectToAction("Login");
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
