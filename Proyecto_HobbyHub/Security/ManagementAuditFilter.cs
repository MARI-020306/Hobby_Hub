using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Proyecto_HobbyHub.Security;

// Management routes must persist their domain changes/audit together inside services.
// This additional filter records that an authorized management endpoint was invoked.
public sealed class ManagementAuditFilter(ISecurityAudit audit, AuditEventBudget budget) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        string? controller = context.RouteData.Values["controller"]?.ToString();
        if (controller is not ("Administracion" or "Moderacion")) { await next(); return; }
        if (!budget.Allow()) { await next(); return; }
        int? actor = int.TryParse(context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out int id) ? id : null;
        // This event is an attempt, never evidence that the domain operation completed.
        await audit.WriteAsync(controller == "Administracion" ? "AdministrationAttempt" : "ModerationAttempt", actor, null, false,
            context.HttpContext.RequestAborted);
        var executed = await next();
        bool success = executed.Exception is null && executed.Result is not (BadRequestResult or BadRequestObjectResult or ForbidResult or NotFoundResult or NotFoundObjectResult) &&
            (executed.Result is not Microsoft.AspNetCore.Mvc.Infrastructure.IStatusCodeActionResult status || status.StatusCode is null or < 400);
        if (success && HttpMethods.IsGet(context.HttpContext.Request.Method))
            await audit.WriteAsync(controller == "Administracion" ? "AdministrationAccess" : "ModerationAccess", actor, null, true,
                context.HttpContext.RequestAborted);
        context.HttpContext.RequestServices.GetRequiredService<RequestSecurityLog>().Write(context.HttpContext,
            controller + "." + context.ActionDescriptor.RouteValues["action"], success ? "Returned" : "Failed");
    }
}
