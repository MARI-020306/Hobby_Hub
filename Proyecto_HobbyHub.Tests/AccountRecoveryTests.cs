using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Proyecto_HobbyHub.Models;
using Proyecto_HobbyHub.Security;

namespace Proyecto_HobbyHub.Tests;

public class AccountRecoveryTests
{
    private readonly FakeAccountSecurityStore _accounts = new();
    private readonly ManualTimeProvider _clock = new();
    private readonly MemorySecurityAudit _audit = new();
    private readonly MemoryRecoveryMailbox _mail = new();
    private readonly FakeAccountRecoveryStore _recovery;
    private readonly AccountRecoveryService _service;
    private AccountSecurityService Security => new(_accounts, _clock, _audit);
    public AccountRecoveryTests()
    {
        _accounts.Audit = _audit;
        _recovery = new(_accounts, _audit);
        _service = new(_accounts, _recovery, _mail, _audit, _clock, Security);
    }
    [Fact]
    public async Task RequestStoresOnlyHashAndSendsOpaqueRandomTokenToLocalInbox()
    {
        await _service.RequestAsync(" PERSON1@EXAMPLE.TEST ");
        var mail = Assert.Single(_mail.Messages);
        var token = Assert.Single(_recovery.Tokens);
        Assert.Equal(43, mail.Token.Length);
        Assert.Equal("person1@example.test", mail.Recipient);
        Assert.Equal(SecurityHelper.HashPersonalData(mail.Token), token.TokenHash);
        Assert.NotEqual(mail.Token, token.TokenHash);
        Assert.Equal(AccountRecoveryService.TokenLifetime, token.ExpiraUtc - token.CreadaUtc);
        Assert.Contains(_audit.Entries, a => a.Accion == "RecoveryIssued" && a.IdObjetivo == 1);
    }
    [Theory]
    [InlineData("Suspendido")]
    [InlineData("Bloqueado")]
    [InlineData("Inactivo")]
    public async Task NonActiveAccountCannotRequestRecovery(string state)
    {
        _accounts.Accounts[1] = _accounts.Accounts[1] with { State = state };
        await _service.RequestAsync(FakeAccountSecurityStore.Email(1));
        Assert.Empty(_mail.Messages);
        Assert.Empty(_recovery.Tokens);
    }
    [Fact]
    public async Task UnknownAccountNeverGeneratesMailOrToken()
    {
        await _service.RequestAsync("unknown@example.test");
        Assert.Empty(_mail.Messages);
        Assert.Empty(_recovery.Tokens);
        Assert.All(_audit.Entries, a => Assert.Null(a.IdObjetivo));
    }
    [Fact]
    public async Task ResetConsumesTokenUsesBcryptRevokesSessionsAndPreventsReplay()
    {
        var session = (await Security.LoginAsync(FakeAccountSecurityStore.Email(1), FakeAccountSecurityStore.Password))!;
        await _service.RequestAsync(FakeAccountSecurityStore.Email(1));
        var mail = Assert.Single(_mail.Messages);
        Assert.True(await _service.ResetAsync(mail.Token, "new-test-password"));
        Assert.False(await Security.ValidateAsync(session.Principal));
        Assert.False(await _service.ResetAsync(mail.Token, "another-password"));
        Assert.True(SecurityHelper.VerifyPassword("new-test-password", _accounts.Accounts[1].PasswordHash));
        Assert.False(SecurityHelper.VerifyPassword(FakeAccountSecurityStore.Password, _accounts.Accounts[1].PasswordHash));
        Assert.NotNull(Assert.Single(_recovery.Tokens).ConsumidaUtc);
        Assert.Single(_audit.Entries, a => a.Accion == "PasswordReset");
        Assert.Contains(_audit.Entries, a => a.Accion == "SessionsRevoked");
        Assert.Contains(_audit.Entries, a => a.Accion == "PasswordResetDenied" && !a.Exito);
    }
    [Fact]
    public async Task ExpiredTokenFailsExactlyAtExpiry()
    {
        await _service.RequestAsync(FakeAccountSecurityStore.Email(1));
        _clock.Advance(AccountRecoveryService.TokenLifetime);
        Assert.False(await _service.ResetAsync(_mail.Messages[0].Token, "new-test-password"));
        Assert.Null(_recovery.Tokens[0].ConsumidaUtc);
    }
    [Fact]
    public async Task NewRequestInvalidatesOlderToken()
    {
        await _service.RequestAsync(FakeAccountSecurityStore.Email(1));
        await _service.RequestAsync(FakeAccountSecurityStore.Email(1));
        Assert.NotEqual(_mail.Messages[0].Token, _mail.Messages[1].Token);
        Assert.False(await _service.ResetAsync(_mail.Messages[0].Token, "new-test-password"));
        Assert.True(await _service.ResetAsync(_mail.Messages[1].Token, "new-test-password"));
    }
    [Theory]
    [InlineData("version")]
    [InlineData("suspended")]
    [InlineData("blocked")]
    public async Task AccountSecurityChangesInvalidateIssuedToken(string change)
    {
        await _service.RequestAsync(FakeAccountSecurityStore.Email(1));
        var a = _accounts.Accounts[1];
        _accounts.Accounts[1] = change switch
        {
            "version" => a with { SecurityVersion = Guid.NewGuid() },
            "suspended" => a with { State = "Suspendido" },
            _ => a with { State = "Bloqueado" }
        };
        Assert.False(await _service.ResetAsync(_mail.Messages[0].Token, "new-test-password"));
    }
    [Fact]
    public async Task RecoveryCanClearTemporaryFailureLockButNeverSuspension()
    {
        for (int i = 0; i < 5; i++) await Security.LoginAsync(FakeAccountSecurityStore.Email(1), "wrong");
        await _service.RequestAsync(FakeAccountSecurityStore.Email(1));
        Assert.True(await _service.ResetAsync(_mail.Messages[0].Token, "new-test-password"));
        Assert.Null(_accounts.Accounts[1].LockoutUntilUtc);
        Assert.Equal(0, _accounts.Failures[1]);
    }
    [Fact]
    public async Task ConcurrentResetHasOneSuccessfulConsumerInTestDouble()
    {
        await _service.RequestAsync(FakeAccountSecurityStore.Email(1));
        string token = _mail.Messages[0].Token;
        var results = await Task.WhenAll(Task.Run(() => _service.ResetAsync(token, "new-test-password")),
            Task.Run(() => _service.ResetAsync(token, "other-test-password")));
        Assert.Single(results, success => success);
    }
    [Fact]
    public async Task WrongCurrentPasswordDoesNotChangePasswordAndCountsFailure()
    {
        var principal = _accounts.IssuePrincipal(1, _clock.GetUtcNow().UtcDateTime);
        string before = _accounts.Accounts[1].PasswordHash;
        Assert.False(await _service.ChangeAsync(principal, "wrong", "new-test-password"));
        Assert.Equal(before, _accounts.Accounts[1].PasswordHash);
        Assert.Equal(1, _accounts.Failures[1]);
        Assert.Contains(_audit.Entries, a => a.Accion == "PasswordChangeDenied" && a.IdActor == 1);
    }
    [Fact]
    public async Task CorrectCurrentPasswordCannotBypassRevokedOrUnissuedSession()
    {
        var session = (await Security.LoginAsync(FakeAccountSecurityStore.Email(1), FakeAccountSecurityStore.Password))!;
        await Security.LogoutAsync(session.Principal);
        Assert.False(await _service.ChangeAsync(session.Principal, FakeAccountSecurityStore.Password, "new-test-password"));
        var unissued = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "1") }, "Test"));
        Assert.False(await _service.ChangeAsync(unissued, FakeAccountSecurityStore.Password, "new-test-password"));
    }
    [Fact]
    public async Task PasswordChangeRequiresCurrentPasswordAndRevokesTokensAndAllSessions()
    {
        var first = (await Security.LoginAsync(FakeAccountSecurityStore.Email(1), FakeAccountSecurityStore.Password))!;
        var second = (await Security.LoginAsync(FakeAccountSecurityStore.Email(1), FakeAccountSecurityStore.Password))!;
        await _service.RequestAsync(FakeAccountSecurityStore.Email(1));
        Assert.True(await _service.ChangeAsync(first.Principal, FakeAccountSecurityStore.Password, "new-test-password"));
        Assert.False(await Security.ValidateAsync(first.Principal));
        Assert.False(await Security.ValidateAsync(second.Principal));
        Assert.False(await _service.ResetAsync(_mail.Messages[0].Token, "other-test-password"));
        Assert.Contains(_audit.Entries, a => a.Accion == "PasswordChanged" && a.IdActor == 1);
    }
    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa+")]
    public async Task MalformedTokenCannotResetPassword(string token)
    {
        Assert.False(await _service.ResetAsync(token, "new-test-password"));
    }
    [Fact]
    public async Task AuditRecordsLoginLogoutAndRevocationWithoutSensitivePayload()
    {
        var session = (await Security.LoginAsync(FakeAccountSecurityStore.Email(1), FakeAccountSecurityStore.Password))!;
        await Security.LoginAsync(FakeAccountSecurityStore.Email(1), "wrong");
        await Security.LogoutAsync(session.Principal);
        await Security.RevokeAllAsync(1);
        Assert.Contains(_audit.Entries, a => a.Accion == "Login" && a.Exito);
        Assert.Contains(_audit.Entries, a => a.Accion == "LoginDenied" && !a.Exito);
        Assert.Contains(_audit.Entries, a => a.Accion == "Logout");
        Assert.Contains(_audit.Entries, a => a.Accion == "SessionsRevoked");
        Assert.DoesNotContain(typeof(AuditoriaSeguridad).GetProperties(), p => p.Name.Contains("Password") || p.Name.Contains("Token") || p.Name.Contains("Correo"));
    }
    [Theory]
    [InlineData("RolId")]
    [InlineData("Estado")]
    public async Task AdministrativeChangesQueueAuditInsideEfSave(string property)
    {
        using var context = Context();
        var user = new Usuario { IdUsuario = 1, Nombre = "Test", Password = "synthetic", Correo = "synthetic", RolId = 1, Estado = "Activo" };
        context.Attach(user);
        context.Entry(user).Property(property).CurrentValue = property == "RolId" ? 2 : "Suspendido";
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
        var audit = Assert.Single(context.ChangeTracker.Entries<AuditoriaSeguridad>());
        Assert.Equal(1, audit.Entity.IdObjetivo);
        Assert.Equal(property == "RolId" ? "AccountRoleChanged" : "AccountStateChanged", audit.Entity.Accion);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ApplicationCannotEditOrDeleteAuditRecords(bool delete)
    {
        using var context = Context();
        var audit = new AuditoriaSeguridad { IdAuditoria = 1, Accion = "Login" };
        context.Attach(audit);
        if (delete) context.Remove(audit); else audit.Accion = "changed";
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
        Assert.Contains("auditoría", error.Message);
    }
    private static HobbyHubContext Context() => new(new DbContextOptionsBuilder<HobbyHubContext>()
        .UseSqlServer("Server=localhost;Database=HobbyHub_Development_OfflineTests;Integrated Security=True")
        .AddInterceptors(new OfflineDatabaseInterceptor()).Options);
}
