using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Proyecto_HobbyHub.Models;

namespace Proyecto_HobbyHub.Tests;

public class SchemaSafetyTests
{
    public static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Proyecto_HobbyHub.sln")))
                return directory.FullName;
        throw new InvalidOperationException("Run these source/schema safety tests from the repository checkout.");
    }

    [Fact]
    public void ApplicationContainsNoAutomaticSchemaMutation()
    {
        string project = Path.Combine(RepositoryRoot(), "Proyecto_HobbyHub");
        foreach (string file in Directory.EnumerateFiles(project, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(p => p is "bin" or "obj")))
        {
            string source = File.ReadAllText(file);
            Assert.False(Regex.IsMatch(source, @"\b(?:CREATE|ALTER|DROP|TRUNCATE)\s+(?:TABLE|DATABASE)\b", RegexOptions.IgnoreCase),
                $"Schema SQL must stay outside the application: {Path.GetFileName(file)}");
            Assert.False(Regex.IsMatch(source, @"\b(?:EnsureCreated|EnsureDeleted|Migrate|ExecuteSqlRaw|ExecuteSqlInterpolated)(?:Async)?\s*\("),
                $"Schema/database execution must not be triggered by web requests: {Path.GetFileName(file)}");
        }
    }

    [Theory]
    [InlineData("001_EmptyDevelopmentBaseline.sql")]
    [InlineData("002_ExistingFeatureExtensions.sql")]
    [InlineData("003_DevelopmentRoles.sql")]
    [InlineData("005_AccountSessionsSecurity.sql")]
    [InlineData("006_RecoveryAndAudit.sql")]
    [InlineData("007_SocialModules.sql")]
    [InlineData("008_ModerationConcurrency.sql")]
    [InlineData("009_PersonalBlocksAndMessageReports.sql")]
    public void MutatingScriptsAreDevelopmentGuardedAndNonDestructive(string name)
    {
        string sql = File.ReadAllText(Path.Combine(RepositoryRoot(), "Proyecto_HobbyHub", "Database", "Scripts", name));
        int guard = sql.IndexOf("THROW 51000", StringComparison.Ordinal);
        int transaction = sql.IndexOf("BEGIN TRANSACTION", StringComparison.Ordinal);
        Assert.True(guard >= 0 && guard < transaction);
        Assert.Contains("DB_NAME()", sql);
        Assert.Contains("HobbyHub_Development", sql);
        Assert.DoesNotMatch(@"(?i)\b(?:DROP\s+(?:TABLE|DATABASE)|TRUNCATE\s+TABLE|DELETE\s+FROM)\b", sql);
        Assert.Contains("ROLLBACK TRANSACTION", sql);
    }

    [Fact]
    public void ReviewedScriptsCoverEveryCurrentEntityTableAndColumn()
    {
        string scripts = Path.Combine(RepositoryRoot(), "Proyecto_HobbyHub", "Database", "Scripts");
        string sql = File.ReadAllText(Path.Combine(scripts, "001_EmptyDevelopmentBaseline.sql")) +
            File.ReadAllText(Path.Combine(scripts, "002_ExistingFeatureExtensions.sql")) +
            File.ReadAllText(Path.Combine(scripts, "005_AccountSessionsSecurity.sql")) +
            File.ReadAllText(Path.Combine(scripts, "006_RecoveryAndAudit.sql"))+
            File.ReadAllText(Path.Combine(scripts, "007_SocialModules.sql"))+
            File.ReadAllText(Path.Combine(scripts, "008_ModerationConcurrency.sql"))+
            File.ReadAllText(Path.Combine(scripts, "009_PersonalBlocksAndMessageReports.sql"));
        using var context = new HobbyHubContext(new DbContextOptionsBuilder<HobbyHubContext>()
            .UseSqlServer("Server=localhost;Database=HobbyHub_Development_OfflineTests;Integrated Security=True")
            .AddInterceptors(new OfflineDatabaseInterceptor()).Options);
        var entities = context.Model.GetEntityTypes().ToList();
        Assert.Equal(18, entities.Count);
        foreach (var entity in entities)
        {
            string table = entity.GetTableName()!;
            Assert.Matches($@"(?i)CREATE\s+TABLE\s+(?:dbo\.|\[dbo\]\.\[){Regex.Escape(table)}\]?\s*\(", sql);
            foreach (var property in entity.GetProperties())
                Assert.Matches($@"(?i)\b{Regex.Escape(property.Name)}\]?\s+(?:BIGINT|INT|VARCHAR|BIT|DATETIME2?|UNIQUEIDENTIFIER|ROWVERSION)\b", sql);
        }
        // Query/script generation is pure metadata work; no database connection is opened.
        Assert.Contains("CREATE TABLE", context.Database.GenerateCreateScript());
    }
}
