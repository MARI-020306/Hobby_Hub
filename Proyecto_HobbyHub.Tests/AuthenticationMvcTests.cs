using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Proyecto_HobbyHub.Security;

namespace Proyecto_HobbyHub.Tests;

public class AuthenticationMvcTests
{
    internal static HttpClient Client(OfflineApplicationFactory factory) => factory.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = false });

    internal static string Authenticate(OfflineApplicationFactory factory, HttpClient client, int userId,
        out string token, bool extraAdminRole = false)
    {
        using var scope = factory.Services.CreateScope();
        var principal = factory.SecurityStore.IssuePrincipal(userId, factory.Clock.GetUtcNow().UtcDateTime);
        if (extraAdminRole) ((ClaimsIdentity)principal.Identity!).AddClaim(new(ClaimTypes.Role, "Administrador"));
        var options = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get("Cookies");
        string cookie = options.TicketDataFormat.Protect(new AuthenticationTicket(principal, "Cookies"));
        var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider, User = principal };
        http.Request.Scheme = "https";
        var csrf = scope.ServiceProvider.GetRequiredService<IAntiforgery>().GetAndStoreTokens(http);
        var csrfName = scope.ServiceProvider.GetRequiredService<IOptions<AntiforgeryOptions>>().Value.Cookie.Name;
        string header = $"{options.Cookie.Name}={cookie}; {csrfName}={csrf.CookieToken}";
        client.DefaultRequestHeaders.Add("Cookie", header);
        token = csrf.RequestToken!;
        return header;
    }

    internal static async Task<(string Token, string Cookie)> LoginForm(HttpClient client)
    {
        var response = await client.GetAsync("/Auth/Login");
        string html = await response.Content.ReadAsStringAsync();
        string token = WebUtility.HtmlDecode(Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        Assert.NotEmpty(token);
        string cookie = response.Headers.GetValues("Set-Cookie").First().Split(';')[0];
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        return (token, cookie);
    }
    private static FormUrlEncodedContent LoginBody(string token, string email, string password, string returnUrl = "") => new(
        new Dictionary<string, string> { ["Email"] = email, ["Password"] = password,
            ["ReturnUrl"] = returnUrl, ["__RequestVerificationToken"] = token });

    [Theory]
    [InlineData("/Perfil/Editar", "/Perfil/Editar")]
    [InlineData("https://external.example/", "/Home")]
    [InlineData("//external.example/", "/Home")]
    public async Task LoginCreatesSecureCookieAndOnlyRedirectsLocally(string returnUrl, string expected)
    {
        using var factory = new OfflineApplicationFactory();
        using var client = Client(factory);
        var form = await LoginForm(client);
        var response = await client.PostAsync("/Auth/Login", LoginBody(form.Token, " PERSON1@EXAMPLE.TEST ", FakeAccountSecurityStore.Password, returnUrl));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(expected, response.Headers.Location!.ToString());
        string cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("HobbyHub.Auth="));
        Assert.Contains("secure", cookie.ToLowerInvariant());
        Assert.Contains("httponly", cookie.ToLowerInvariant());
        Assert.Contains("samesite=lax", cookie.ToLowerInvariant());
        Assert.DoesNotContain("expires=", cookie.ToLowerInvariant());
        Assert.Single(factory.SecurityStore.Sessions);
    }
    [Fact]
    public async Task IpLimiterRejectsEleventhLoginAttemptWithRetryAfter()
    {
        using var factory = new OfflineApplicationFactory();
        using var client = Client(factory);
        var form = await LoginForm(client);
        for (int i = 0; i < 10; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/Auth/Login", LoginBody(form.Token, "unknown@example.test", "wrong"))).StatusCode);
        var rejected = await client.PostAsync("/Auth/Login", LoginBody(form.Token, "unknown@example.test", "wrong"));
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("60", rejected.Headers.GetValues("Retry-After").Single());
    }
    [Fact]
    public async Task UnknownWrongPasswordAndSuspendedAccountsShowSameGenericError()
    {
        using var factory = new OfflineApplicationFactory();
        using var client = Client(factory);
        var form = await LoginForm(client);
        string? expected = null;
        foreach (var email in new[] { "unknown@example.test", FakeAccountSecurityStore.Email(1), FakeAccountSecurityStore.Email(2) })
        {
            factory.SecurityStore.Accounts[2] = factory.SecurityStore.Accounts[2] with { State = "Suspendido" };
            var result = await client.PostAsync("/Auth/Login", LoginBody(form.Token, email, "wrong"));
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            string error = Regex.Match(await result.Content.ReadAsStringAsync(), "<div class=\"error-message\">(.*?)</div>").Groups[1].Value;
            Assert.NotEmpty(error);
            expected ??= error;
            Assert.Equal(expected, error);
        }
    }
    [Fact]
    public async Task LogoutRequiresPostAndCsrfAndRevokesCookieReplay()
    {
        using var factory = new OfflineApplicationFactory();
        using var client = Client(factory);
        string cookie = Authenticate(factory, client, 1, out var token);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.GetAsync("/Auth/Logout")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Auth/Logout", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode);
        Assert.All(factory.SecurityStore.Sessions.Values, s => Assert.Null(s.RevocadaUtc));
        var logout = await client.PostAsync("/Auth/Logout", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.All(factory.SecurityStore.Sessions.Values, s => Assert.NotNull(s.RevocadaUtc));
        using var replay = Client(factory);
        replay.DefaultRequestHeaders.Add("Cookie", cookie);
        var denied = await replay.GetAsync("/__tests/admin");
        Assert.Contains("/Auth/Login", denied.Headers.Location!.ToString());
    }
    [Theory]
    [InlineData(1, "/__tests/admin", false)]
    [InlineData(2, "/__tests/admin", false)]
    [InlineData(3, "/__tests/admin", false)]
    [InlineData(4, "/__tests/admin", true)]
    [InlineData(1, "/__tests/moderation", false)]
    [InlineData(3, "/__tests/moderation", true)]
    [InlineData(4, "/__tests/moderation", true)]
    public async Task ManagementControllersEnforceRolePolicies(int id, string path, bool allowed)
    {
        using var factory = new OfflineApplicationFactory();
        using var client = Client(factory);
        Authenticate(factory, client, id, out _);
        var response = await client.GetAsync(path);
        Assert.Equal(allowed ? HttpStatusCode.OK : HttpStatusCode.Redirect, response.StatusCode);
        if (!allowed)
        {
            Assert.Contains("/Auth/AccesoDenegado", response.Headers.Location!.ToString());
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(response.Headers.Location)).StatusCode);
        }
    }
    [Theory]
    [InlineData("suspended")]
    [InlineData("unavailable")]
    [InlineData("extra-role")]
    public async Task CookieValidationFailsClosedBeforeProtectedAction(string scenario)
    {
        using var factory = new OfflineApplicationFactory();
        using var client = Client(factory);
        Authenticate(factory, client, 4, out _, scenario == "extra-role");
        if (scenario == "suspended") factory.SecurityStore.Accounts[4] = factory.SecurityStore.Accounts[4] with { State = "Suspendido" };
        if (scenario == "unavailable") factory.SecurityStore.Unavailable = true;
        var denied = await client.GetAsync("/__tests/admin");
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Contains("/Auth/Login", denied.Headers.Location!.ToString());
    }
    [Fact]
    public async Task LoginBackendFailureDoesNotExposeExceptionOrCredentials()
    {
        using var factory = new OfflineApplicationFactory();
        using var client = Client(factory);
        var form = await LoginForm(client);
        factory.SecurityStore.Unavailable = true;
        var response = await client.PostAsync("/Auth/Login", LoginBody(form.Token, FakeAccountSecurityStore.Email(1), FakeAccountSecurityStore.Password));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        string html = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Synthetic store unavailable", html);
        Assert.DoesNotContain(FakeAccountSecurityStore.Password, html);
    }
    [Fact]
    public async Task SharedLogoutFormRendersPostAndAntiforgeryToken()
    {
        using var factory = new OfflineApplicationFactory();
        using var client = Client(factory);
        Authenticate(factory, client, 4, out _);
        string html = await client.GetStringAsync("/__tests/logout-form");
        Assert.Contains("method=\"post\"", html);
        Assert.Contains("action=\"/Auth/Logout\"", html);
        Assert.Contains("__RequestVerificationToken", html);
    }
}
