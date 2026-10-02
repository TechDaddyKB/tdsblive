using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using ExtensionSuite.Core;
using ExtensionSuite.Host;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ExtensionSuite.Host.Tests;

public sealed class FoundationHostFactory : WebApplicationFactory<Program>
{
    private int cleanupStarted;
    public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "tdsblive-host-tests", Guid.NewGuid().ToString());
    public string AdminCredential { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public bool RemotePeer { get; }

    public FoundationHostFactory() : this(false) { }

    internal FoundationHostFactory(bool remotePeer)
    {
        RemotePeer = remotePeer;
        Directory.CreateDirectory(DirectoryPath);
        if (remotePeer) File.WriteAllText(Path.Combine(DirectoryPath, "configuration.json"), JsonSerializer.Serialize(
            new ApplicationConfiguration { Server = new ServerConfiguration { EnableLan = true } }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("TDSBLive:DataDirectory", DirectoryPath);
        if (RemotePeer) builder.ConfigureServices(services =>
        {
            var access = new AccessControl();
            access.SetAdminCredential(AdminCredential);
            services.AddSingleton(access);
            services.AddSingleton<IStartupFilter, RemotePeerFilter>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        // WebApplicationFactory.Dispose can dispatch through DisposeAsync back into this override.
        // Only the outer call may remove the database, after the complete host shutdown.
        var cleanDirectory = disposing && Interlocked.CompareExchange(ref cleanupStarted, 1, 0) == 0;
        base.Dispose(disposing);
        if (cleanDirectory)
        {
            // Other test hosts run concurrently. Global pool clearing can invalidate their live log connections.
            var database = Path.Combine(DirectoryPath, "tdsblive.db");
            foreach (var timeout in new int?[] { null, 2 })
            {
                var options = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = database, ForeignKeys = true };
                if (timeout is { } seconds) options.DefaultTimeout = seconds;
                using var connection = new Microsoft.Data.Sqlite.SqliteConnection(options.ToString());
                Microsoft.Data.Sqlite.SqliteConnection.ClearPool(connection);
            }
            DeleteTemporaryDirectory(DirectoryPath);
        }
    }

    internal static void DeleteTemporaryDirectory(string directory)
    {
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        while (Directory.Exists(directory))
        {
            try { Directory.Delete(directory, recursive: true); return; }
            catch (IOException) when (OperatingSystem.IsWindows() && elapsed.Elapsed < TimeSpan.FromSeconds(2))
            {
                // Windows may temporarily retain a file handle after shutdown. Persistent locks still fail.
                Thread.Sleep(25);
            }
        }
    }

    private sealed class RemotePeerFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, continuation) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
                await continuation();
            });
            next(app);
        };
    }
}
