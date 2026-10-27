using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Proyecto_HobbyHub.Configuration;

/// <summary>Chooses the database without opening a connection or changing its schema.</summary>
public static class DatabaseConnectionPolicy
{
    public static bool RequiresIsolation(string environmentName) =>
        string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase);

    public static string Resolve(IConfiguration configuration, string environmentName)
    {
        bool isolated = RequiresIsolation(environmentName);
        string? value = isolated
            ? configuration["DevelopmentDatabase:ConnectionString"]
            : configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(isolated
                ? "Configura DevelopmentDatabase:ConnectionString para una base SQL Server local aislada."
                : "Configura ConnectionStrings:DefaultConnection para este entorno.");

        SqlConnectionStringBuilder connection;
        try
        {
            connection = new SqlConnectionStringBuilder(value);
        }
        catch (ArgumentException)
        {
            // Do not include the supplied value or the parser exception: either may contain secrets.
            throw new InvalidOperationException("La configuración de conexión SQL Server no es válida.");
        }

        if (isolated)
        {
            string server = connection.DataSource.Trim();
            bool local = Regex.IsMatch(server,
                @"^(?:\(localdb\)\\[a-z0-9_-]+|(?:\.|\(local\)|localhost|127\.0\.0\.1)(?:\\[a-z0-9_-]+)?(?:,[0-9]{1,5})?)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            string database = connection.InitialCatalog;
            bool developmentDatabase = string.Equals(database, "HobbyHub_Development", StringComparison.OrdinalIgnoreCase) ||
                database.StartsWith("HobbyHub_Development_", StringComparison.OrdinalIgnoreCase);

            if (!local || !developmentDatabase || !string.IsNullOrEmpty(connection.AttachDBFilename) ||
                !string.IsNullOrEmpty(connection.FailoverPartner))
                throw new InvalidOperationException(
                    "Development y Testing requieren un servidor local y una base HobbyHub_Development " +
                    "o HobbyHub_Development_<nombre>, sin archivos adjuntos ni failover. No se permite la conexión compartida.");

            connection.ConnectTimeout = Math.Min(connection.ConnectTimeout, 5);
        }

        return connection.ConnectionString;
    }
}
