using Microsoft.AspNetCore.Mvc.Filters;

namespace Proyecto_HobbyHub.Security;

// Request outcomes complement durable domain audits; a redirect is not proof of a mutation.
public sealed class WriteRequestAuditFilter(RequestSecurityLog log, AuditEventBudget budget) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!HttpMethods.IsPost(context.HttpContext.Request.Method)) { await next(); return; }
        if (!budget.Allow()) { await next(); return; }
        string action = context.RouteData.Values["controller"] + "." + context.RouteData.Values["action"];
        log.Write(context.HttpContext, action, "Attempt");
        try
        {
            var result = await next();
            log.Write(context.HttpContext, action, result.Exception is null ? "ActionReturned" : "Failed");
        }
        catch
        {
            log.Write(context.HttpContext, action, "Failed");
            throw;
        }
    }
}
