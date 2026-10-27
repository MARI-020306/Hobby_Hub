using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Proyecto_HobbyHub.Models;

namespace Proyecto_HobbyHub.Security;

public static class PersistenceDiagnostics
{
    // Only schema identifiers known to EF are allowed into technical logs.
    // Never log exception messages, commands, parameters or connection strings.
    public static void Log(ILogger logger, Exception error, HttpContext http, int status)
    {
        SqlException? sql = null;
        for (Exception? current = error; current is not null; current = current.InnerException)
            if (current is SqlException found) { sql = found; break; }
        var db = http.RequestServices.GetService<HobbyHubContext>();
        var columns = db?.Model.GetEntityTypes().SelectMany(e => e.GetProperties())
            .Select(p => p.GetColumnName()).Where(n => n is not null).Distinct().ToArray() ?? [];
        var invalid = sql?.Errors.Cast<SqlError>().Where(e => e.Number == 207)
            .SelectMany(e => columns.Where(c => e.Message.Contains("'" + c + "'", StringComparison.Ordinal)))
            .Distinct().Order().ToArray() ?? [];
        http.Items["FunctionalResult"] = status == 409 ? "Conflict" : "Unavailable";
        logger.LogWarning("PersistenceFailure Type={ExceptionType} SqlErrorNumber={SqlErrorNumber} InvalidColumns={InvalidColumns} CorrelationId={CorrelationId} IntendedStatusCode={IntendedStatusCode}",
            error.GetType().Name, sql?.Number, string.Join(",", invalid), http.TraceIdentifier, status);
    }
}
