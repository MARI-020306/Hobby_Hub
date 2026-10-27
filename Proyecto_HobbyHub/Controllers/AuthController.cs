using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Data.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Proyecto_HobbyHub.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Proyecto_HobbyHub.Models;
using Proyecto_HobbyHub.ViewModels;

namespace Proyecto_HobbyHub.Controllers
{
    public class AuthController : Controller
    {
        private static readonly string[] RolesRegistroPermitidos = SecurityPolicies.RegistrationRoles;

        private readonly HobbyHubContext _context;

        private readonly AccountSecurityService _security;
        private readonly ILogger<AuthController> _logger;

        public AuthController(HobbyHubContext context, AccountSecurityService security, ILogger<AuthController> logger)
        {
            _context = context;
            _security = security;
            _logger = logger;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");
            return View(new LoginViewModel { ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(SecurityPolicies.LoginAttempts)]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            HttpContext.Items["FunctionalResult"] = "Rejected";
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");
            if (!ModelState.IsValid) return View(model);
            LoginSession? session;
            try { session = await _security.LoginAsync(model.Email, model.Password, HttpContext.RequestAborted); }
            catch (Exception error) when (error is DbException or DbUpdateException or InvalidOperationException)
            {
                PersistenceDiagnostics.Log(_logger, error, HttpContext, StatusCodes.Status503ServiceUnavailable);
                Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                ViewBag.Error = "No fue posible iniciar sesión. Inténtalo más tarde.";
                return View(model);
            }
            if (session is null)
            {
                ViewBag.Error = "No fue posible iniciar sesión. Revisa tus datos o inténtalo más tarde.";
                return View(model);
            }
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, session.Principal,
                new AuthenticationProperties
                {
                    IsPersistent = false, AllowRefresh = false,
                    IssuedUtc = session.IssuedUtc, ExpiresUtc = session.ExpiresUtc
                });
            HttpContext.Items["FunctionalResult"] = "Authenticated";
            if (Url.IsLocalUrl(model.ReturnUrl)) return LocalRedirect(model.ReturnUrl!);
            return RedirectToAction("Index", "Home");
        }

        // GET: /Auth/Registro
        [HttpGet]
        [AllowAnonymous]
        [Route("Auth/Registro")]
        public async Task<IActionResult> Registro()
        {
            var model = new RegistroViewModel();
            await CargarRolesDisponiblesAsync(model);
            return View(model);
        }

        // POST: /Auth/Registro
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registro(RegistroViewModel model)
        {
            HttpContext.Items["FunctionalResult"] = "Rejected";
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
                Nombre = model.Nombre.Trim(),

                Correo = correoHash,

                Celular = celularNormalizado is null
                    ? null
                    : SecurityHelper.HashPersonalData(celularNormalizado),

                Direccion = direccionNormalizada is null
                    ? null
                    : SecurityHelper.HashPersonalData(direccionNormalizada),

                Password = SecurityHelper.HashPassword(model.Password),

                RolId = rolUsuario.IdRol,

                Estado = "Activo",

                FechaRegistro = DateTime.Now
            };

            _context.Usuarios.Add(nuevoUsuario);

            try { await _context.SaveChangesAsync(HttpContext.RequestAborted); }
            catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 })
            {
                // A concurrent registration may pass the earlier existence check.
                _context.Entry(nuevoUsuario).State = EntityState.Detached;
                ModelState.AddModelError("Correo", "No fue posible registrar la cuenta con estos datos.");
                await CargarRolesDisponiblesAsync(model);
                return View(model);
            }

            TempData["Success"] =
                "Registro exitoso. Por favor inicia sesión.";
            HttpContext.Items["FunctionalResult"] = "AccountCreated";

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
                .ToListAsync(HttpContext.RequestAborted);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            try { await _security.LogoutAsync(User, HttpContext.RequestAborted); }
            catch (Exception error) when (error is DbException or DbUpdateException or InvalidOperationException)
            {
                _logger.LogWarning("No fue posible revocar la sesión en el servidor.");
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    "No fue posible completar el cierre de sesión. Inténtalo de nuevo más tarde.");
            }
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccesoDenegado()
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return View();
        }
    }
}
