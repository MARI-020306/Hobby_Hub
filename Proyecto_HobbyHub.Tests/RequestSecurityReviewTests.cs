using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Proyecto_HobbyHub.Models;
using Proyecto_HobbyHub.Security;

namespace Proyecto_HobbyHub.Tests;

public class RequestSecurityReviewTests
{
    [Fact]
    public void RejectionLogsAreBoundedAndUseServerTimeWithoutSensitiveInput()
    {
        var logger = new CaptureLogger();
        var clock = new ManualTimeProvider();
        var log = new RequestSecurityLog(logger, clock);
        var http = new DefaultHttpContext { TraceIdentifier = "synthetic-correlation" };
        http.Request.QueryString = new QueryString("?token=synthetic-sensitive-marker");
        for (int i = 0; i < 25; i++) log.Rejected(http, "RateLimitRejected");
        Assert.Equal(10, logger.Events.Count);
        Assert.All(logger.Events, values => Assert.Equal(clock.GetUtcNow(), values["TimestampUtc"]));
        Assert.All(logger.Events, values => Assert.DoesNotContain("synthetic-sensitive-marker", string.Join(" ", values.Values)));
        clock.Advance(TimeSpan.FromMinutes(1));
        log.Rejected(http, "AuthorizationDenied");
        Assert.Equal(12, logger.Events.Count); // One aggregate and one new event.
        Assert.Equal(15L, logger.Events[10]["Count"]);
    }

    private sealed class CaptureLogger : ILogger<RequestSecurityLog>
    {
        public List<Dictionary<string, object?>> Events { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Events.Add(((IEnumerable<KeyValuePair<string, object?>>)state!).ToDictionary(v => v.Key, v => v.Value));
    }

    [Fact]
    public async Task FailedManagementActionIsAnAttemptAndNeverSuccessfulAccess()
    {
        using var app = new OfflineApplicationFactory();
        using var client = AuthenticationMvcTests.Client(app);
        AuthenticationMvcTests.Authenticate(app, client, 4, out _);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/__tests/admin-failed")).StatusCode);
        Assert.Contains(app.Audit.Entries, a => a.Accion == "AdministrationAttempt" && !a.Exito);
        Assert.DoesNotContain(app.Audit.Entries, a => a.Accion == "AdministrationAccess");
    }

    [Theory]
    [InlineData("/Cuenta/Recuperar")]
    [InlineData("/Cuenta/Restablecer")]
    public async Task RecoveryLimitReportsItsActualFifteenMinuteWindow(string path)
    {
        using var app = new OfflineApplicationFactory();
        using var client = AuthenticationMvcTests.Client(app);
        var form = await AuthenticationMvcTests.LoginForm(client);
        for (int i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(path, new FormUrlEncodedContent(
                new Dictionary<string, string> { ["__RequestVerificationToken"] = form.Token }))).StatusCode);
        var response = await client.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.InRange(response.Headers.RetryAfter!.Delta!.Value.TotalSeconds, 850, 900);
        Assert.True(response.Headers.Contains("X-Correlation-ID"));
        Assert.Empty(app.RecoveryStore.Tokens);
    }

    [Fact]
    public async Task AnonymousQueriesAreLimitedWithoutTrustingForwardedIpHeaders()
    {
        using var app = new OfflineApplicationFactory();
        using var client = AuthenticationMvcTests.Client(app);
        for (int i = 0; i < 120; i++)
        {
            client.DefaultRequestHeaders.Remove("X-Forwarded-For");
            client.DefaultRequestHeaders.Add("X-Forwarded-For", $"192.0.2.{i}");
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Auth/Login")).StatusCode);
        }
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/Auth/Login")).StatusCode);
    }

    [Fact]
    public void AccountsBehindSharedIpHaveIndependentWritePartitions()
    {
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = IPAddress.Loopback;
        string anonymous = RequestLimits.Partition(http);
        http.User = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "1")], "Test"));
        string first = RequestLimits.Partition(http);
        http.User = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "2")], "Test"));
        Assert.NotEqual(first, RequestLimits.Partition(http));
        Assert.Equal(anonymous, RequestLimits.Partition(http, ipOnly: true));
    }

    [Fact]
    public void InvalidLimitConfigurationFailsClosed()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Security:RateLimits:Registration:PermitLimit"] = "0" }).Build();
        Assert.Throws<InvalidOperationException>(() => RequestLimits.Configure(new RateLimiterOptions(), configuration));
    }

    [Fact]
    public async Task ContentAuditSharesFailedSaveAndDoesNotChangeHistoricalDate()
    {
        using var db = new HobbyHubContext(new DbContextOptionsBuilder<HobbyHubContext>()
            .UseSqlServer("Server=localhost;Database=HobbyHub_Development_OfflineTests;Integrated Security=True")
            .AddInterceptors(new OfflineDatabaseInterceptor()).Options);
        var historical = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Unspecified);
        var post = new Publicacione { IdUsuario = 42, IdComunidad = 1, Contenido = "Synthetic", Estado = "Visible", FechaPublicacion = historical };
        db.Publicaciones.Add(post);
        var before = DateTime.UtcNow;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        var audit = Assert.Single(db.ChangeTracker.Entries<AuditoriaSeguridad>());
        Assert.Equal(EntityState.Added, audit.State); // Still pending; nothing was persisted.
        Assert.Equal("PublicationCreated", audit.Entity.Accion);
        Assert.Equal(42, audit.Entity.IdObjetivo);
        Assert.InRange(audit.Entity.FechaUtc, before, DateTime.UtcNow);
        Assert.Equal(DateTimeKind.Utc, audit.Entity.FechaUtc.Kind);
        Assert.Equal(historical, post.FechaPublicacion);
    }
}
