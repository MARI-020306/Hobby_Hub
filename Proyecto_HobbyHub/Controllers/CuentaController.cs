using System.Data.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Proyecto_HobbyHub.Security;
using Proyecto_HobbyHub.ViewModels;

namespace Proyecto_HobbyHub.Controllers;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class CuentaController(AccountRecoveryService recovery, AccountSecurityService security,
    ILogger<CuentaController> logger) : Controller
{
    [HttpGet]
    public IActionResult Seguridad() => View(new ChangePasswordViewModel());

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("AccountRecovery")]
    public async Task<IActionResult> Seguridad(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        try
        {
            if (!await recovery.ChangeAsync(User, model.CurrentPassword, model.Password, HttpContext.RequestAborted))
            {
                ModelState.AddModelError(string.Empty, "No fue posible cambiar la contraseña. Comprueba tus datos y el estado de tu cuenta.");
                return View(model);
            }
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["Success"] = "Contraseña actualizada. Inicia sesión de nuevo.";
            return RedirectToAction("Login", "Auth");
        }
        catch (Exception e) when (InfrastructureFailure(e)) { return Unavailable(model); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RevocarSesiones()
    {
        if (!int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out int id)) return Forbid();
        try
        {
            await security.RevokeAllAsync(id, HttpContext.RequestAborted);
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["Success"] = "Las sesiones fueron revocadas. Inicia sesión de nuevo.";
            return RedirectToAction("Login", "Auth");
        }
        catch (Exception e) when (InfrastructureFailure(e)) { return StatusCode(503, "No fue posible revocar las sesiones. Inténtalo más tarde."); }
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Recuperar() => View(new RequestRecoveryViewModel());

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken, EnableRateLimiting("AccountRecovery")]
    public async Task<IActionResult> Recuperar(RequestRecoveryViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        try { await recovery.RequestAsync(model.Email.Trim(), HttpContext.RequestAborted); }
        catch (Exception e) when (InfrastructureFailure(e) || e is IOException or System.Security.Cryptography.CryptographicException)
        { logger.LogWarning("No fue posible completar una solicitud de recuperación."); }
        // Same status/body for unknown, inactive, delivery failure and eligible accounts.
        ViewBag.Message = "Si la cuenta puede recuperarse, recibirás instrucciones. El código caduca en 30 minutos.";
        return View(new RequestRecoveryViewModel());
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Restablecer() => View(new ResetPasswordViewModel());

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken, EnableRateLimiting("AccountRecovery")]
    public async Task<IActionResult> Restablecer(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        try
        {
            if (!await recovery.ResetAsync(model.Token, model.Password, HttpContext.RequestAborted))
            {
                ModelState.AddModelError(string.Empty, "El código no es válido, caducó o ya fue utilizado. Solicita uno nuevo.");
                return View(model);
            }
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["Success"] = "Contraseña restablecida. Inicia sesión de nuevo.";
            return RedirectToAction("Login", "Auth");
        }
        catch (Exception e) when (InfrastructureFailure(e)) { return Unavailable(model); }
    }

    private IActionResult Unavailable(object model)
    {
        logger.LogWarning("El servicio de seguridad de cuentas no está disponible.");
        Response.StatusCode = 503;
        ModelState.AddModelError(string.Empty, "No fue posible completar la operación. Inténtalo más tarde.");
        return View(model);
    }
    private static bool InfrastructureFailure(Exception e) => e is DbException or DbUpdateException or InvalidOperationException;
}
