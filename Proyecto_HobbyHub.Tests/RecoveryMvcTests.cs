using System.Net;
using System.Text.RegularExpressions;
using Proyecto_HobbyHub.Security;
using static Proyecto_HobbyHub.Tests.AuthenticationMvcTests;

namespace Proyecto_HobbyHub.Tests;

public class RecoveryMvcTests
{
    [Theory]
    [InlineData("/Cuenta/Seguridad")]
    [InlineData("/Cuenta/RevocarSesiones")]
    public async Task AnonymousCannotAccessAccountOperations(string path)
    {
        using var factory = new OfflineApplicationFactory();
        using var client = Client(factory);
        var result = path.EndsWith("RevocarSesiones") ? await client.PostAsync(path, Body()) : await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        Assert.Contains("/Auth/Login", result.Headers.Location!.ToString());
    }
    [Theory]
    [InlineData("/Cuenta/Recuperar", false)]
    [InlineData("/Cuenta/Restablecer", false)]
    [InlineData("/Cuenta/Seguridad", true)]
    [InlineData("/Cuenta/RevocarSesiones", true)]
    public async Task SensitivePostRequiresCsrf(string path, bool authenticated)
    {
        using var factory = new OfflineApplicationFactory();
        using var client = Client(factory);
        if (authenticated) Authenticate(factory, client, 1, out _);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(path, Body())).StatusCode);
    }
    [Fact]
    public async Task RecoveryResponseDoesNotRevealAccountEligibilityOrDeliveryFailure()
    {
        using var factory = new OfflineApplicationFactory();
        using var client = Client(factory);
        var csrf = await LoginForm(client);
        string? expected = null;
        foreach (var scenario in new[] { "known", "unknown", "suspended", "delivery" })
        {
            factory.SecurityStore.Accounts[1] = factory.SecurityStore.Accounts[1] with { State = scenario == "suspended" ? "Suspendido" : "Activo" };
            factory.Mailbox.FailDelivery = scenario == "delivery";
            var result = await client.PostAsync("/Cuenta/Recuperar", Body(("Email", scenario == "unknown" ? "unknown@example.test" : FakeAccountSecurityStore.Email(1)),
                ("__RequestVerificationToken", csrf.Token)));
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            string html = await result.Content.ReadAsStringAsync();
            string message = Regex.Match(html, "<p class=\"notice\" role=\"status\">(.*?)</p>").Groups[1].Value;
            Assert.NotEmpty(message);
            expected ??= message;
            Assert.Equal(expected, message);
            Assert.DoesNotContain("Synthetic delivery failure", html);
            Assert.All(factory.Mailbox.Messages, mail => Assert.DoesNotContain(mail.Token, html));
        }
    }
    [Fact]
    public async Task BrowserRecoveryFlowResetsPasswordAndRejectsSecondUse()
    {
        using var factory = new OfflineApplicationFactory();
        using var client = Client(factory);
        var csrf = await LoginForm(client);
        await client.PostAsync("/Cuenta/Recuperar", Body(("Email", FakeAccountSecurityStore.Email(1)), ("__RequestVerificationToken", csrf.Token)));
        string token = Assert.Single(factory.Mailbox.Messages).Token;
        var first = await client.PostAsync("/Cuenta/Restablecer", ResetBody(csrf.Token, token));
        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        var replay = await client.PostAsync("/Cuenta/Restablecer", ResetBody(csrf.Token, token));
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Contains("validation-summary-errors", await replay.Content.ReadAsStringAsync());
        Assert.Single(factory.Audit.Entries, a => a.Accion == "PasswordReset");
    }
    [Fact]
    public async Task ExpiredCodeShowsValidationWithoutChangingCredential()
    {
        using var factory = new OfflineApplicationFactory();
        using var client = Client(factory);
        var csrf = await LoginForm(client);
        await client.PostAsync("/Cuenta/Recuperar", Body(("Email", FakeAccountSecurityStore.Email(1)), ("__RequestVerificationToken", csrf.Token)));
        string before = factory.SecurityStore.Accounts[1].PasswordHash;
        factory.Clock.Advance(AccountRecoveryService.TokenLifetime);
        var result = await client.PostAsync("/Cuenta/Restablecer", ResetBody(csrf.Token, factory.Mailbox.Messages[0].Token));
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(before, factory.SecurityStore.Accounts[1].PasswordHash);
        Assert.DoesNotContain(factory.Audit.Entries, a => a.Accion == "PasswordReset");
    }
    [Fact]
    public async Task PasswordChangeVerifiesCurrentPasswordIgnoresPostedUserIdAndRevokesCookie()
    {
        using var factory = new OfflineApplicationFactory();
        using var client = Client(factory);
        Authenticate(factory, client, 1, out var csrf);
        string otherHash = factory.SecurityStore.Accounts[2].PasswordHash;
        var wrong = await client.PostAsync("/Cuenta/Seguridad", ChangeBody(csrf, "wrong"));
        Assert.Equal(HttpStatusCode.OK, wrong.StatusCode);
        var correct = await client.PostAsync("/Cuenta/Seguridad", ChangeBody(csrf, FakeAccountSecurityStore.Password));
        Assert.Equal(HttpStatusCode.Redirect, correct.StatusCode);
        Assert.Equal(otherHash, factory.SecurityStore.Accounts[2].PasswordHash);
        Assert.Contains(factory.Audit.Entries, a => a.Accion == "PasswordChanged" && a.IdObjetivo == 1);
        var denied = await client.GetAsync("/Cuenta/Seguridad");
        Assert.Contains("/Auth/Login", denied.Headers.Location!.ToString());
    }
    [Fact]
    public async Task AuthorizedAdministrativeAccessIsAuditedButRoleDeniedActionIsNotExecuted()
    {
        using var factory = new OfflineApplicationFactory();
        using var admin = Client(factory);
        Authenticate(factory, admin, 4, out _);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/__tests/admin")).StatusCode);
        Assert.Contains(factory.Audit.Entries, a => a.Accion == "AdministrationAccess" && a.IdActor == 4);
        using var user = Client(factory);
        Authenticate(factory, user, 1, out _);
        Assert.Equal(HttpStatusCode.Redirect, (await user.GetAsync("/__tests/admin")).StatusCode);
        Assert.DoesNotContain(factory.Audit.Entries, a => a.Accion == "AdministrationAccess" && a.IdActor == 1);
    }
    [Fact]
    public async Task RecoveryRateLimiterRejectsSixthRequest()
    {
        using var factory = new OfflineApplicationFactory();
        using var client = Client(factory);
        var csrf = await LoginForm(client);
        for (int i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/Cuenta/Recuperar",
            Body(("Email", "unknown@example.test"), ("__RequestVerificationToken", csrf.Token)))).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsync("/Cuenta/Recuperar",
            Body(("Email", "unknown@example.test"), ("__RequestVerificationToken", csrf.Token)))).StatusCode);
    }
    private static FormUrlEncodedContent ResetBody(string csrf, string token) => Body(("Token", token),
        ("Password", "new-test-password"), ("ConfirmPassword", "new-test-password"), ("__RequestVerificationToken", csrf));
    private static FormUrlEncodedContent ChangeBody(string csrf, string current) => Body(("CurrentPassword", current),
        ("Password", "new-test-password"), ("ConfirmPassword", "new-test-password"), ("IdUsuario", "2"), ("__RequestVerificationToken", csrf));
    private static FormUrlEncodedContent Body(params (string Key, string Value)[] fields) => new(fields.ToDictionary(f => f.Key, f => f.Value));
}
