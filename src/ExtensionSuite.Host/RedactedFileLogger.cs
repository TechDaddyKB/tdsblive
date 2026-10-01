using System.Text.Json;
using ExtensionSuite.Core;
using ExtensionSuite.Data;

namespace ExtensionSuite.Host;

public sealed class RedactedFileLoggerProvider(ApplicationPaths paths, ApplicationConfiguration configuration, SensitiveValues sensitive, FoundationStateStore? state = null) : ILoggerProvider
{
    private readonly object sync = new();
    private readonly LogLevel minimum = Enum.Parse<LogLevel>(configuration.MinimumLogLevel);
    private long writeFailures;
    public long WriteFailures => Interlocked.Read(ref writeFailures);
    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);
    public void Dispose() { }

    private void Write(string category, LogLevel level, EventId id, string message, Exception? exception)
    {
        if (level < minimum) return;
        var row = new { timestamp = DateTimeOffset.UtcNow, level = level.ToString(), category, eventId = id.Id,
            message = CredentialRedactor.Text(message, sensitive.Snapshot()), exceptionType = exception?.GetType().Name };
        var json = JsonSerializer.Serialize(row);
        try { WriteFile(json); }
        catch (IOException) { Interlocked.Increment(ref writeFailures); }
        catch (UnauthorizedAccessException) { Interlocked.Increment(ref writeFailures); }
        state?.AppendLog(json, row.timestamp, configuration.LogRetentionDays);
    }

    private void WriteFile(string json)
    {
        lock (sync)
        {
            Directory.CreateDirectory(paths.Logs);
            var cutoff = DateTime.UtcNow.Date.AddDays(1 - configuration.LogRetentionDays);
            foreach (var file in Directory.EnumerateFiles(paths.Logs, "*.jsonl"))
                if (DateTime.TryParseExact(Path.GetFileNameWithoutExtension(file), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var date) && date < cutoff) File.Delete(file);
            File.AppendAllText(Path.Combine(paths.Logs, DateTime.UtcNow.ToString("yyyy-MM-dd") + ".jsonl"), json + Environment.NewLine);
        }
    }

    private sealed class FileLogger(RedactedFileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= provider.minimum && logLevel != LogLevel.None;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel)) provider.Write(category, logLevel, eventId, formatter(state, exception), exception);
        }
    }
}
