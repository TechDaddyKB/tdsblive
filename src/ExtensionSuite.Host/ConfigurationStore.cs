using System.Text.Json;
using System.Text.Json.Serialization;
using ExtensionSuite.Core;

namespace ExtensionSuite.Host;

public sealed class ApplicationPaths
{
    public string Root { get; }
    public string Database => Path.Combine(Root, "tdsblive.db");
    public string Configuration => Path.Combine(Root, "configuration.json");
    public string Secrets => Path.Combine(Root, "credentials");
    public string Logs => Path.Combine(Root, "logs");

    public ApplicationPaths(IConfiguration configuration)
    {
        Root = Path.GetFullPath(configuration["TDSBLive:DataDirectory"] ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TDSBLive"));
        Directory.CreateDirectory(Root);
    }
}

public sealed class ConfigurationStore(ApplicationPaths paths)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectNullableAnnotations = true
    };

    public ApplicationConfiguration Load()
    {
        if (!File.Exists(paths.Configuration)) return new ApplicationConfiguration();
        try
        {
            var config = JsonSerializer.Deserialize<ApplicationConfiguration>(File.ReadAllText(paths.Configuration), Options)
                ?? throw new JsonException();
            config.Validate();
            return config;
        }
        catch (Exception error) when (error is JsonException or ArgumentException)
        {
            throw new InvalidOperationException("Configuration is invalid. Check the typed configuration fields; credentials belong in the secret vault.");
        }
    }

    public async Task SaveAsync(ApplicationConfiguration configuration, CancellationToken cancellationToken)
    {
        configuration.Validate();
        await gate.WaitAsync(cancellationToken);
        try
        {
            var pending = paths.Configuration + ".pending";
            await File.WriteAllTextAsync(pending, JsonSerializer.Serialize(configuration, Options), cancellationToken);
            File.Move(pending, paths.Configuration, overwrite: true);
        }
        finally { gate.Release(); }
    }
}
