using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Proyecto_HobbyHub.Security;

public sealed class TechnicalRequestMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ILogger<TechnicalRequestMiddleware> logger, AuditEventBudget budget)
    {
        // Ignore caller-provided correlation headers.
        context.TraceIdentifier = Guid.NewGuid().ToString("N");
        context.Response.OnStarting(() => { context.Response.Headers["X-Correlation-ID"] = context.TraceIdentifier; return Task.CompletedTask; });
        var started = Stopwatch.GetTimestamp();
        using var scope = logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = context.TraceIdentifier });
        try { await next(context); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (budget.Allow()) logger.LogError("ServerFailure Type={Type} CorrelationId={CorrelationId}", error.GetType().Name, context.TraceIdentifier);
            if (context.Response.HasStarted) throw;
            context.Response.Clear();
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("No fue posible completar la solicitud. Inténtalo de nuevo. Referencia: " + context.TraceIdentifier);
        }
        finally
        {
            var action = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
            if (action is not null && budget.Allow())
                logger.Log(context.Response.StatusCode >= 500 ? LogLevel.Error : context.Response.StatusCode >= 400 ? LogLevel.Warning : LogLevel.Information,
                    "RequestCompleted Action={Controller}.{Action} Result={Result} StatusCode={StatusCode} DurationMs={DurationMs} CorrelationId={CorrelationId}",
                    action.ControllerName, action.ActionName, context.Items["FunctionalResult"] ?? "Unconfirmed", context.Response.StatusCode, Stopwatch.GetElapsedTime(started).TotalMilliseconds, context.TraceIdentifier);
        }
    }
}
