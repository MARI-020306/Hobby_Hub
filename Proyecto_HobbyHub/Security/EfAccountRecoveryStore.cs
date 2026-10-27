using System.Data;
using Microsoft.EntityFrameworkCore;
using Proyecto_HobbyHub.Models;

namespace Proyecto_HobbyHub.Security;

public sealed class EfAccountRecoveryStore(HobbyHubContext context) : IAccountRecoveryStore
{
    private void Audit(string action, int? actor, int user, DateTime now) =>
        context.AuditoriasSeguridad.Add(new() { Accion = action, IdActor = actor, IdObjetivo = user, Exito = true, FechaUtc = now });

    public async Task<bool> IssueAsync(RecuperacionCuenta token, CancellationToken cancellationToken)
    {
        await using var tx = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        // Updating the account acquires the same lock/order used by all credential operations.
        int eligible = await context.Usuarios.Where(u => u.IdUsuario == token.IdUsuario && u.Estado == "Activo" &&
                u.VersionSeguridad == token.VersionSeguridad)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.IntentosFallidos, u => u.IntentosFallidos), cancellationToken);
        if (eligible != 1) return false;
        await context.RecuperacionesCuenta.Where(t => t.IdUsuario == token.IdUsuario && t.ConsumidaUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.ConsumidaUtc, token.CreadaUtc), cancellationToken);
        context.RecuperacionesCuenta.Add(token);
        Audit("RecoveryIssued", null, token.IdUsuario, token.CreadaUtc);
        await context.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ResetAsync(string tokenHash, string passwordHash, DateTime now, CancellationToken cancellationToken)
    {
        // Read to discover account; eligibility is rechecked inside the transaction.
        var token = await context.RecuperacionesCuenta.AsNoTracking().SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
        if (token is null) return false;
        await using var tx = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        int eligible = await context.Usuarios.Where(u => u.IdUsuario == token.IdUsuario && u.Estado == "Activo" &&
                u.VersionSeguridad == token.VersionSeguridad)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.IntentosFallidos, u => u.IntentosFallidos), cancellationToken);
        if (eligible != 1) return false;
        int consumed = await context.RecuperacionesCuenta.Where(t => t.IdRecuperacion == token.IdRecuperacion &&
                t.TokenHash == tokenHash && t.ConsumidaUtc == null && t.CreadaUtc <= now && t.ExpiraUtc > now)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.ConsumidaUtc, now), cancellationToken);
        if (consumed != 1) return false;
        await ReplaceCredentialAsync(token.IdUsuario, passwordHash, now, cancellationToken);
        Audit("PasswordReset", null, token.IdUsuario, now);
        await context.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ChangeAsync(AccountSecuritySnapshot expected, string passwordHash, DateTime now, CancellationToken cancellationToken)
    {
        await using var tx = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        int eligible = await context.Usuarios.Where(u => u.IdUsuario == expected.Id && u.Estado == "Activo" &&
                u.VersionSeguridad == expected.SecurityVersion && u.Password == expected.PasswordHash && u.RolId == expected.RoleId &&
                (u.BloqueoHastaUtc == null || u.BloqueoHastaUtc <= now))
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.IntentosFallidos, u => u.IntentosFallidos), cancellationToken);
        if (eligible != 1) return false;
        await ReplaceCredentialAsync(expected.Id, passwordHash, now, cancellationToken);
        Audit("PasswordChanged", expected.Id, expected.Id, now);
        await context.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return true;
    }

    private async Task ReplaceCredentialAsync(int id, string passwordHash, DateTime now, CancellationToken cancellationToken)
    {
        Guid version = Guid.NewGuid();
        await context.Usuarios.Where(u => u.IdUsuario == id).ExecuteUpdateAsync(s => s
            .SetProperty(u => u.Password, passwordHash).SetProperty(u => u.VersionSeguridad, version)
            .SetProperty(u => u.IntentosFallidos, 0).SetProperty(u => u.BloqueoHastaUtc, (DateTime?)null), cancellationToken);
        await context.SesionesUsuario.Where(s => s.IdUsuario == id && s.RevocadaUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevocadaUtc, now), cancellationToken);
        await context.RecuperacionesCuenta.Where(t => t.IdUsuario == id && t.ConsumidaUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.ConsumidaUtc, now), cancellationToken);
        Audit("SessionsRevoked", id, id, now);
    }
}
