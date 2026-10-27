using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Proyecto_HobbyHub.Models;

namespace Proyecto_HobbyHub.Tests;

public class OfflineMvcTests : IClassFixture<OfflineApplicationFactory>
{
    private readonly OfflineApplicationFactory _factory;

    public OfflineMvcTests(OfflineApplicationFactory factory) => _factory = factory;

    private HttpClient Client() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
    });

    [Fact]
    public async Task LoginRendersWithAntiforgeryAndRegistrationLink()
    {
        using var client = Client();
        var response = await client.GetAsync("/Auth/Login");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string html = await response.Content.ReadAsStringAsync();
        Assert.Contains("__RequestVerificationToken", html);
        Assert.Contains("/Auth/Registro", html);
    }

    [Theory]
    [InlineData("/Home/Index")]
    [InlineData("/Perfil")]
    [InlineData("/Perfil/Editar")]
    [InlineData("/Comunidades")]
    [InlineData("/Comunidades/Detalle/1")]
    public async Task ExistingProtectedScreensRedirectAnonymousUsers(string path)
    {
        using var client = Client();
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Auth/Login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task LoginRejectsMissingAntiforgeryToken()
    {
        using var client = Client();
        var response = await client.PostAsync("/Auth/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "person@example.test", ["Password"] = "not-a-real-password"
        }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task InvalidLoginReturnsValidationErrorsWithoutDatabaseAccess()
    {
        using var client = Client();
        string html = await client.GetStringAsync("/Auth/Login");
        string token = WebUtility.HtmlDecode(Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        Assert.NotEmpty(token);
        var response = await client.PostAsync("/Auth/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "invalid-address", ["Password"] = "", ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("field-validation-error", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SqlConnectionIsIsolatedAndPhysicallyBlockedInTests()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HobbyHubContext>();
        Assert.Equal("HobbyHub_Development_OfflineTests", new SqlConnectionStringBuilder(
            context.Database.GetConnectionString()).InitialCatalog);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => context.Usuarios.AnyAsync());
        Assert.Equal(OfflineDatabaseInterceptor.Message, error.Message);
    }

    [Theory]
    [InlineData("/Comunidades/Publicar/1", "/Comunidades/Detalle/1")]
    [InlineData("/Home/Comentar", "/Home/Index")]
    [InlineData("/Perfil/Editar", null)]
    public async Task AuthenticatedInvalidFormsKeepTheirExistingRoutesAndDoNotAccessSql(string path, string? redirect)
    {
        using var client = Client();
        using var scope = _factory.Services.CreateScope();
        var principal = _factory.SecurityStore.IssuePrincipal(1, _factory.Clock.GetUtcNow().UtcDateTime);
        var options = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        string cookie = options.TicketDataFormat.Protect(new AuthenticationTicket(principal,
            CookieAuthenticationDefaults.AuthenticationScheme));
        var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider, User = principal };
        http.Request.Scheme = "https";
        var tokens = scope.ServiceProvider.GetRequiredService<IAntiforgery>().GetAndStoreTokens(http);
        string antiforgeryName = scope.ServiceProvider.GetRequiredService<IOptions<AntiforgeryOptions>>().Value.Cookie.Name!;
        client.DefaultRequestHeaders.Add("Cookie", $"{options.Cookie.Name}={cookie}; {antiforgeryName}={tokens.CookieToken}");
        var response = await client.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = tokens.RequestToken!,
            ["NuevaPublicacion.IdComunidad"] = "1", ["NuevaPublicacion.Contenido"] = "",
            ["idPublicacion"] = "1", ["contenido"] = "", ["Nombre"] = ""
        }));
        if (redirect is not null)
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(redirect, response.Headers.Location!.ToString());
        }
        else
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("field-validation-error", await response.Content.ReadAsStringAsync());
        }
    }
}
