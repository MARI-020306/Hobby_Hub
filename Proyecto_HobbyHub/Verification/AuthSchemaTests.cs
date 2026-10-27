using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Proyecto_HobbyHub.Configuration;
using Proyecto_HobbyHub.Controllers;
using Proyecto_HobbyHub.Models;
using Proyecto_HobbyHub.Security;
using Proyecto_HobbyHub.ViewModels;
using Xunit;
using Xunit.Abstractions;

public class AuthSchemaTests(ITestOutputHelper output)
{
    public sealed class SchemaDiagnosticFactAttribute : FactAttribute
    {
        public SchemaDiagnosticFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("HOBBYHUB_SCHEMA_DIAGNOSTIC") != "1")
                Skip = "Explicit pre-migration SQL diagnostic opt-in required.";
        }
    }

    [SchemaDiagnosticFact]
    public async Task ConfiguredSqlRegistrationAndUnknownLoginDiagnoseSeparately()
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            Environment.GetEnvironmentVariable("HOBBYHUB_PROJECT_ROOT") ?? throw new InvalidOperationException("Project root required")));
        var config = new ConfigurationBuilder().SetBasePath(root).AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.Isolated.json")
            .AddJsonFile("appsettings.Development.Local.json", true)
            .AddUserSecrets<Program>(true).AddEnvironmentVariables().Build();
        string connection = DatabaseConnectionPolicy.Resolve(config, "Development");
        var target = new SqlConnectionStringBuilder(connection);
        Assert.Equal("(localdb)\\HobbyHubPhase1C_20261008", target.DataSource);
        Assert.Equal("HobbyHub_Development_Phase1C_20261008", target.InitialCatalog);
        output.WriteLine("Effective target: " + target.DataSource + " / " + target.InitialCatalog);
        var options = new DbContextOptionsBuilder<HobbyHubContext>().UseSqlServer(connection).Options;
        await using var db = new HobbyHubContext(options);
        await db.Database.OpenConnectionAsync();
        Assert.Equal(target.InitialCatalog, db.Database.GetDbConnection().Database);
        var service = new AccountSecurityService(new EfAccountSecurityStore(db), TimeProvider.System,
            new EfSecurityAudit(db, TimeProvider.System, new AuditEventBudget(TimeProvider.System)));
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            int role = await db.Roles.Where(r => r.Nombre == "Usuario").Select(r => r.IdRol).FirstAsync();
            var controller = new AuthController(db, service, NullLogger<AuthController>.Instance)
                { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
            await Diagnose("Registration", () => controller.Registro(new RegistroViewModel {
                Nombre = "Schema probe", Correo = Guid.NewGuid().ToString("N") + "@example.test",
                Password = Guid.NewGuid().ToString("N"), RolId = role }));
            await tx.RollbackAsync();
        }
        db.ChangeTracker.Clear();
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            await Diagnose("Unknown-account login", () => service.LoginAsync(Guid.NewGuid().ToString("N") + "@example.test", Guid.NewGuid().ToString("N")));
            await tx.RollbackAsync();
        }
    }

    private async Task Diagnose(string operation, Func<Task> run)
    {
        var error = await Record.ExceptionAsync(run);
        var sql = error as SqlException ?? error?.InnerException as SqlException;
        Assert.NotNull(sql);
        Assert.Equal(207, sql.Number);
        string[] expected = ["CorrelacionId", "IdRecurso", "TipoRecurso"];
        var actual = sql.Errors.Cast<SqlError>().Where(e => e.Number == 207)
            .SelectMany(e => expected.Where(c => e.Message.Contains("'" + c + "'"))).Distinct().Order().ToArray();
        Assert.Equal(expected, actual);
        output.WriteLine(operation + ": SQL 207, columns=" + string.Join(",", actual));
    }
}
