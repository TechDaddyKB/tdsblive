using ExtensionSuite.Core;
using ExtensionSuite.Data;
using ExtensionSuite.StreamerBot;
using ExtensionSuite.Rumble;
using ExtensionSuite.Finance;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace ExtensionSuite.Host;

public static class FoundationServices
{
    public static void AddFoundation(this WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        // The provider applies the validated application level, including Trace.
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = ExtensionSuite.Overlays.AssetValidation.MaximumBytes);
        builder.Services.AddSingleton<ApplicationPaths>();
        builder.Services.AddSingleton<OverlayStore>();
        builder.Services.AddSingleton<AssetStore>();
        builder.Services.AddSingleton(services => new FoundationStateStore(services.GetRequiredService<ApplicationPaths>().Database));
        builder.Services.AddSingleton<ConfigurationStore>();
        builder.Services.AddSingleton(services => services.GetRequiredService<ConfigurationStore>().Load());
        builder.Services.AddSingleton<ILoggerProvider, RedactedFileLoggerProvider>();
        builder.Services.AddSingleton<AccessControl>();
        builder.Services.AddSingleton<SensitiveValues>();
        if (OperatingSystem.IsWindows()) builder.Services.AddSingleton(services =>
        {
            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
            return new WindowsSecretVault(services.GetRequiredService<ApplicationPaths>().Secrets);
        });
        builder.Services.AddDbContextFactory<FoundationDbContext>((services, options) =>
        {
            var path = services.GetRequiredService<ApplicationPaths>().Database;
            options.UseSqlite(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
                { DataSource = path, ForeignKeys = true }.ToString());
        });
        builder.Services.AddSingleton<EventStore>();
        builder.Services.AddSingleton<FinancialStore>();
        builder.Services.AddSingleton<ValuationRuleStore>();
        builder.Services.AddSingleton<FrankfurterRateProvider>();
        builder.Services.AddSingleton(services => new CachedCurrencyRates(services.GetRequiredService<FrankfurterRateProvider>(),
            services.GetRequiredService<IDbContextFactory<FoundationDbContext>>()));
        builder.Services.AddSingleton<ICurrencyRateProvider>(services => services.GetRequiredService<CachedCurrencyRates>());
        builder.Services.AddSingleton<FinancialProjection>();
        builder.Services.AddSingleton<IIsolatedIntegration, FinancialHostedIntegration>();
        builder.Services.AddOpenApi();
        builder.Services.AddSingleton<EditorEventHub>();
        builder.Services.AddSingleton<IntegrationHealthRegistry>();
        builder.Services.AddSingleton<EventInspectorStore>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<RumbleStore>();
        builder.Services.AddSingleton<IRumbleStore>(services => services.GetRequiredService<RumbleStore>());
        builder.Services.AddSingleton(_ => new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false,
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate }) { Timeout = Timeout.InfiniteTimeSpan });
        builder.Services.AddSingleton(services => new RumbleHttpTransport(services.GetRequiredService<HttpClient>(), services.GetRequiredService<TimeProvider>(),
            services.GetRequiredService<ApplicationConfiguration>().Rumble.RequestTimeoutSeconds));
        builder.Services.AddSingleton<RumbleIntegration>();
        builder.Services.AddSingleton<IIsolatedIntegration>(services => services.GetRequiredService<RumbleIntegration>());
        builder.Services.AddSingleton<IIsolatedIntegration, RumbleTriggerDispatcher>();
        builder.Services.AddSingleton<StreamerBotEventNormalizer>();
        builder.Services.AddSingleton(services => new StreamerBotConnection(services.GetRequiredService<ApplicationConfiguration>().StreamerBot,
            () => services.GetRequiredService<SensitiveValues>().Get("streamerbot-password"), services.GetRequiredService<SensitiveValues>()));
        builder.Services.AddSingleton(services => new SpeakerBotConnection(services.GetRequiredService<ApplicationConfiguration>().SpeakerBot));
        builder.Services.AddSingleton<IIsolatedIntegration, StreamerBotHostedIntegration>();
        builder.Services.AddSingleton<IIsolatedIntegration, SpeakerBotHostedIntegration>();
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
            options.SerializerOptions.RespectNullableAnnotations = true;
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<EventProvenance>(JsonNamingPolicy.CamelCase));
        });
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("admin-login", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions
                    { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
        builder.Services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-TDSBLive-CSRF";
            options.Cookie.Name = "tdsblive-csrf";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.None;
        });
        builder.Services.AddHostedService<DatabaseShutdown>();
        builder.Services.AddHostedService<FoundationLogWriter>();
        builder.Services.AddHostedService<IsolatedIntegrations>();
        builder.Services.AddHostedService<DurableOutboxWorker>();
    }

    public static async Task InitializeFoundationAsync(this WebApplication app)
    {
        var configuration = app.Services.GetRequiredService<ApplicationConfiguration>();
        configuration.Validate();
        var factory = app.Services.GetRequiredService<IDbContextFactory<FoundationDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        await DatabaseLifecycle.InitializeAsync(db);
        await app.Services.GetRequiredService<OverlayStore>().InitializeAsync();
        app.Services.GetRequiredService<ConfigurationStore>().InitializePersistence(configuration);
        if (OperatingSystem.IsWindows())
        {
            var vault = app.Services.GetRequiredService<WindowsSecretVault>();
            var sensitive = app.Services.GetRequiredService<SensitiveValues>();
            foreach (var name in vault.Names())
            {
                var value = await vault.GetAsync(name);
                if (value is not null) sensitive.Set(name, value);
            }
            var admin = await vault.GetAsync("admin-token");
            if (admin is not null) app.Services.GetRequiredService<AccessControl>().SetAdminCredential(admin);
        }
        if (configuration.Server.EnableLan && !app.Services.GetRequiredService<AccessControl>().HasAdminCredential)
            throw new InvalidOperationException("LAN access requires a generated admin credential. Provision it on loopback before enabling LAN.");
        var address = configuration.Server.Host.Contains(':', StringComparison.Ordinal) ? $"[{configuration.Server.Host}]" : configuration.Server.Host;
        app.Urls.Clear();
        app.Urls.Add($"http://{address}:{configuration.Server.Port}");
    }
}

public sealed class DatabaseShutdown(ApplicationPaths paths) : IHostedService
{
    private int stopped;
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref stopped, 1) != 0) return;
        // The final checkpoint must not resolve EF logging services from a provider
        // that a concurrent host-disposal path may already have disposed.
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            { DataSource = paths.Database, ForeignKeys = true }.ToString());
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
