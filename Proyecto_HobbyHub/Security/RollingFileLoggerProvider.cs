using System.Text;
using System.Text.Json;

namespace Proyecto_HobbyHub.Security;

// No external package: implements the .NET 8 logging provider contract.
public sealed class RollingFileLoggerProvider : ILoggerProvider
{
    private readonly object gate = new();
    private readonly string directory;
    private readonly long maxBytes;
    private readonly int retained;
    private readonly int days;
    private string? current;
    private DateTime date;
    private long sequence;
    public RollingFileLoggerProvider(string directory, long maxBytes = 5 * 1024 * 1024, int retained = 14, int days = 14)
    {
        this.directory = directory;
        this.maxBytes = Math.Clamp(maxBytes, 4096, 100 * 1024 * 1024);
        this.retained = Math.Clamp(retained, 1, 100);
        this.days = Math.Clamp(days, 1, 90);
        Directory.CreateDirectory(directory);
        foreach (var file in new DirectoryInfo(directory).GetFiles("hobbyhub-*.jsonl").OrderByDescending(f => f.Name).Where((f, i) => i >= this.retained || f.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-this.days))) file.Delete();
    }
    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);
    public void Dispose() { }
    private void Write(string category, LogLevel level, string message)
    {
        lock (gate)
        {
            try
            {
                var now = DateTime.UtcNow;
                var line = JsonSerializer.Serialize(new { TimestampUtc = now, Level = level.ToString(), Category = category, Message = message }) + Environment.NewLine;
                if (current is null || date != now.Date || !File.Exists(current) || new FileInfo(current).Length + Encoding.UTF8.GetByteCount(line) > maxBytes)
                {
                    date = now.Date;
                    current = Path.Combine(directory, $"hobbyhub-{now:yyyyMMdd-HHmmssfffffff}-{Environment.ProcessId}-{sequence++}.jsonl");
                }
                File.AppendAllText(current, line, Encoding.UTF8);
                var files = new DirectoryInfo(directory).GetFiles("hobbyhub-*.jsonl").OrderByDescending(f => f.Name).ToArray();
                foreach (var file in files.Where((f, i) => f.FullName != current && (i >= retained || f.LastWriteTimeUtc < now.AddDays(-days)))) file.Delete();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine("HobbyHub: no se pudo escribir el archivo de logs técnicos.");
            }
        }
    }
    private sealed class FileLogger(RollingFileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level >= LogLevel.Information && level != LogLevel.None;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(level)) provider.Write(category, level, formatter(state, null));
        }
    }
}
