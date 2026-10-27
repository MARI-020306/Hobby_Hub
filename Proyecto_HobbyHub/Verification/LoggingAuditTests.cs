using System.Data.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Proyecto_HobbyHub.Models;
using Proyecto_HobbyHub.Security;
using Xunit;

public class LoggingAuditTests
{
    [Fact]
    public void AttemptAndActionReturnDoNotClaimFinalHttpStatus()
    {
        var logger = new CaptureLogger<RequestSecurityLog>();
        var log = new RequestSecurityLog(logger, TimeProvider.System);
        var http = new DefaultHttpContext();
        log.Write(http, "Auth.Registro", "Attempt");
        log.Write(http, "Auth.Registro", "Failed");
        Assert.All(logger.Messages, m => Assert.DoesNotContain("StatusCode", m));
        Assert.DoesNotContain(logger.Messages, m => m.Contains("Success"));
    }

    [Theory]
    [InlineData(200, "Rejected")]
    [InlineData(503, "Unavailable")]
    public async Task CompletionSeparatesFunctionalResultAndFinalHttpStatus(int status, string result)
    {
        var http = new DefaultHttpContext();
        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor
            { ControllerName = "Auth", ActionName = "Login" }), "test"));
        var logger = new CaptureLogger<TechnicalRequestMiddleware>();
        var middleware = new TechnicalRequestMiddleware(context => {
            context.Items["FunctionalResult"] = result;
            context.Response.StatusCode = status;
            return Task.CompletedTask;
        });
        await middleware.InvokeAsync(http, logger, new AuditEventBudget(TimeProvider.System));
        Assert.Contains(logger.Messages, m => m.Contains("Result=" + result) && m.Contains("StatusCode=" + status));
    }

    [Fact]
    public void FileProviderRotatesRetainsAndWritesUtcLevels()
    {
        string directory = Path.Combine(Path.GetTempPath(), "hobbyhub-log-test-" + Guid.NewGuid().ToString("N"));
        using var provider = new RollingFileLoggerProvider(directory, 4096, 2, 1);
        var logger = provider.CreateLogger("Verification");
        for (int i = 0; i < 30; i++) logger.LogInformation("Synthetic {Number} {Padding}", i, new string('x', 1800));
        logger.LogWarning("Warning event");
        logger.LogError("Error event");
        var files = Directory.GetFiles(directory, "*.jsonl");
        Assert.InRange(files.Length, 1, 2);
        string text = string.Join("", files.Select(File.ReadAllText));
        Assert.Contains("TimestampUtc", text);
        Assert.Contains("Information", text);
        Assert.Contains("Warning", text);
        Assert.Contains("Error", text);
        Assert.All(files, file => Assert.True(new FileInfo(file).Length <= 4096));
    }

    [Fact]
    public async Task UnexpectedFailureHasSafeResponseAndCorrelatedError()
    {
        var http = new DefaultHttpContext();
        http.Response.Body = new MemoryStream();
        http.Request.Headers["X-Correlation-ID"] = "untrusted";
        var logger = new CaptureLogger<TechnicalRequestMiddleware>();
        var middleware = new TechnicalRequestMiddleware(_ => throw new InvalidOperationException("SECRET-MARKER"));
        await middleware.InvokeAsync(http, logger, new AuditEventBudget(TimeProvider.System));
        Assert.Equal(500, http.Response.StatusCode);
        Assert.NotEqual("untrusted", http.TraceIdentifier);
        http.Response.Body.Position = 0;
        string response = await new StreamReader(http.Response.Body).ReadToEndAsync();
        Assert.Contains(http.TraceIdentifier, response);
        Assert.DoesNotContain("SECRET-MARKER", response + string.Join("", logger.Messages));
        Assert.Contains(logger.Messages, m => m.Contains("ServerFailure") && m.Contains(http.TraceIdentifier));
    }

    [Fact]
    public async Task FailedSaveKeepsAuditPendingWithUtcCorrelationAndResource()
    {
        var http = new DefaultHttpContext { TraceIdentifier = "server-correlation" };
        using var db = new HobbyHubContext(new DbContextOptionsBuilder<HobbyHubContext>()
            .UseSqlServer("Server=localhost;Database=HobbyHub_Development_Offline;Integrated Security=True")
            .AddInterceptors(new BlockDatabase()).Options, new HttpContextAccessor { HttpContext = http });
        db.Publicaciones.Add(new Publicacione { IdUsuario = 42, IdComunidad = 1, Contenido = "SECRET-MARKER" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        var audit = Assert.Single(db.ChangeTracker.Entries<AuditoriaSeguridad>());
        Assert.Equal(EntityState.Added, audit.State);
        Assert.Equal("PublicationCreated", audit.Entity.Accion);
        Assert.Equal("Publication", audit.Entity.TipoRecurso);
        Assert.Equal("server-correlation", audit.Entity.CorrelacionId);
        Assert.Equal(DateTimeKind.Utc, audit.Entity.FechaUtc.Kind);
        Assert.DoesNotContain("SECRET-MARKER", System.Text.Json.JsonSerializer.Serialize(audit.Entity));
    }

    [Fact]
    public async Task HistoricalAuditCannotBeEditedOrDeleted()
    {
        using var db = new HobbyHubContext(new DbContextOptionsBuilder<HobbyHubContext>().UseSqlServer("Server=localhost;Database=HobbyHub_Development_Offline;Integrated Security=True").Options);
        var audit = new AuditoriaSeguridad { IdAuditoria = 1, Accion = "Login" };
        db.Attach(audit);
        audit.Accion = "Changed";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.Entry(audit).State = EntityState.Deleted;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    private sealed class BlockDatabase : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Offline");
    }
    private sealed class CaptureLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
