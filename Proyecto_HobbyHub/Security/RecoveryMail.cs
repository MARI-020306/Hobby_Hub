using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Proyecto_HobbyHub.Security;

public sealed record RecoveryMail(string Recipient, string Token, DateTime ExpiresUtc);

public interface IRecoveryMailSender
{
    Task SendAsync(RecoveryMail mail, CancellationToken cancellationToken);
}

// Development pickup mailbox, encrypted using the existing Data Protection key ring.
// No network delivery, HTTP mailbox route or logging of message contents.
public sealed class LocalRecoveryMailbox : IRecoveryMailSender
{
    private readonly IWebHostEnvironment _environment;
    private readonly IDataProtector _protector;
    public LocalRecoveryMailbox(IWebHostEnvironment environment, IDataProtectionProvider protection)
    {
        _environment = environment;
        _protector = protection.CreateProtector("HobbyHub.LocalRecoveryMailbox.v1");
    }
    public async Task SendAsync(RecoveryMail mail, CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment() && !_environment.IsEnvironment("Testing"))
            throw new InvalidOperationException("No hay proveedor de correo autorizado para este entorno.");
        string directory = Path.Combine(_environment.ContentRootPath, ".local", "mailbox");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, Guid.NewGuid().ToString("N") + ".mail"),
            _protector.Protect(JsonSerializer.Serialize(mail)), cancellationToken);
    }
}
