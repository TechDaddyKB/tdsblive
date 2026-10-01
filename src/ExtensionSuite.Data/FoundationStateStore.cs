using Microsoft.Data.Sqlite;
using System.Threading.Channels;

namespace ExtensionSuite.Data;

// Direct connections avoid recursively logging EF operations from the logger itself.
public sealed class FoundationStateStore(string database)
{
    private bool ready;
    private long logPersistenceFailures;
    public long LogPersistenceFailures => Interlocked.Read(ref logPersistenceFailures);
    private readonly Channel<(string Json, DateTimeOffset Timestamp, int RetentionDays)> logs =
        Channel.CreateBounded<(string, DateTimeOffset, int)>(new BoundedChannelOptions(1024)
            { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = database, ForeignKeys = true, DefaultTimeout = 2 }.ToString());
        connection.Open();
        return connection;
    }

    public string? ReadConfiguration()
    {
        if (!File.Exists(database)) return null;
        using var connection = Open();
        using var exists = connection.CreateCommand();
        exists.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Configurations'";
        if (Convert.ToInt64(exists.ExecuteScalar()) == 0) return null;
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Json FROM Configurations WHERE Name = 'main'";
        return command.ExecuteScalar() as string;
    }

    public void Initialize(string configuration)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Configurations (Name, Json) VALUES ('main', $json) ON CONFLICT(Name) DO NOTHING";
        command.Parameters.AddWithValue("$json", configuration);
        command.ExecuteNonQuery();
        Volatile.Write(ref ready, true);
    }

    public void SaveConfiguration(string configuration, Action saveFallback)
    {
        if (!Volatile.Read(ref ready)) { saveFallback(); return; }
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE Configurations SET Json = $json WHERE Name = 'main'";
        command.Parameters.AddWithValue("$json", configuration);
        if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("Configuration persistence is not initialized.");
        saveFallback();
        transaction.Commit();
    }

    public void AppendLog(string json, DateTimeOffset timestamp, int retentionDays)
    {
        if (!Volatile.Read(ref ready)) return; // Startup logs remain in the file until migrations finish.
        if (!logs.Writer.TryWrite((json, timestamp, retentionDays))) Interlocked.Increment(ref logPersistenceFailures);
    }

    public async Task RunLogsAsync(CancellationToken cancellationToken)
    {
        while (await logs.Reader.WaitToReadAsync(cancellationToken)) FlushLogs(cancellationToken);
    }

    public void FlushLogs(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var batch = new List<(string Json, DateTimeOffset Timestamp, int RetentionDays)>();
            while (batch.Count < 64 && logs.Reader.TryRead(out var row)) batch.Add(row);
            if (batch.Count == 0) return;
            try { WriteLogs(batch); }
            catch (Exception error) when (error is SqliteException or IOException or UnauthorizedAccessException)
            { Interlocked.Add(ref logPersistenceFailures, batch.Count); }
        }
    }

    private void WriteLogs(List<(string Json, DateTimeOffset Timestamp, int RetentionDays)> batch)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO Logs (TimestampTicks, Json) VALUES ($time, $json)";
        var time = command.Parameters.Add("$time", SqliteType.Integer);
        var json = command.Parameters.Add("$json", SqliteType.Text);
        foreach (var row in batch)
        {
            time.Value = row.Timestamp.UtcTicks;
            json.Value = row.Json;
            command.ExecuteNonQuery();
        }
        using var prune = connection.CreateCommand();
        prune.Transaction = transaction;
        prune.CommandText = "DELETE FROM Logs WHERE TimestampTicks < $cutoff";
        prune.Parameters.AddWithValue("$cutoff", batch.Min(row => row.Timestamp.UtcDateTime.Date.AddDays(1 - row.RetentionDays).Ticks));
        prune.ExecuteNonQuery();
        transaction.Commit();
    }
}
