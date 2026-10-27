using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Proyecto_HobbyHub.Security;

namespace Proyecto_HobbyHub.Tests;

public class LocalMailboxTests
{
    [Fact]
    public async Task LocalMailboxEncryptsRecipientAndTokenOutsidePublicDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), "HobbyHubMailboxTest_" + Guid.NewGuid().ToString("N"));
        var environment = new MailEnvironment { ContentRootPath = root, EnvironmentName = "Development" };
        var protection = new EphemeralDataProtectionProvider();
        var sender = new LocalRecoveryMailbox(environment, protection);
        var message = new RecoveryMail("synthetic@example.test", "synthetic-test-token", DateTime.UtcNow.AddMinutes(30));
        await sender.SendAsync(message, CancellationToken.None);
        string file = Assert.Single(Directory.GetFiles(Path.Combine(root, ".local", "mailbox")));
        try
        {
            string encrypted = await File.ReadAllTextAsync(file);
            Assert.DoesNotContain(message.Recipient, encrypted);
            Assert.DoesNotContain(message.Token, encrypted);
            string plain = protection.CreateProtector("HobbyHub.LocalRecoveryMailbox.v1").Unprotect(encrypted);
            Assert.Equal(message, JsonSerializer.Deserialize<RecoveryMail>(plain));
        }
        finally
        {
            File.Delete(file);
            Directory.Delete(Path.Combine(root, ".local", "mailbox"));
            Directory.Delete(Path.Combine(root, ".local"));
            Directory.Delete(root);
        }
    }
    [Fact]
    public async Task LocalMailboxCannotSendInProductionWithoutAuthorizedProvider()
    {
        var sender = new LocalRecoveryMailbox(new MailEnvironment { EnvironmentName = "Production" }, new EphemeralDataProtectionProvider());
        await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync(
            new RecoveryMail("synthetic@example.test", "synthetic", DateTime.UtcNow), CancellationToken.None));
    }
    private sealed class MailEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "HobbyHub.Tests";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
