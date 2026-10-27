using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace Proyecto_HobbyHub.Controllers;

public abstract class AuthenticatedController : Controller
{
    protected async Task<IActionResult> ExpireAuthenticationAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login", "Auth");
    }
}
