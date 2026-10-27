using System.Data.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace Proyecto_HobbyHub.Security;

public sealed class RevalidatingCookieEvents(AccountSecurityService security,
    ILogger<RevalidatingCookieEvents> logger) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        bool valid;
        try
        {
            valid = context.Principal is not null && await security.ValidateAsync(context.Principal, context.HttpContext.RequestAborted);
        }
        catch (Exception error) when (error is DbException or DbUpdateException or InvalidOperationException)
        {
            // Fail closed without publishing SQL/connection details or credential material.
            logger.LogWarning("No fue posible revalidar la sesión; se rechaza la autenticación.");
            valid = false;
        }
        if (!valid)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}
