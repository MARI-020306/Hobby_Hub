using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Proyecto_HobbyHub.Models;
using Proyecto_HobbyHub.Security;

namespace Proyecto_HobbyHub.Tests;

public class AccountSecurityTests
{
    private readonly FakeAccountSecurityStore _store = new();
    private readonly ManualTimeProvider _clock = new();
    private AccountSecurityService Service => new(_store, _clock, new MemorySecurityAudit());

    [Fact]
    public async Task LoginIssuesValidSessionAndNormalizesEmail()
    {
        var login = await Service.LoginAsync(" PERSON1@EXAMPLE.TEST ", FakeAccountSecurityStore.Password);
        Assert.NotNull(login);
        Assert.True(await Service.ValidateAsync(login.Principal));
        Assert.Equal(AccountSecurityService.SessionLifetime, login.ExpiresUtc - login.IssuedUtc);
        Assert.DoesNotContain(FakeAccountSecurityStore.Password, string.Join(',', login.Principal.Claims.Select(c => c.Value)));
    }
    [Fact]
    public async Task FiveFailuresLockAccountAndInvalidateExistingSessionsEvenAfterLockExpires()
    {
        var prior = (await Service.LoginAsync(FakeAccountSecurityStore.Email(1), FakeAccountSecurityStore.Password))!;
        for (int i = 0; i < 5; i++) Assert.Null(await Service.LoginAsync(FakeAccountSecurityStore.Email(1), "wrong"));
        Assert.Equal(5, _store.Failures[1]);
        Assert.Null(await Service.LoginAsync(FakeAccountSecurityStore.Email(1), FakeAccountSecurityStore.Password));
        Assert.False(await Service.ValidateAsync(prior.Principal));
        _clock.Advance(AccountSecurityService.LockoutDuration + TimeSpan.FromSeconds(1));
        Assert.NotNull(await Service.LoginAsync(FakeAccountSecurityStore.Email(1), FakeAccountSecurityStore.Password));
        Assert.Equal(0, _store.Failures[1]);
        Assert.False(await Service.ValidateAsync(prior.Principal));
    }
    [Fact]
    public async Task CorrectPasswordResetsFailures()
    {
        await Service.LoginAsync(FakeAccountSecurityStore.Email(1), "wrong");
        Assert.NotNull(await Service.LoginAsync(FakeAccountSecurityStore.Email(1), FakeAccountSecurityStore.Password));
        Assert.Equal(0, _store.Failures[1]);
    }
    [Theory]
    [InlineData("Suspendido")]
    [InlineData("Bloqueado")]
    [InlineData("Inactivo")]
    public async Task NonActiveAccountsCannotLoginOrKeepSessions(string state)
    {
        var prior = (await Service.LoginAsync(FakeAccountSecurityStore.Email(1), FakeAccountSecurityStore.Password))!;
        _store.Accounts[1] = _store.Accounts[1] with { State = state };
        Assert.Null(await Service.LoginAsync(FakeAccountSecurityStore.Email(1), FakeAccountSecurityStore.Password));
        Assert.False(await Service.ValidateAsync(prior.Principal));
    }
    [Theory]
    [InlineData("password")]
    [InlineData("role")]
    [InlineData("version")]
    [InlineData("delete")]
    public async Task SecurityChangesInvalidateSessionOnNextRequest(string change)
    {
        var prior = (await Service.LoginAsync(FakeAccountSecurityStore.Email(1), FakeAccountSecurityStore.Password))!;
        var a = _store.Accounts[1];
        switch (change)
        {
            case "password": _store.Accounts[1] = a with { PasswordHash = SecurityHelper.HashPassword("different-password") }; break;
            case "role": _store.Accounts[1] = a with { RoleId = 4, RoleName = "Administrador" }; break;
            case "version": _store.Accounts[1] = a with { SecurityVersion = Guid.NewGuid() }; break;
            case "delete": _store.Accounts.Remove(1); break;
        }
        Assert.False(await Service.ValidateAsync(prior.Principal));
    }
    [Fact]
    public async Task ExtraRoleClaimsCannotEscalateAccess()
    {
        var prior = (await Service.LoginAsync(FakeAccountSecurityStore.Email(1), FakeAccountSecurityStore.Password))!;
        ((ClaimsIdentity)prior.Principal.Identity!).AddClaim(new(ClaimTypes.Role, "Administrador"));
        Assert.False(await Service.ValidateAsync(prior.Principal));
    }
    [Fact]
    public async Task LegacyCookiesWithoutSessionClaimsAreRejected()
    {
        Assert.False(await Service.ValidateAsync(new ClaimsPrincipal(new ClaimsIdentity(new[]
            { new Claim(ClaimTypes.NameIdentifier, "1"), new Claim(ClaimTypes.Role, "Administrador") }, "Cookies"))));
    }
    [Fact]
    public async Task LogoutRevokesOnlyItsSessionAndRevokeAllInvalidatesTheRest()
    {
        var first = (await Service.LoginAsync(FakeAccountSecurityStore.Email(1), FakeAccountSecurityStore.Password))!;
        var second = (await Service.LoginAsync(FakeAccountSecurityStore.Email(1), FakeAccountSecurityStore.Password))!;
        await Service.LogoutAsync(first.Principal);
        Assert.False(await Service.ValidateAsync(first.Principal));
        Assert.True(await Service.ValidateAsync(second.Principal));
        await Service.RevokeAllAsync(1);
        Assert.False(await Service.ValidateAsync(second.Principal));
    }
    [Fact]
    public async Task SessionHasAbsoluteExpiry()
    {
        var prior = (await Service.LoginAsync(FakeAccountSecurityStore.Email(1), FakeAccountSecurityStore.Password))!;
        _clock.Advance(AccountSecurityService.SessionLifetime);
        Assert.False(await Service.ValidateAsync(prior.Principal));
    }
    [Fact]
    public async Task AccountChangedDuringPasswordCheckCannotGetNewSession()
    {
        _store.BeforeCreate = () => _store.Accounts[1] = _store.Accounts[1] with { State = "Suspendido" };
        Assert.Null(await Service.LoginAsync(FakeAccountSecurityStore.Email(1), FakeAccountSecurityStore.Password));
        Assert.Empty(_store.Sessions);
    }
    [Theory]
    [InlineData("Password", true)]
    [InlineData("RolId", true)]
    [InlineData("Estado", true)]
    [InlineData("Nombre", false)]
    public async Task EfSaveRotatesVersionForSecurityChangesOnlyWithoutOpeningDatabase(string property, bool rotates)
    {
        using var context = new HobbyHubContext(new DbContextOptionsBuilder<HobbyHubContext>()
            .UseSqlServer("Server=localhost;Database=HobbyHub_Development_OfflineTests;Integrated Security=True")
            .AddInterceptors(new OfflineDatabaseInterceptor()).Options);
        var user = new Usuario { IdUsuario = 1, Nombre = "Test", Password = "synthetic-hash", Correo = "hash", RolId = 1, Estado = "Activo" };
        context.Attach(user);
        Guid before = user.VersionSeguridad;
        context.Entry(user).Property(property).CurrentValue = property == "RolId" ? 2 : "changed";
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
        Assert.Equal(rotates, before != user.VersionSeguridad);
    }
    [Fact]
    public void BcryptLengthIsValidatedInBytesAndMalformedHashesFailSafely()
    {
        Assert.False(new BcryptPasswordAttribute().IsValid(new string('é', 37)));
        Assert.True(new BcryptPasswordAttribute().IsValid(new string('a', 72)));
        Assert.Throws<ArgumentException>(() => SecurityHelper.HashPassword(new string('é', 37)));
        Assert.False(SecurityHelper.VerifyPassword("password", "malformed"));
        Assert.DoesNotContain("Administrador", SecurityPolicies.RegistrationRoles);
        Assert.DoesNotContain("Moderador", SecurityPolicies.RegistrationRoles);
    }
}
