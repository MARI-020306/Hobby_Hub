using Proyecto_HobbyHub.Models;

namespace Proyecto_HobbyHub.Security;

public interface IAccountRecoveryStore
{
    Task<bool> IssueAsync(RecuperacionCuenta token, CancellationToken cancellationToken);
    Task<bool> ResetAsync(string tokenHash, string passwordHash, DateTime now, CancellationToken cancellationToken);
    Task<bool> ChangeAsync(AccountSecuritySnapshot expected, string passwordHash, DateTime now, CancellationToken cancellationToken);
}

public interface ISecurityAudit
{
    Task WriteAsync(string action, int? actor, int? target, bool success, CancellationToken cancellationToken = default);
}

public sealed class EfSecurityAudit(HobbyHubContext context, TimeProvider time, AuditEventBudget budget) : ISecurityAudit
{
    public async Task WriteAsync(string action, int? actor, int? target, bool success, CancellationToken cancellationToken = default)
    {
        if (!success && !budget.Allow()) return;
        context.AuditoriasSeguridad.Add(new() { Accion = action, IdActor = actor, IdObjetivo = target,
            Exito = success, FechaUtc = time.GetUtcNow().UtcDateTime });
        await context.SaveChangesAsync(cancellationToken);
    }
}
