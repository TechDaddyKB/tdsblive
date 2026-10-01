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
        base.Dispose(disposing);
        if (disposing)
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, recursive: true);
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
