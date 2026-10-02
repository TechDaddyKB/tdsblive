using System.Text.Json;
using ExtensionSuite.Core;
using ExtensionSuite.Data;
using Microsoft.Data.Sqlite;

namespace ExtensionSuite.Host;

public sealed record RecoveryReceipt(DateTimeOffset RestoredAt, string SafetyDirectory, bool ConnectionsDisabled, bool AutomationDisabled);

public sealed class RecoveryRestore(ApplicationPaths paths, TimeProvider clock)
{
    public async Task<RecoveryReceipt> ApplyAsync(PreparedRecovery prepared, RecoveryShutdown shutdown,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(paths.Root, shutdown.DataDirectory, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidOperationException("Restore requires shutdown of its owning application.");
        if ((File.GetAttributes(paths.Root) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Application data directory is not an ordinary local directory.");
        cancellationToken.ThrowIfCancellationRequested();
        await new RecoveryArchive(paths).VerifyPreparedAsync(prepared, cancellationToken);
        var receipt = new RecoveryReceipt(clock.GetUtcNow(), paths.Root + ".safety-" + Guid.NewGuid().ToString("N"), true, true);
        await MakeSafeAsync(prepared, cancellationToken);
        // Preserve only this computer's encrypted vault, never credentials from an archive.
        if (Directory.Exists(paths.Secrets))
        {
            if ((File.GetAttributes(paths.Secrets) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Credential directory is not an ordinary local directory.");
            var target = Path.Combine(prepared.DirectoryPath, "credentials");
            Directory.CreateDirectory(target);
            foreach (var file in Directory.EnumerateFiles(paths.Secrets, "*.dpapi"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (name.Length is < 1 or > 64 || name.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-') ||
                    (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Credential file is not an ordinary vault file.");
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: false);
            }
        }
        await File.WriteAllTextAsync(Path.Combine(prepared.DirectoryPath, "recovery-result.json"),
            JsonSerializer.Serialize(receipt, EventStore.JsonOptions), cancellationToken);
        // Clear only this stopped host's pools. Other test/application hosts stay intact.
        foreach (var timeout in new int?[] { null, 2 })
        {
            var options = new SqliteConnectionStringBuilder { DataSource = paths.Database, ForeignKeys = true };
            if (timeout is { } value) options.DefaultTimeout = value;
            using var connection = new SqliteConnection(options.ToString());
            SqliteConnection.ClearPool(connection);
        }
        cancellationToken.ThrowIfCancellationRequested();
        // Both directories are siblings on the same filesystem. Keep the old complete
        // directory as the safety backup. Once replacement begins, do not cancel halfway.
        Directory.Move(paths.Root, receipt.SafetyDirectory);
        try { Directory.Move(prepared.DirectoryPath, paths.Root); }
        catch
        {
            Directory.Move(receipt.SafetyDirectory, paths.Root);
            throw;
        }
        return receipt;
    }

    private async Task MakeSafeAsync(PreparedRecovery prepared, CancellationToken cancellationToken)
    {
        await using var db = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = prepared.DatabasePath, Pooling = false, ForeignKeys = true, Mode = SqliteOpenMode.ReadWrite }.ToString());
        await db.OpenAsync(cancellationToken);
        using var transaction = db.BeginTransaction();
        using var command = db.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Json FROM Configurations WHERE Name = 'main';";
        var configuration = JsonSerializer.Deserialize<ApplicationConfiguration>((string)(await command.ExecuteScalarAsync(cancellationToken))!, EventStore.JsonOptions)!;
        configuration.Validate();
        configuration = configuration with
        {
            Server = configuration.Server with { Host = "127.0.0.1", EnableLan = false, AllowedHosts = [] },
            StreamerBot = configuration.StreamerBot with { Enabled = false, ForwardLiveEvents = false },
            SpeakerBot = configuration.SpeakerBot with { Enabled = false },
            Rumble = configuration.Rumble with { Enabled = false, ForwardTriggers = false }
        };
        var rules = new List<AutomationRule>();
        command.CommandText = "SELECT Id, Version, length(Json), Json FROM AutomationRules;";
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken))
            {
                if (reader.GetInt64(2) > 262144) throw new InvalidDataException("Backup rule exceeds its supported size.");
                var rule = JsonSerializer.Deserialize<AutomationRule>(reader.GetString(3), EventStore.JsonOptions)
                    ?? throw new InvalidDataException("Backup rule is invalid.");
                rule.Validate();
                if (rule.Id != Guid.Parse(reader.GetString(0)) || rule.Version != reader.GetInt32(1))
                    throw new InvalidDataException("Backup rule identity is inconsistent.");
                rules.Add(rule with { Enabled = false, Version = checked(rule.Version + 1) });
            }
        foreach (var rule in rules)
        {
            command.Parameters.Clear();
            command.CommandText = "UPDATE AutomationRules SET Json=$json, Version=$version WHERE Id=$id;";
            command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(rule, EventStore.JsonOptions));
            command.Parameters.AddWithValue("$version", rule.Version);
            command.Parameters.AddWithValue("$id", rule.Id.ToString().ToUpperInvariant());
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1) throw new InvalidDataException("Backup rule identity is inconsistent.");
        }
        command.Parameters.Clear();
        command.CommandText = """
            UPDATE Configurations SET Json=$json WHERE Name='main';
            UPDATE AutomationExecutions SET State='cancelled', Detail='backup-restored', CancelRequested=1, Version=Version+1
                WHERE State IN ('queued','moderation-pending','language-review','waiting-effect','dispatching');
            UPDATE AutomationTemporaryEffects SET State='uncertain', Version=Version+1 WHERE State <> 'idle';
            UPDATE AutomationInbox SET Processed=1 WHERE Processed=0;
            UPDATE RumbleDeliveries SET State='restored-suppressed' WHERE State IN ('pending','dispatching','waitingForTrigger');
            UPDATE Outbox SET DeliveredAtTicks=$now WHERE DeliveredAtTicks IS NULL;
            UPDATE OverlayTokens SET Revoked=1;
            """;
        var json = JsonSerializer.Serialize(configuration, EventStore.JsonOptions);
        command.Parameters.AddWithValue("$json", json);
        command.Parameters.AddWithValue("$now", clock.GetUtcNow().UtcTicks);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(prepared.DirectoryPath, "configuration.json"), json, cancellationToken);
        transaction.Commit();
    }
}
