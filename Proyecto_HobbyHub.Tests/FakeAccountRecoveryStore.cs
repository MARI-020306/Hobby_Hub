using Proyecto_HobbyHub.Models;
using Proyecto_HobbyHub.Security;

namespace Proyecto_HobbyHub.Tests;

public sealed class MemorySecurityAudit : ISecurityAudit
{
    public List<AuditoriaSeguridad> Entries { get; } = new();
    public Task WriteAsync(string action, int? actor, int? target, bool success, CancellationToken cancellationToken = default)
    {
        Entries.Add(new() { Accion = action, IdActor = actor, IdObjetivo = target, Exito = success, FechaUtc = DateTime.UtcNow });
        return Task.CompletedTask;
    }
}
public sealed class MemoryRecoveryMailbox : IRecoveryMailSender
{
    public List<RecoveryMail> Messages { get; } = new();
    public bool FailDelivery { get; set; }
    public Task SendAsync(RecoveryMail mail, CancellationToken cancellationToken)
    {
        if (FailDelivery) throw new IOException("Synthetic delivery failure");
        Messages.Add(mail);
        return Task.CompletedTask;
    }
}
public sealed class FakeAccountRecoveryStore(FakeAccountSecurityStore accounts, MemorySecurityAudit audit) : IAccountRecoveryStore
{
    private readonly object _gate = new();
    public List<RecuperacionCuenta> Tokens { get; } = new();
    public Task<bool> IssueAsync(RecuperacionCuenta token, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var a = accounts.Accounts[token.IdUsuario];
            if (a.State != "Activo" || a.SecurityVersion != token.VersionSeguridad) return Task.FromResult(false);
            foreach (var t in Tokens.Where(t => t.IdUsuario == token.IdUsuario && t.ConsumidaUtc is null)) t.ConsumidaUtc = token.CreadaUtc;
            Tokens.Add(token);
            audit.WriteAsync("RecoveryIssued", null, a.Id, true).GetAwaiter().GetResult();
            return Task.FromResult(true);
        }
    }
    public Task<bool> ResetAsync(string hash, string passwordHash, DateTime now, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var t = Tokens.SingleOrDefault(t => t.TokenHash == hash);
            if (t is null || t.ConsumidaUtc is not null || t.ExpiraUtc <= now || t.CreadaUtc > now ||
                !accounts.Accounts.TryGetValue(t.IdUsuario, out var a) || a.State != "Activo" || a.SecurityVersion != t.VersionSeguridad)
                return Task.FromResult(false);
            Replace(a.Id, passwordHash, now);
            audit.WriteAsync("PasswordReset", null, a.Id, true).GetAwaiter().GetResult();
            return Task.FromResult(true);
        }
    }
    public Task<bool> ChangeAsync(AccountSecuritySnapshot expected, string passwordHash, DateTime now, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var a = accounts.Accounts[expected.Id];
            if (a.State != "Activo" || a.SecurityVersion != expected.SecurityVersion || a.PasswordHash != expected.PasswordHash ||
                a.RoleId != expected.RoleId || a.LockoutUntilUtc > now) return Task.FromResult(false);
            Replace(a.Id, passwordHash, now);
            audit.WriteAsync("PasswordChanged", a.Id, a.Id, true).GetAwaiter().GetResult();
            return Task.FromResult(true);
        }
    }
    private void Replace(int id, string hash, DateTime now)
    {
        accounts.Accounts[id] = accounts.Accounts[id] with { PasswordHash = hash, SecurityVersion = Guid.NewGuid(), LockoutUntilUtc = null };
        accounts.Failures[id] = 0;
        foreach (var s in accounts.Sessions.Values.Where(s => s.IdUsuario == id)) s.RevocadaUtc = now;
        foreach (var t in Tokens.Where(t => t.IdUsuario == id && t.ConsumidaUtc is null)) t.ConsumidaUtc = now;
        audit.WriteAsync("SessionsRevoked", id, id, true).GetAwaiter().GetResult();
    }
}
