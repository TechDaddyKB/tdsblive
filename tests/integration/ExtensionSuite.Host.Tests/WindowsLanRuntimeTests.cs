using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
using ExtensionSuite.Core;
using ExtensionSuite.Data;
using ExtensionSuite.Host;
using Xunit;

namespace ExtensionSuite.Host.Tests;

[SupportedOSPlatform("windows")]
public sealed class WindowsLanRuntimeTests
{
    [WindowsFact]
    public async Task RealNonLoopbackHttpRequiresDpapiBackedAuthentication()
    {
        var address = NetworkInterface.GetAllNetworkInterfaces().Where(adapter => adapter.OperationalStatus == OperationalStatus.Up)
            .SelectMany(adapter => adapter.GetIPProperties().UnicastAddresses).Select(item => item.Address)
            .First(ip => ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var directory = Path.Combine(Path.GetTempPath(), "tdsblive-lan-runtime-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        var credential = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await new WindowsSecretVault(Path.Combine(directory, "credentials")).SetAsync("admin-token", credential);
        var configuration = new ApplicationConfiguration { Server = new ServerConfiguration
            { Host = "0.0.0.0", Port = port, EnableLan = true, AllowedHosts = [address.ToString()] } };
        await File.WriteAllTextAsync(Path.Combine(directory, "configuration.json"), JsonSerializer.Serialize(configuration, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        start.ArgumentList.Add(typeof(Program).Assembly.Location);
        start.ArgumentList.Add("--TDSBLive:DataDirectory");
        start.ArgumentList.Add(directory);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            using var client = new HttpClient(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false })
                { BaseAddress = new Uri($"http://{address}:{port}") };
            await WaitReadyAsync(process, client, timeout.Token);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/status", timeout.Token)).StatusCode);
            Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/editor", timeout.Token)).StatusCode);
            client.DefaultRequestHeaders.Authorization = new("Bearer", credential);
            using var response = await client.GetAsync("/api/status", timeout.Token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Null(response.Headers.Location);
            using var status = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
            Assert.True(status.RootElement.GetProperty("lanEnabled").GetBoolean());
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync("/api/configuration", configuration, timeout.Token)).StatusCode);
            using var csrf = JsonDocument.Parse(await client.GetStringAsync("/api/auth/csrf", timeout.Token));
            client.DefaultRequestHeaders.Add("X-TDSBLive-CSRF", csrf.RootElement.GetProperty("requestToken").GetString());
            client.DefaultRequestHeaders.Authorization = null;
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/login", new AdminLogin(credential), timeout.Token)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/status", timeout.Token)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/auth/provision", null, timeout.Token)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null, timeout.Token)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/status", timeout.Token)).StatusCode);
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            await Task.WhenAll(output, errors); // Drain privately; never expose credential-bearing child output.
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            await DeleteRuntimeDirectoryAsync(directory);
        }
    }

    private static async Task DeleteRuntimeDirectoryAsync(string directory)
    {
        // Windows can briefly retain a killed child's SQLite sidecar while releasing handles/scanning files.
        // Retry only cleanup, after process exit; assertions and persistent cleanup failures still fail the test.
        for (var attempt = 0; ; attempt++)
        {
            try { Directory.Delete(directory, recursive: true); return; }
            catch (Exception error) when (attempt < 20 && (error is IOException or UnauthorizedAccessException))
            { await Task.Delay(100); }
        }
    }

    private static async Task WaitReadyAsync(Process process, HttpClient client, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (process.HasExited) throw new InvalidOperationException("Isolated LAN host exited before readiness.");
            try
            {
                using var response = await client.GetAsync("/api/status", cancellationToken);
                if (response.StatusCode == HttpStatusCode.Unauthorized) return;
            }
            catch (HttpRequestException) { }
            await Task.Delay(100, cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
    }
}
