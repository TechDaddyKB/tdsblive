using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ExtensionSuite.Core;
using ExtensionSuite.Overlays;
using Microsoft.Data.Sqlite;

namespace ExtensionSuite.Host;

public sealed record RecoveryFile(string Name, long Length, string Sha256);
public sealed record RecoveryManifest(int Format, DateTimeOffset CreatedAt, RecoveryFile[] Files);

// Owns only a newly created staging directory; it never deletes application data.
public sealed class PreparedRecovery : IAsyncDisposable
{
    internal PreparedRecovery(string directory) => DirectoryPath = directory;
    internal RecoveryManifest? Manifest { get; set; }
    public string DirectoryPath { get; }
    public string DatabasePath => Path.Combine(DirectoryPath, "tdsblive.db");
    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, recursive: true);
        return ValueTask.CompletedTask;
    }
}

public sealed class RecoveryArchive(ApplicationPaths paths)
{
    public const long MaximumArchiveBytes = 1024L * 1024 * 1024;
    public const long MaximumDatabaseBytes = 512L * 1024 * 1024;
    public const int MaximumAssets = 10000;
    private const int MaximumManifestBytes = 2 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true
    };

    public async Task CreateAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        await using var snapshot = NewStaging();
        // SQLite's backup API takes a consistent committed snapshot, including WAL.
        // Immutable, content-addressed assets are selected from that snapshot's metadata.
        using (var source = Open(paths.Database, readOnly: true))
        using (var target = Open(snapshot.DatabasePath, readOnly: false))
        {
            source.BackupDatabase(target);
            using var command = target.CreateCommand();
            command.CommandText = "PRAGMA journal_mode=DELETE;";
            if (command.ExecuteScalar() as string != "delete") throw new InvalidDataException("Backup snapshot could not be made self-contained.");
        }
        var assets = AssetIds(snapshot.DatabasePath);
        var files = new List<RecoveryFile>();
        files.Add(await DescribeAsync("tdsblive.db", snapshot.DatabasePath, cancellationToken));
        foreach (var id in assets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = Path.Combine(paths.Root, "assets", id);
            if (!File.Exists(source) || IsLink(source)) throw new InvalidDataException("A required backup asset is unavailable.");
            var info = await DescribeAsync("assets/" + id, source, cancellationToken);
            if (info.Length > AssetValidation.MaximumBytes || info.Sha256 != id)
                throw new InvalidDataException("A backup asset does not match its content identity.");
            files.Add(info);
        }
        if (files.Sum(file => file.Length) > MaximumArchiveBytes) throw new InvalidDataException("Backup exceeds the supported size.");
        using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);
        foreach (var file in files)
        {
            var source = file.Name == "tdsblive.db" ? snapshot.DatabasePath : Path.Combine(paths.Root, "assets", Path.GetFileName(file.Name));
            await using var input = File.OpenRead(source);
            await using var output = archive.CreateEntry(file.Name, CompressionLevel.Optimal).Open();
            await input.CopyToAsync(output, cancellationToken);
        }
        await using var manifest = archive.CreateEntry("manifest.json", CompressionLevel.Optimal).Open();
        await JsonSerializer.SerializeAsync(manifest, new RecoveryManifest(1, DateTimeOffset.UtcNow, [.. files]), Json, cancellationToken);
    }

    public async Task<PreparedRecovery> PrepareAsync(Stream source, CancellationToken cancellationToken = default)
    {
        if (!source.CanSeek || source.Length > MaximumArchiveBytes) throw new InvalidDataException("Backup upload exceeds the supported size.");
        var stage = NewStaging();
        var accepted = false;
        try
        {
            using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);
            if (archive.Entries.Count is < 2 or > MaximumAssets + 2) throw new InvalidDataException("Invalid backup entry count.");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in archive.Entries)
            {
                if (!ValidName(entry.FullName) || !names.Add(entry.FullName) || entry.Length > EntryLimit(entry.FullName))
                    throw new InvalidDataException("Backup contains an invalid, duplicate or oversized entry.");
            }
            if (archive.Entries.Sum(entry => entry.Length) > MaximumArchiveBytes) throw new InvalidDataException("Expanded backup exceeds the supported size.");
            var manifestEntry = archive.GetEntry("manifest.json") ?? throw new InvalidDataException("Backup manifest is missing.");
            RecoveryManifest manifest;
            await using (var stream = manifestEntry.Open())
            {
                using var bounded = new MemoryStream();
                await CopyBoundedAsync(stream, bounded, manifestEntry.Length, cancellationToken);
                bounded.Position = 0;
                manifest = await JsonSerializer.DeserializeAsync<RecoveryManifest>(bounded, Json, cancellationToken)
                    ?? throw new InvalidDataException("Invalid backup manifest.");
            }
            if (manifest.Format != 1 || manifest.Files is null || manifest.Files.Length != archive.Entries.Count - 1 ||
                manifest.Files.Any(file => file is null || file.Name == "manifest.json" || !ValidName(file.Name) ||
                    file.Length < 0 || file.Length > EntryLimit(file.Name) || !AssetIdentity.IsValid(file.Sha256)) ||
                manifest.Files.Select(file => file.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != manifest.Files.Length)
                throw new InvalidDataException("Invalid backup file declarations.");
            foreach (var file in manifest.Files)
            {
                var entry = archive.GetEntry(file.Name) ?? throw new InvalidDataException("A declared backup file is missing.");
                if (entry.Length != file.Length) throw new InvalidDataException("Backup file length does not match its declaration.");
                var target = Path.Combine(stage.DirectoryPath, file.Name.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await using (var input = entry.Open())
                await using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    await CopyBoundedAsync(input, output, file.Length, cancellationToken);
                var actual = await DescribeAsync(file.Name, target, cancellationToken);
                if (actual != file) throw new InvalidDataException("Backup file integrity check failed.");
            }
            ValidateDatabase(stage.DatabasePath);
            var expected = AssetIds(stage.DatabasePath).Select(id => "assets/" + id).Append("tdsblive.db").ToHashSet(StringComparer.Ordinal);
            if (!expected.SetEquals(manifest.Files.Select(file => file.Name))) throw new InvalidDataException("Backup assets do not match database metadata.");
            foreach (var file in manifest.Files.Where(file => file.Name.StartsWith("assets/", StringComparison.Ordinal)))
                if (file.Sha256 != Path.GetFileName(file.Name)) throw new InvalidDataException("Backup asset content identity is invalid.");
            await ValidateAssetsAsync(stage, cancellationToken);
            // Validate persisted typed configuration before any live-data replacement.
            using var db = Open(stage.DatabasePath, readOnly: true);
            using var command = db.CreateCommand();
            command.CommandText = "SELECT length(CAST(Json AS BLOB)), Json FROM Configurations WHERE Name = 'main'";
            using var configurationReader = command.ExecuteReader();
            if (!configurationReader.Read() || configurationReader.IsDBNull(0) || configurationReader.GetInt64(0) > 65536)
                throw new InvalidDataException("Backup configuration is missing or too large.");
            var configuration = JsonSerializer.Deserialize<ApplicationConfiguration>(configurationReader.GetString(1), Json)
                ?? throw new InvalidDataException("Backup configuration is missing.");
            configuration.Validate();
            stage.Manifest = manifest;
            accepted = true;
            return stage;
        }
        catch (Exception error) when (error is JsonException or ArgumentException or SqliteException)
        {
            throw new InvalidDataException("Backup validation failed.");
        }
        finally { if (!accepted) await stage.DisposeAsync(); }
    }

    internal async Task VerifyPreparedAsync(PreparedRecovery stage, CancellationToken cancellationToken)
    {
        var manifest = stage.Manifest ?? throw new InvalidDataException("Recovery was not prepared from a validated archive.");
        if (IsLink(stage.DirectoryPath)) throw new InvalidDataException("Recovery staging directory is not an ordinary directory.");
        foreach (var entry in Directory.EnumerateFileSystemEntries(stage.DirectoryPath))
        {
            if (IsLink(entry) || Directory.Exists(entry) && Path.GetFileName(entry) != "assets")
                throw new InvalidDataException("Recovery staging directory structure changed.");
            if (Directory.Exists(entry) && Directory.EnumerateFileSystemEntries(entry).Any(child => Directory.Exists(child) || IsLink(child)))
                throw new InvalidDataException("Recovery asset directory structure changed.");
        }
        var expected = manifest.Files.Select(file => file.Name).ToHashSet(StringComparer.Ordinal);
        var actual = Directory.EnumerateFiles(stage.DirectoryPath, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(stage.DirectoryPath, file).Replace(Path.DirectorySeparatorChar, '/')).ToHashSet(StringComparer.Ordinal);
        if (!expected.SetEquals(actual)) throw new InvalidDataException("Recovery staging inventory changed.");
        foreach (var file in manifest.Files)
        {
            var path = Path.Combine(stage.DirectoryPath, file.Name.Replace('/', Path.DirectorySeparatorChar));
            if (IsLink(path) || await DescribeAsync(file.Name, path, cancellationToken) != file)
                throw new InvalidDataException("Recovery staging integrity changed.");
        }
        ValidateDatabase(stage.DatabasePath);
        await ValidateAssetsAsync(stage, cancellationToken);
    }

    private PreparedRecovery NewStaging()
    {
        var parent = Path.GetDirectoryName(paths.Root) ?? throw new InvalidDataException("Recovery requires a data directory below a parent.");
        var directory = Path.Combine(parent, ".tdsblive-recovery-" + Guid.NewGuid().ToString("N"));
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(directory);
        else Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return new(directory);
    }

    private static SqliteConnection Open(string file, bool readOnly)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = file, Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate,
            Pooling = false, ForeignKeys = true, DefaultTimeout = 5
        }.ToString());
        try
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA trusted_schema=OFF;";
            command.ExecuteNonQuery();
            return connection;
        }
        catch { connection.Dispose(); throw; }
    }

    private void ValidateDatabase(string file)
    {
        if (!File.Exists(file)) throw new InvalidDataException("Backup database is missing.");
        using var candidate = Open(file, readOnly: true);
        using var current = Open(paths.Database, readOnly: true);
        if (Schema(candidate) != Schema(current)) throw new InvalidDataException("Backup database schema is incompatible with this installation.");
        using var command = candidate.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        if (command.ExecuteScalar() as string != "ok") throw new InvalidDataException("Backup database integrity check failed.");
        command.CommandText = "PRAGMA foreign_key_check;";
        using var reader = command.ExecuteReader();
        if (reader.Read()) throw new InvalidDataException("Backup database has invalid references.");
    }

    private static string Schema(SqliteConnection db)
    {
        using var command = db.CreateCommand();
        command.CommandText = "SELECT type, name, tbl_name, sql, length(type), length(name), length(tbl_name), length(sql) FROM sqlite_master WHERE name NOT GLOB 'sqlite_*' ORDER BY type, name;";
        using var reader = command.ExecuteReader();
        var text = new StringBuilder();
        var rows = 0;
        while (reader.Read())
        {
            if (++rows > 1024) throw new InvalidDataException("Backup schema exceeds its supported size.");
            for (var index = 0; index < 4; index++)
            {
                if (!reader.IsDBNull(index + 4) && reader.GetInt64(index + 4) > 65536)
                    throw new InvalidDataException("Backup schema declaration exceeds its supported size.");
                var value = reader.IsDBNull(index) ? "" : reader.GetString(index);
                text.Append(value.Replace("\r\n", "\n", StringComparison.Ordinal)).Append('\0');
            }
        }
        return text.ToString();
    }

    private static async Task ValidateAssetsAsync(PreparedRecovery stage, CancellationToken cancellationToken)
    {
        using var db = Open(stage.DatabasePath, readOnly: true);
        using var command = db.CreateCommand();
        command.CommandText = "SELECT Id, length(Json), Json FROM Assets ORDER BY Id;";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetInt64(1) > 16384) throw new InvalidDataException("Backup asset metadata exceeds its supported size.");
            var info = JsonSerializer.Deserialize<AssetInfo>(reader.GetString(2), Json)
                ?? throw new InvalidDataException("Backup asset metadata is invalid.");
            if (info.Id != reader.GetString(0) || info.Hash != info.Id || string.IsNullOrWhiteSpace(info.Filename) ||
                info.Filename.Length > 128 || info.Filename.IndexOfAny(['/', '\\', ':']) >= 0 || info.Filename.Any(char.IsControl) ||
                info.License?.Length > 256 || string.IsNullOrWhiteSpace(info.Mime) || info.Mime != AssetValidation.NormalizeMime(info.Mime) ||
                info.Mime.StartsWith("font/", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(info.License))
                throw new InvalidDataException("Backup asset metadata is invalid.");
            var bytes = await File.ReadAllBytesAsync(Path.Combine(stage.DirectoryPath, "assets", info.Id), cancellationToken);
            if (bytes.LongLength != info.Size || !AssetValidation.Validate(bytes, info.Mime).Bytes.AsSpan().SequenceEqual(bytes))
                throw new InvalidDataException("Backup asset content is invalid.");
        }
    }

    private static string[] AssetIds(string file)
    {
        using var db = Open(file, readOnly: true);
        using var command = db.CreateCommand();
        command.CommandText = "SELECT Id FROM Assets ORDER BY Id LIMIT 10001;";
        using var reader = command.ExecuteReader();
        var ids = new List<string>();
        while (reader.Read())
        {
            var id = reader.GetString(0);
            if (!AssetIdentity.IsValid(id) || ids.Count >= MaximumAssets) throw new InvalidDataException("Invalid backup asset inventory.");
            ids.Add(id);
        }
        return [.. ids];
    }

    private static bool ValidName(string name) => name is "tdsblive.db" or "manifest.json" ||
        name.StartsWith("assets/", StringComparison.Ordinal) && AssetIdentity.IsValid(name[7..]);
    private static long EntryLimit(string name) => name switch
    {
        "tdsblive.db" => MaximumDatabaseBytes,
        "manifest.json" => MaximumManifestBytes,
        _ => AssetValidation.MaximumBytes
    };
    private static bool IsLink(string file) => (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0;

    private static async Task<RecoveryFile> DescribeAsync(string name, string file, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(file);
        if (stream.Length > EntryLimit(name)) throw new InvalidDataException("Backup file exceeds its supported size.");
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
        return new(name, stream.Length, hash);
    }

    private static async Task CopyBoundedAsync(Stream input, Stream output, long expected, CancellationToken cancellationToken)
    {
        var buffer = new byte[65536];
        long total = 0;
        int count;
        while ((count = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += count;
            if (total > expected) throw new InvalidDataException("Backup entry exceeds its declared size.");
            await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
        }
        if (total != expected) throw new InvalidDataException("Backup entry is truncated.");
    }
}
