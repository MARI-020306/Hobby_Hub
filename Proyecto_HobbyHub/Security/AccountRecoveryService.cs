using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using Proyecto_HobbyHub.Models;

namespace Proyecto_HobbyHub.Security;

public sealed class AccountRecoveryService(IAccountSecurityStore accounts, IAccountRecoveryStore recovery,
    IRecoveryMailSender mail, ISecurityAudit audit, TimeProvider time, AccountSecurityService security)
{
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(30);
    public async Task RequestAsync(string email, CancellationToken cancellationToken = default)
    {
        var account = await accounts.FindByEmailHashAsync(SecurityHelper.HashEmail(email), cancellationToken);
        if (account is null || account.State != "Activo" || account.SecurityVersion == Guid.Empty || !SecurityPolicies.KnownRoles.Contains(account.RoleName))
        {
            await audit.WriteAsync("RecoveryRequested", null, null, false, cancellationToken);
            return;
        }
        string raw = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        DateTime now = time.GetUtcNow().UtcDateTime;
        var token = new RecuperacionCuenta { IdRecuperacion = Guid.NewGuid(), IdUsuario = account.Id,
            TokenHash = SecurityHelper.HashPersonalData(raw), VersionSeguridad = account.SecurityVersion,
            CreadaUtc = now, ExpiraUtc = now.Add(TokenLifetime) };
        if (await recovery.IssueAsync(token, cancellationToken))
            await mail.SendAsync(new(email.Trim().ToLowerInvariant(), raw, token.ExpiraUtc), cancellationToken);
    }
    public async Task<bool> ResetAsync(string token, string password, CancellationToken cancellationToken = default)
    {
        if (!ValidToken(token) || !ValidPassword(password)) { await audit.WriteAsync("PasswordResetDenied", null, null, false, cancellationToken); return false; }
        bool success = await recovery.ResetAsync(SecurityHelper.HashPersonalData(token), SecurityHelper.HashPassword(password),
            time.GetUtcNow().UtcDateTime, cancellationToken);
        if (!success) await audit.WriteAsync("PasswordResetDenied", null, null, false, cancellationToken);
        return success;
    }
    public async Task<bool> ChangeAsync(ClaimsPrincipal principal, string current, string password, CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out int id) || !ValidPassword(password)) return false;
        if (principal.Identity?.IsAuthenticated != true || !await security.ValidateAsync(principal, cancellationToken))
        {
            await audit.WriteAsync("PasswordChangeDenied", null, id, false, cancellationToken);
            return false;
        }
        var account = await accounts.FindByIdAsync(id, cancellationToken);
        if (account is null || account.State != "Activo" || account.LockoutUntilUtc > time.GetUtcNow().UtcDateTime ||
            !SecurityHelper.VerifyPassword(current, account.PasswordHash))
        {
            if (account is { State: "Activo" }) await accounts.RecordFailureAsync(id, time.GetUtcNow().UtcDateTime,
                AccountSecurityService.FailureThreshold, AccountSecurityService.LockoutDuration, cancellationToken);
            await audit.WriteAsync("PasswordChangeDenied", id, id, false, cancellationToken);
            return false;
        }
        return await recovery.ChangeAsync(account, SecurityHelper.HashPassword(password), time.GetUtcNow().UtcDateTime, cancellationToken);
    }
    private static bool ValidPassword(string value) => !string.IsNullOrWhiteSpace(value) && value.Length >= 8 &&
        new BcryptPasswordAttribute().IsValid(value);
    private static bool ValidToken(string value) => value is { Length: 43 } &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}
