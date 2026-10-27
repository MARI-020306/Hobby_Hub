using Microsoft.EntityFrameworkCore;
using Proyecto_HobbyHub.Models;

namespace Proyecto_HobbyHub.Security;

public sealed class EfAccountSecurityStore(HobbyHubContext context) : IAccountSecurityStore
{
    private void Audit(string action, int userId, DateTime now) => context.AuditoriasSeguridad.Add(new()
        { Accion = action, IdActor = userId, IdObjetivo = userId, Exito = true, FechaUtc = now });
    public static AccountSecuritySnapshot Snapshot(Usuario user) => new(user.IdUsuario, user.Nombre,
        user.Password, user.RolId, user.Rol.Nombre, user.Estado, user.VersionSeguridad, user.BloqueoHastaUtc);

    public async Task<AccountSecuritySnapshot?> FindByEmailHashAsync(string emailHash, CancellationToken cancellationToken)
    {
        await EndSuspension(context.Usuarios.Where(u=>u.Correo==emailHash),cancellationToken);
        var user = await context.Usuarios.AsNoTracking().Include(u => u.Rol)
            .SingleOrDefaultAsync(u => u.Correo == emailHash, cancellationToken);
        return user is null ? null : Snapshot(user);
    }

    public async Task<AccountSecuritySnapshot?> FindByIdAsync(int id, CancellationToken cancellationToken)
    {
        await EndSuspension(context.Usuarios.Where(u=>u.IdUsuario==id),cancellationToken);
        var user = await context.Usuarios.AsNoTracking().Include(u => u.Rol)
            .SingleOrDefaultAsync(u => u.IdUsuario == id, cancellationToken);
        return user is null ? null : Snapshot(user);
    }

    public Task RecordFailureAsync(int id, DateTime nowUtc, int threshold, TimeSpan duration, CancellationToken cancellationToken)
    {
        Guid version = Guid.NewGuid();
        return context.Usuarios.Where(u => u.IdUsuario == id && u.Estado == "Activo" &&
                (u.BloqueoHastaUtc == null || u.BloqueoHastaUtc <= nowUtc))
            .ExecuteUpdateAsync(update => update
                .SetProperty(u => u.IntentosFallidos, u => u.BloqueoHastaUtc != null ? 1 : u.IntentosFallidos + 1)
                .SetProperty(u => u.BloqueoHastaUtc, u => (u.BloqueoHastaUtc != null ? 1 : u.IntentosFallidos + 1) >= threshold
                    ? nowUtc.Add(duration) : (DateTime?)null)
                .SetProperty(u => u.VersionSeguridad, u => (u.BloqueoHastaUtc != null ? 1 : u.IntentosFallidos + 1) >= threshold
                    ? version : u.VersionSeguridad), cancellationToken);
    }

    public async Task<bool> CreateSessionAsync(SesionUsuario session, AccountSecuritySnapshot expected, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        // Atomic eligibility check/reset. Its write lock is retained until the session commits.
        int eligible = await context.Usuarios.Where(u => u.IdUsuario == expected.Id && u.Estado == "Activo" &&
                u.VersionSeguridad == expected.SecurityVersion && u.Password == expected.PasswordHash &&
                u.RolId == expected.RoleId && u.Rol.Nombre == expected.RoleName &&
                (u.BloqueoHastaUtc == null || u.BloqueoHastaUtc <= session.FechaCreacionUtc))
            .ExecuteUpdateAsync(update => update.SetProperty(u => u.IntentosFallidos, 0)
                .SetProperty(u => u.BloqueoHastaUtc, (DateTime?)null), cancellationToken);
        if (eligible == 0) return false;
        context.SesionesUsuario.Add(session);
        Audit("Login", expected.Id, session.FechaCreacionUtc);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<SessionSecuritySnapshot?> FindSessionAsync(Guid id, CancellationToken cancellationToken)
    {
        await EndSuspension(context.Usuarios.Where(u=>context.SesionesUsuario.Any(s=>s.IdSesion==id&&s.IdUsuario==u.IdUsuario)),cancellationToken);
        var session = await context.SesionesUsuario.AsNoTracking().Include(s => s.Usuario).ThenInclude(u => u.Rol)
            .SingleOrDefaultAsync(s => s.IdSesion == id, cancellationToken);
        return session is null ? null : new(session, Snapshot(session.Usuario));
    }

    public async Task RevokeSessionAsync(Guid id, DateTime nowUtc, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var session = await context.SesionesUsuario.AsNoTracking().SingleOrDefaultAsync(s => s.IdSesion == id, cancellationToken);
        if (session is null) return;
        int revoked = await context.SesionesUsuario.Where(s => s.IdSesion == id && s.RevocadaUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(s => s.RevocadaUtc, nowUtc), cancellationToken);
        if (revoked > 0) Audit("Logout", session.IdUsuario, nowUtc);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RevokeAllAsync(int userId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        Guid version = Guid.NewGuid();
        await context.Usuarios.Where(u => u.IdUsuario == userId).ExecuteUpdateAsync(
            update => update.SetProperty(u => u.VersionSeguridad, version), cancellationToken);
        await context.SesionesUsuario.Where(s => s.IdUsuario == userId && s.RevocadaUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(s => s.RevocadaUtc, nowUtc), cancellationToken);
        Audit("SessionsRevoked", userId, nowUtc);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
    private async Task EndSuspension(IQueryable<Usuario> candidates,CancellationToken cancellationToken)
    {
        DateTime now=DateTime.UtcNow;
        var expired=await candidates.AsNoTracking().Where(u=>u.Estado=="Suspendido"&&u.SuspensionHastaUtc!=null&&u.SuspensionHastaUtc<=now).Select(u=>u.IdUsuario).ToListAsync(cancellationToken);
        foreach(int id in expired) {
            await using var tx=await context.Database.BeginTransactionAsync(cancellationToken);Guid version=Guid.NewGuid();
            int changed=await context.Usuarios.Where(u=>u.IdUsuario==id&&u.Estado=="Suspendido"&&u.SuspensionHastaUtc<=now)
                .ExecuteUpdateAsync(s=>s.SetProperty(u=>u.Estado,"Activo").SetProperty(u=>u.SuspensionHastaUtc,(DateTime?)null).SetProperty(u=>u.VersionSeguridad,version),cancellationToken);
            if(changed>0){context.AuditoriasSeguridad.Add(new(){Accion="SuspensionExpired",IdObjetivo=id,Exito=true,FechaUtc=now});await context.SaveChangesAsync(cancellationToken);}
            await tx.CommitAsync(cancellationToken);
        }
    }
}
