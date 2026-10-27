using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Controllers;
using Proyecto_HobbyHub.Models;

namespace Proyecto_HobbyHub.Security;

// Bounded rejection sampling is shared by the entire process, never keyed by attacker input.
public sealed class RequestSecurityLog(ILogger<RequestSecurityLog> logger, TimeProvider time)
{
    private readonly AuditEventBudget events = new(time);
    private readonly object gate = new();
    private DateTimeOffset window;
    private int count;
    private long suppressed;

    public void Persisted(HttpContext context, AuditoriaSeguridad audit, bool pendingTransaction)
    {
        if (!events.Allow()) return;
        logger.LogInformation("SecurityAudit TimestampUtc={TimestampUtc} Action={Action} Result={Result} Actor={Actor} TargetUser={TargetUser} AuditId={AuditId} CorrelationId={CorrelationId}",
            new DateTimeOffset(DateTime.SpecifyKind(audit.FechaUtc, DateTimeKind.Utc)), audit.Accion,
            pendingTransaction ? "SavedPendingTransaction" : audit.Exito ? "Committed" : "AttemptOrDenied",
            audit.IdActor, audit.IdObjetivo, audit.IdAuditoria, context.TraceIdentifier);
    }

    public void Rejected(HttpContext context, string action)
    {
        lock (gate)
        {
            var now = time.GetUtcNow();
            if (now - window >= TimeSpan.FromMinutes(1))
            {
                if (suppressed > 0)
                    logger.LogWarning("SecurityRejectionsSuppressed TimestampUtc={TimestampUtc} Count={Count}", now, suppressed);
                window = now; count = 0; suppressed = 0;
            }
            if (++count > 10) { suppressed++; return; }
            Write(context, action, "Denied");
        }
    }

    public void Write(HttpContext context, string action, string result)
    {
        if (!events.Allow()) return;
        var descriptor = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
        int? actor = int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out int id) ? id : null;
        // Only typed numeric route IDs, never recovery tokens, query strings or arbitrary URLs.
        string? resource = context.Request.RouteValues.Where(v => v.Key.StartsWith("id", StringComparison.OrdinalIgnoreCase))
            .Select(v => long.TryParse(v.Value?.ToString(), out long number) ? number.ToString(System.Globalization.CultureInfo.InvariantCulture) : null)
            .FirstOrDefault(v => v is not null);
        logger.LogInformation("SecurityEvent TimestampUtc={TimestampUtc} Action={Action} Result={Result} Actor={Actor} ResourceType={ResourceType} ResourceId={ResourceId} CorrelationId={CorrelationId}",
            time.GetUtcNow(), action, result, actor, descriptor?.ControllerName, resource, context.TraceIdentifier);
    }
}
