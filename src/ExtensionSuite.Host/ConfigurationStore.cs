using System.Text.Json;
using System.Text.Json.Serialization;
using ExtensionSuite.Core;
using ExtensionSuite.Data;

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

public sealed class ConfigurationStore(ApplicationPaths paths, FoundationStateStore? state = null)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectNullableAnnotations = true
    };

    public ApplicationConfiguration Load()
    {
        var persisted = state?.ReadConfiguration();
        if (persisted is null && !File.Exists(paths.Configuration)) return new ApplicationConfiguration();
        try
        {
            var config = JsonSerializer.Deserialize<ApplicationConfiguration>(persisted ?? File.ReadAllText(paths.Configuration), Options)
                ?? throw new JsonException();
            config.Validate();
            return config;
        }
        catch (Exception error) when (error is JsonException or ArgumentException)
        {
            throw new InvalidOperationException("Configuration is invalid. Check the typed configuration fields; credentials belong in the secret vault.");
        }
    }

    public void InitializePersistence(ApplicationConfiguration configuration) => state?.Initialize(JsonSerializer.Serialize(configuration, Options));

    public async Task SaveAsync(ApplicationConfiguration configuration, CancellationToken cancellationToken)
    {
        configuration.Validate();
        await gate.WaitAsync(cancellationToken);
        try
        {
            var pending = paths.Configuration + ".pending";
            await File.WriteAllTextAsync(pending, JsonSerializer.Serialize(configuration, Options), cancellationToken);
            void SaveFallback() => File.Move(pending, paths.Configuration, overwrite: true);
            if (state is null) SaveFallback();
            else state.SaveConfiguration(JsonSerializer.Serialize(configuration, Options), SaveFallback);
        }
        finally { gate.Release(); }
    }
}
