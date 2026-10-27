using Proyecto_HobbyHub.Models;
using Proyecto_HobbyHub.Security;

namespace Proyecto_HobbyHub.Tests;

public sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan duration) => _now += duration;
}

// Test-only persistence double. Real SQL is physically blocked by the factory.
public sealed class FakeAccountSecurityStore : IAccountSecurityStore
{
    public const string Password = "synthetic-test-password";
    private static readonly string PasswordHash = SecurityHelper.HashPassword(Password);
    public Dictionary<int, AccountSecuritySnapshot> Accounts { get; } = new();
    public Dictionary<Guid, SesionUsuario> Sessions { get; } = new();
    public Dictionary<int, int> Failures { get; } = new();
    public bool Unavailable { get; set; }
    public MemorySecurityAudit Audit { get; set; } = new();
    public Action? BeforeCreate { get; set; }

    public FakeAccountSecurityStore()
    {
        for (int id = 1; id <= 4; id++)
            Accounts[id] = new(id, "Synthetic User", PasswordHash, id,
                SecurityPolicies.KnownRoles[id - 1], "Activo", Guid.NewGuid(), null);
    }
    public static string Email(int id) => $"person{id}@example.test";
    public Task<AccountSecuritySnapshot?> FindByEmailHashAsync(string hash, CancellationToken cancellationToken)
    {
        if (Unavailable) throw new InvalidOperationException("Synthetic store unavailable");
        return Task.FromResult(Accounts.Values.SingleOrDefault(a => SecurityHelper.HashEmail(Email(a.Id)) == hash));
    }
    public Task<AccountSecuritySnapshot?> FindByIdAsync(int id, CancellationToken cancellationToken) =>
        Task.FromResult(Accounts.GetValueOrDefault(id));
    public Task RecordFailureAsync(int id, DateTime now, int threshold, TimeSpan duration, CancellationToken cancellationToken)
    {
        var a = Accounts[id];
        if (a.State != "Activo" || a.LockoutUntilUtc > now) return Task.CompletedTask;
        int count = a.LockoutUntilUtc is null ? Failures.GetValueOrDefault(id) + 1 : 1;
        Failures[id] = count;
        Accounts[id] = a with { LockoutUntilUtc = count >= threshold ? now.Add(duration) : null,
            SecurityVersion = count >= threshold ? Guid.NewGuid() : a.SecurityVersion };
        return Task.CompletedTask;
    }
    public Task<bool> CreateSessionAsync(SesionUsuario session, AccountSecuritySnapshot expected, CancellationToken cancellationToken)
    {
        BeforeCreate?.Invoke();
        var a = Accounts[expected.Id];
        if (a.State != "Activo" || a.SecurityVersion != expected.SecurityVersion || a.PasswordHash != expected.PasswordHash ||
            a.RoleId != expected.RoleId || a.RoleName != expected.RoleName || a.LockoutUntilUtc > session.FechaCreacionUtc)
            return Task.FromResult(false);
        Failures[a.Id] = 0;
        Accounts[a.Id] = a with { LockoutUntilUtc = null };
        Sessions.Add(session.IdSesion, session);
        Audit.WriteAsync("Login", a.Id, a.Id, true).GetAwaiter().GetResult();
        return Task.FromResult(true);
    }
    public Task<SessionSecuritySnapshot?> FindSessionAsync(Guid id, CancellationToken cancellationToken)
    {
        if (Unavailable) throw new InvalidOperationException("Synthetic store unavailable");
        return Task.FromResult(Sessions.TryGetValue(id, out var s) && Accounts.TryGetValue(s.IdUsuario, out var a)
            ? new SessionSecuritySnapshot(s, a) : null);
    }
    public Task RevokeSessionAsync(Guid id, DateTime now, CancellationToken cancellationToken)
    {
        if (Sessions.TryGetValue(id, out var s) && s.RevocadaUtc is null)
        {
            s.RevocadaUtc = now;
            Audit.WriteAsync("Logout", s.IdUsuario, s.IdUsuario, true).GetAwaiter().GetResult();
        }
        return Task.CompletedTask;
    }
    public Task RevokeAllAsync(int userId, DateTime now, CancellationToken cancellationToken)
    {
        Accounts[userId] = Accounts[userId] with { SecurityVersion = Guid.NewGuid() };
        foreach (var s in Sessions.Values.Where(s => s.IdUsuario == userId)) s.RevocadaUtc = now;
        Audit.WriteAsync("SessionsRevoked", userId, userId, true).GetAwaiter().GetResult();
        return Task.CompletedTask;
    }
    public System.Security.Claims.ClaimsPrincipal IssuePrincipal(int userId, DateTime now)
    {
        var a = Accounts[userId];
        var s = new SesionUsuario { IdSesion = Guid.NewGuid(), IdUsuario = userId, VersionSeguridad = a.SecurityVersion,
            FechaCreacionUtc = now, ExpiraUtc = now.Add(AccountSecurityService.SessionLifetime) };
        Sessions.Add(s.IdSesion, s);
        return AccountSecurityService.CreatePrincipal(a, s.IdSesion);
    }
}
