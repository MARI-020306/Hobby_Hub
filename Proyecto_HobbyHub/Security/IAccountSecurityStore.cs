using Proyecto_HobbyHub.Models;

namespace Proyecto_HobbyHub.Security;

public sealed record AccountSecuritySnapshot(int Id, string Name, string PasswordHash, int RoleId,
    string RoleName, string State, Guid SecurityVersion, DateTime? LockoutUntilUtc);
public sealed record SessionSecuritySnapshot(SesionUsuario Session, AccountSecuritySnapshot Account);

public interface IAccountSecurityStore
{
    Task<AccountSecuritySnapshot?> FindByEmailHashAsync(string emailHash, CancellationToken cancellationToken);
    Task<AccountSecuritySnapshot?> FindByIdAsync(int id, CancellationToken cancellationToken);
    Task RecordFailureAsync(int id, DateTime nowUtc, int threshold, TimeSpan duration, CancellationToken cancellationToken);
    Task<bool> CreateSessionAsync(SesionUsuario session, AccountSecuritySnapshot expected, CancellationToken cancellationToken);
    Task<SessionSecuritySnapshot?> FindSessionAsync(Guid id, CancellationToken cancellationToken);
    Task RevokeSessionAsync(Guid id, DateTime nowUtc, CancellationToken cancellationToken);
    Task RevokeAllAsync(int userId, DateTime nowUtc, CancellationToken cancellationToken);
}
