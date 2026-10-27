using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Proyecto_HobbyHub.Configuration;

namespace Proyecto_HobbyHub.Tests;

public class DatabaseConnectionPolicyTests
{
    private static IConfiguration Configuration(string? development, string? shared = null) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DevelopmentDatabase:ConnectionString"] = development,
            ["ConnectionStrings:DefaultConnection"] = shared
        }).Build();

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void IsolatedEnvironmentsNeverFallBackToSharedConfiguration(string environment)
    {
        var config = Configuration(null, "Server=shared.invalid;Database=Shared;User ID=user;Password=private-sentinel");
        var error = Assert.Throws<InvalidOperationException>(() => DatabaseConnectionPolicy.Resolve(config, environment));
        Assert.DoesNotContain("private-sentinel", error.ToString());
        Assert.DoesNotContain("shared.invalid", error.ToString());
    }

    [Theory]
    [InlineData("Server=remote.invalid;Database=HobbyHub_Development;Integrated Security=True")]
    [InlineData("Server=localhost;Database=Shared;Integrated Security=True")]
    [InlineData("Server=localhost;Database=HobbyHub_Development;Failover Partner=remote.invalid;Integrated Security=True")]
    [InlineData("Server=localhost;Database=HobbyHub_Development;AttachDbFilename=C:\\shared.mdf;Integrated Security=True")]
    [InlineData("Server=localhost;Database=HobbyHub_DevelopmentEvil;Integrated Security=True")]
    public void RejectsUnsafeDevelopmentTargets(string connection)
    {
        Assert.Throws<InvalidOperationException>(() => DatabaseConnectionPolicy.Resolve(Configuration(connection), "Development"));
    }

    [Theory]
    [InlineData("(localdb)\\HobbyHubDevelopment", "HobbyHub_Development")]
    [InlineData("localhost,14330", "HobbyHub_Development_Tests")]
    [InlineData(".\\HobbyHubDevelopment", "HobbyHub_Development")]
    public void AllowsExplicitLocalDevelopmentTargets(string server, string database)
    {
        var value = DatabaseConnectionPolicy.Resolve(
            Configuration($"Server={server};Database={database};Integrated Security=True;Connect Timeout=30"), "Development");
        var connection = new SqlConnectionStringBuilder(value);
        Assert.Equal(server, connection.DataSource);
        Assert.Equal(database, connection.InitialCatalog);
        Assert.Equal(5, connection.ConnectTimeout);
    }

    [Fact]
    public void ProductionRetainsItsExplicitConnection()
    {
        var value = DatabaseConnectionPolicy.Resolve(Configuration(null,
            "Server=production.invalid;Database=Production;Integrated Security=True"), "Production");
        Assert.Equal("Production", new SqlConnectionStringBuilder(value).InitialCatalog);
    }

    [Fact]
    public void MalformedConfigurationDoesNotEchoSecrets()
    {
        var error = Assert.Throws<InvalidOperationException>(() => DatabaseConnectionPolicy.Resolve(
            Configuration("private-sentinel=secret"), "Development"));
        Assert.DoesNotContain("private-sentinel", error.ToString());
        Assert.DoesNotContain("secret", error.ToString());
    }
}
