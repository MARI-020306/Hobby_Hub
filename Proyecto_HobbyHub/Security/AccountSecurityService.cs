using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Proyecto_HobbyHub.Models;

namespace Proyecto_HobbyHub.Security;

public sealed record LoginSession(ClaimsPrincipal Principal, DateTimeOffset IssuedUtc, DateTimeOffset ExpiresUtc);

public sealed class AccountSecurityService(IAccountSecurityStore store, TimeProvider time, ISecurityAudit audit)
{
    public const string SessionClaim = "hobbyhub:session";
    public const string VersionClaim = "hobbyhub:security-version";
    public const string CredentialClaim = "hobbyhub:credential-version";
    public const string RoleIdClaim = "hobbyhub:role-id";
    public const int FailureThreshold = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);
    // Timing work for unknown accounts; this is not a credential for any user.
    private static readonly string DummyHash = SecurityHelper.HashPassword(Guid.NewGuid().ToString("N"));

    public async Task<LoginSession?> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var account = await store.FindByEmailHashAsync(SecurityHelper.HashEmail(email), cancellationToken);
        bool passwordValid = SecurityHelper.VerifyPassword(password, account?.PasswordHash ?? DummyHash);
        DateTimeOffset now = time.GetUtcNow();
        if (account is null || account.State != "Activo" || !SecurityPolicies.KnownRoles.Contains(account.RoleName) ||
            account.SecurityVersion == Guid.Empty || account.LockoutUntilUtc > now.UtcDateTime)
        {
            await audit.WriteAsync("LoginDenied", null, account?.Id, false, cancellationToken);
            return null;
        }
        if (!passwordValid)
        {
            await store.RecordFailureAsync(account.Id, now.UtcDateTime, FailureThreshold, LockoutDuration, cancellationToken);
            await audit.WriteAsync("LoginDenied", null, account.Id, false, cancellationToken);
            return null;
        }
        var session = new SesionUsuario
        {
            IdSesion = Guid.NewGuid(), IdUsuario = account.Id, VersionSeguridad = account.SecurityVersion,
            FechaCreacionUtc = now.UtcDateTime, ExpiraUtc = now.Add(SessionLifetime).UtcDateTime
        };
        if (!await store.CreateSessionAsync(session, account, cancellationToken)) return null;
        return new(CreatePrincipal(account, session.IdSesion), now, now.Add(SessionLifetime));
    }

    public static ClaimsPrincipal CreatePrincipal(AccountSecuritySnapshot account, Guid sessionId) => new(new ClaimsIdentity(
        new[]
        {
            new Claim(ClaimTypes.NameIdentifier, account.Id.ToString(CultureInfo.InvariantCulture)),
            new Claim(ClaimTypes.Name, account.Name), new Claim(ClaimTypes.Role, account.RoleName),
            new Claim(RoleIdClaim, account.RoleId.ToString(CultureInfo.InvariantCulture)),
            new Claim(SessionClaim, sessionId.ToString("D")), new Claim(VersionClaim, account.SecurityVersion.ToString("D")),
            new Claim(CredentialClaim, SecurityHelper.HashPersonalData(account.PasswordHash))
        }, CookieAuthenticationDefaults.AuthenticationScheme));

    public async Task<bool> ValidateAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out int userId) ||
            !Guid.TryParse(principal.FindFirstValue(SessionClaim), out Guid sessionId) ||
            !Guid.TryParse(principal.FindFirstValue(VersionClaim), out Guid version)) return false;
        var current = await store.FindSessionAsync(sessionId, cancellationToken);
        if (current is null) return false;
        var (session, account) = current;
        DateTime now = time.GetUtcNow().UtcDateTime;
        return session.IdUsuario == userId && account.Id == userId && session.RevocadaUtc is null &&
            session.ExpiraUtc > now && session.FechaCreacionUtc <= now && account.State == "Activo" &&
            !(account.LockoutUntilUtc > now) && version != Guid.Empty && version == account.SecurityVersion &&
            version == session.VersionSeguridad && SecurityPolicies.KnownRoles.Contains(account.RoleName) &&
            principal.FindAll(ClaimTypes.Role).Select(c => c.Value).SequenceEqual(new[] { account.RoleName }) &&
            principal.FindFirstValue(RoleIdClaim) == account.RoleId.ToString(CultureInfo.InvariantCulture) &&
            principal.FindFirstValue(CredentialClaim) == SecurityHelper.HashPersonalData(account.PasswordHash);
    }

    public async Task LogoutAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(principal.FindFirstValue(SessionClaim), out Guid id)) return;
        await store.RevokeSessionAsync(id, time.GetUtcNow().UtcDateTime, cancellationToken);
    }

    public async Task RevokeAllAsync(int userId, CancellationToken cancellationToken = default)
    {
        await store.RevokeAllAsync(userId, time.GetUtcNow().UtcDateTime, cancellationToken);
    }
}
