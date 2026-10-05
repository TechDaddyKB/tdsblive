using System.Text.Json;

namespace ExtensionSuite.Desktop;

public sealed class LinuxLauncherSettingsStore(string directory)
{
    private const int MaximumBytes = 16 * 1024;
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public string DirectoryPath { get; } = Path.GetFullPath(directory);
    public string FilePath => Path.Combine(DirectoryPath, "launcher.json");
    private sealed record Document(int Version, LinuxLauncherSettings Settings);

    public LinuxLauncherSettings? Load()
    {
        if (!File.Exists(FilePath)) return null;
        if (new FileInfo(FilePath).LinkTarget is not null)
            throw new InvalidDataException("Launcher settings cannot be a link to another file. Keep your existing setup and choose its Windows settings folder again.");
        using var file = File.OpenRead(FilePath);
        var bytes = new byte[MaximumBytes + 1];
        var count = 0;
        while (count < bytes.Length)
        {
            var read = file.Read(bytes, count, bytes.Length - count);
            if (read == 0) break;
            count += read;
        }
        if (count > MaximumBytes) throw new InvalidDataException("Launcher settings are too large. Your Windows app settings have not been changed.");
        var document = JsonSerializer.Deserialize<Document>(bytes.AsSpan(0, count), Options);
        if (document is not { Version: 1, Settings: not null })
            throw new InvalidDataException("These launcher settings need a different application version. Keep the file and your existing Windows settings folder.");
        return document.Settings.Normalize();
    }

    public void Save(LinuxLauncherSettings settings)
    {
        var normalized = settings.Normalize();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new Document(1, normalized), Options);
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("Launcher settings are too large.");
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(DirectoryPath);
        else Directory.CreateDirectory(DirectoryPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var temporary = Path.Combine(DirectoryPath, ".launcher-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write,
                Share = FileShare.None, Options = FileOptions.WriteThrough };
            if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            using (var file = new FileStream(temporary, options))
            {
                file.Write(bytes);
                file.Flush(flushToDisk: true);
            }
            File.Move(temporary, FilePath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
