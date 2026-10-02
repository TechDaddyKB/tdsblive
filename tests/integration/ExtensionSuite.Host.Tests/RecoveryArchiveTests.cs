using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ExtensionSuite.Host;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class RecoveryArchiveTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly byte[] Gif = Convert.FromHexString("47494638396101000100800000ff00000000ff21ff0b4e45545343415045322e30030100000021f904000a0000002c000000000100010000020244010021f904000a0000002c00000000010001000002024c01003b");

    [Fact]
    public async Task OnlineBackupIncludesCommittedWalAndAssetsButExcludesCredentialAndLogFiles()
    {
        using var host = new FoundationHostFactory();
        using var client = host.CreateClient();
        var paths = host.Services.GetRequiredService<ApplicationPaths>();
        var asset = await host.Services.GetRequiredService<AssetStore>().UploadAsync(new MemoryStream(Gif), "example.gif", "image/gif", null, default);
        Assert.NotNull(asset);
        Directory.CreateDirectory(paths.Secrets);
        await File.WriteAllTextAsync(Path.Combine(paths.Secrets, "owned-marker"), "not part of a downloadable backup");
        await File.WriteAllTextAsync(Path.Combine(paths.Root, "owned-notes.txt"), "not application state");
        Execute(paths.Database, "INSERT INTO Configurations (Name, Json) VALUES ('owned-wal-marker', '{}');");
        var recovery = new RecoveryArchive(paths);
        using var bytes = new MemoryStream();
        await recovery.CreateAsync(bytes);
        bytes.Position = 0;
        using (var zip = new ZipArchive(bytes, ZipArchiveMode.Read, leaveOpen: true))
            Assert.Equal(new[] { "assets/" + asset.Id, "manifest.json", "tdsblive.db" }, zip.Entries.Select(entry => entry.FullName).Order().ToArray());
        bytes.Position = 0;
        var prepared = await recovery.PrepareAsync(bytes);
        var stage = prepared.DirectoryPath;
        Assert.True(Directory.Exists(stage));
        Assert.Equal(Gif, await File.ReadAllBytesAsync(Path.Combine(stage, "assets", asset.Id)));
        using (var connection = Open(prepared.DatabasePath))
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT COUNT(*) FROM Configurations WHERE Name = 'owned-wal-marker'";
            Assert.Equal(1L, command.ExecuteScalar());
        }
        await prepared.DisposeAsync();
        Assert.False(Directory.Exists(stage));
        Assert.True(File.Exists(Path.Combine(paths.Secrets, "owned-marker")));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("/absolute")]
    [InlineData("credentials/value")]
    [InlineData("assets/../value")]
    [InlineData("ASSETS/value")]
    public async Task UnrecognizedPathsAreRejectedBeforeExtraction(string name)
    {
        using var host = new FoundationHostFactory();
        using var client = host.CreateClient();
        using var bytes = new MemoryStream();
        using (var zip = new ZipArchive(bytes, ZipArchiveMode.Create, leaveOpen: true))
        {
            await WriteAsync(zip, "manifest.json", "{}"u8.ToArray());
            await WriteAsync(zip, name, "owned"u8.ToArray());
        }
        bytes.Position = 0;
        await Assert.ThrowsAsync<InvalidDataException>(() => new RecoveryArchive(host.Services.GetRequiredService<ApplicationPaths>()).PrepareAsync(bytes));
    }

    [Theory]
    [InlineData("checksum")]
    [InlineData("schema")]
    [InlineData("schemaPrefix")]
    [InlineData("configuration")]
    [InlineData("assetMetadata")]
    [InlineData("missingAsset")]
    [InlineData("assetIdentity")]
    [InlineData("manifestFormat")]
    public async Task TamperedOrIncompatibleBackupCannotChangeLiveData(string change)
    {
        using var host = new FoundationHostFactory();
        using var client = host.CreateClient();
        var paths = host.Services.GetRequiredService<ApplicationPaths>();
        var asset = await host.Services.GetRequiredService<AssetStore>().UploadAsync(new MemoryStream(Gif), "example.gif", "image/gif", null, default);
        var recovery = new RecoveryArchive(paths);
        using var original = new MemoryStream();
        await recovery.CreateAsync(original);
        original.Position = 0;
        var files = new Dictionary<string, byte[]>();
        using (var zip = new ZipArchive(original, ZipArchiveMode.Read, leaveOpen: true))
            foreach (var entry in zip.Entries.Where(entry => entry.FullName != "manifest.json"))
            {
                await using var input = entry.Open();
                using var output = new MemoryStream();
                await input.CopyToAsync(output);
                files[entry.FullName] = output.ToArray();
            }
        if (change is "schema" or "schemaPrefix" or "configuration" or "assetMetadata")
        {
            var modified = Path.Combine(paths.Root, "owned-modified.db");
            await File.WriteAllBytesAsync(modified, files["tdsblive.db"]);
            Execute(modified, change switch
            {
                "schema" => "CREATE TABLE Unexpected (Value TEXT);",
                "schemaPrefix" => "CREATE TABLE sqliteXUnexpected (Value TEXT);",
                "configuration" => "UPDATE Configurations SET Json = '{\"displayName\":\"\"}' WHERE Name = 'main';",
                _ => "UPDATE Assets SET Json = '{}';"
            });
            files["tdsblive.db"] = await File.ReadAllBytesAsync(modified);
        }
        if (change == "missingAsset") files.Remove("assets/" + asset!.Id);
        if (change == "assetIdentity") files["assets/" + asset!.Id] = Encoding.ASCII.GetBytes("GIF89a-invalid-owned-content");
        var declared = files.Select(file => new RecoveryFile(file.Key, file.Value.LongLength,
            Convert.ToHexString(SHA256.HashData(file.Value)).ToLowerInvariant())).ToArray();
        if (change == "checksum") declared[0] = declared[0] with { Sha256 = new string('0', 64) };
        using var altered = new MemoryStream();
        using (var zip = new ZipArchive(altered, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in files) await WriteAsync(zip, file.Key, file.Value);
            await WriteAsync(zip, "manifest.json", JsonSerializer.SerializeToUtf8Bytes(new RecoveryManifest(change == "manifestFormat" ? 2 : 1,
                DateTimeOffset.UtcNow, declared), Json));
        }
        altered.Position = 0;
        await Assert.ThrowsAsync<InvalidDataException>(() => recovery.PrepareAsync(altered));
        Assert.NotNull(await host.Services.GetRequiredService<AssetStore>().GetAsync(asset!.Id, default));
        Assert.Equal(Gif, await File.ReadAllBytesAsync(Path.Combine(paths.Root, "assets", asset.Id)));
    }

    private static async Task WriteAsync(ZipArchive archive, string name, byte[] bytes)
    {
        await using var output = archive.CreateEntry(name).Open();
        await output.WriteAsync(bytes);
    }
    private static SqliteConnection Open(string file)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = file, Pooling = false }.ToString());
        connection.Open();
        return connection;
    }
    private static void Execute(string file, string sql)
    {
        using var connection = Open(file);
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
