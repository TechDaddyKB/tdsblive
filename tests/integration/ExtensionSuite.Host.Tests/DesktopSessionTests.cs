using ExtensionSuite.Core;
using ExtensionSuite.DesktopControl;
using ExtensionSuite.Host;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class DesktopSessionTests
{
    [Theory]
    [InlineData("input")]
    [InlineData("output")]
    public async Task ExistingProfileAcknowledgementIsBoundedAndReturnsNoCapability(string mode)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TDSBLive:DesktopMode"] = "external", ["TDSBLive:DesktopBootstrap"] = mode
        }).Build();
        using var input = new MemoryStream();
        var token = DesktopProtocol.NewSessionToken();
        if (mode == "input")
        {
            await DesktopProtocol.WriteAsync(input, new DesktopBootstrap(0, token), CancellationToken.None);
            input.Position = 0;
        }
        using var output = new StringWriter();
        await DesktopSession.AcknowledgeExistingProfileAsync(configuration, input, mode == "input", output, true);
        Assert.Equal(DesktopProtocol.AlreadyRunningMarker + Environment.NewLine, output.ToString());
        Assert.DoesNotContain(token, output.ToString());
        Assert.Equal(input.Length, input.Position);
    }

    [Theory]
    [InlineData("input")]
    [InlineData("output")]
    [InlineData("invalid")]
    public async Task ExistingProfileAcknowledgementRejectsInvalidLauncherPipes(string mode)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TDSBLive:DesktopMode"] = "external", ["TDSBLive:DesktopBootstrap"] = mode
        }).Build();
        using var input = new MemoryStream();
        using var output = new StringWriter();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            DesktopSession.AcknowledgeExistingProfileAsync(configuration, input, false, output, false));
        Assert.Empty(output.ToString());
    }

    [Fact]
    public async Task OrdinaryDuplicateLaunchDoesNotReadOrWriteLauncherFrames()
    {
        using var input = new MemoryStream();
        using var output = new StringWriter();
        await DesktopSession.AcknowledgeExistingProfileAsync(Configuration("automatic"), input, false, output, false);
        Assert.Empty(output.ToString());
    }

    [Theory]
    [InlineData(false, "restart", true, "failed")]
    [InlineData(false, "restore", true, "failed")]
    [InlineData(true, "restart", true, "restart-ready")]
    [InlineData(true, "restore", true, "restart-ready")]
    [InlineData(true, "restart", false, "relaunched")]
    [InlineData(true, "quit", true, "quit")]
    [InlineData(true, null, true, "stopped")]
    public void RelaunchRequiresSuccessfulShutdownAndExplicitRestartIntent(bool safe, string? operation, bool external, string expected) =>
        Assert.Equal(expected, HostApplicationRunner.CompletionState(safe, operation, external));

    private static IConfiguration Configuration(string? mode, bool? open = null) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TDSBLive:DesktopMode"] = mode,
            ["TDSBLive:OpenEditor"] = open?.ToString()
        }).Build();

    private static WebApplication App(ServerConfiguration? server = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Services.AddSingleton(new ApplicationConfiguration { Server = server ?? new() });
        builder.Services.AddSingleton<IntegrationHealthRegistry>();
        builder.Services.AddSingleton<ApplicationLifecycle>();
        builder.Services.AddSingleton(TimeProvider.System);
        return builder.Build();
    }

    [Theory]
    [InlineData("external", true)]
    [InlineData("off", false)]
    public void ExplicitDesktopModeDeterminesOwnership(string mode, bool expected) =>
        Assert.Equal(expected, DesktopSession.IsEnabled(Configuration(mode)));

    [Fact]
    public async Task HeadlessAndUnpackagedModesDoNotReadAnExternalCapability()
    {
        await using var app = App();
        using var input = new MemoryStream();
        Assert.Null(await DesktopSession.StartAsync(app, Configuration("off"), input, false));
        // Unit/integration hosts have no packaged companion beside them.
        Assert.False(DesktopSession.IsEnabled(Configuration("automatic")));
        Assert.False(DesktopSession.IsEnabled(Configuration(null)));
        Assert.Null(await DesktopSession.StartAsync(app, Configuration("automatic"), input, false));
    }

    [Fact]
    public async Task InvalidModeOrNonPipeLaunchFailsBeforeStartingAControlServer()
    {
        await using var app = App();
        using var input = new MemoryStream();
        Assert.Throws<ArgumentException>(() => DesktopSession.IsEnabled(Configuration("invalid")));
        await Assert.ThrowsAsync<ArgumentException>(() => DesktopSession.StartAsync(app, Configuration("invalid"), input, true));
        await Assert.ThrowsAsync<ArgumentException>(() => DesktopSession.StartAsync(app, Configuration("external"), input, false));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExternalBootstrapRequiresAFreshCapabilityAndNoBorrowedPort(bool wrongPort)
    {
        await using var app = App();
        using var input = new MemoryStream();
        var bootstrap = new DesktopBootstrap(wrongPort ? 1234 : 0, wrongPort ? DesktopProtocol.NewSessionToken() : "invalid");
        await DesktopProtocol.WriteAsync(input, bootstrap, CancellationToken.None);
        input.Position = 0;
        await Assert.ThrowsAsync<ArgumentException>(() => DesktopSession.StartAsync(app, Configuration("external"), input, true));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task ExternalSessionKeepsEditorHandoffLocalAndShutdownOutcomeExplicit(bool? preference, bool opens)
    {
        var saved = new ServerConfiguration { Host = "192.0.2.23", Port = 23456, EnableLan = true };
        await using var app = App(saved);
        using var input = new MemoryStream();
        var token = DesktopProtocol.NewSessionToken();
        await DesktopProtocol.WriteAsync(input, new DesktopBootstrap(0, token), CancellationToken.None);
        input.Position = 0;
        await using var session = await DesktopSession.StartAsync(app, Configuration("external", preference), input, true);
        Assert.NotNull(session);
        Assert.True(session.External);
        Assert.Equal(opens, session.OpenEditor);
        Assert.Equal("127.0.0.1", session.EditorServer.Host);
        Assert.Equal("192.0.2.23", saved.Host);
        Assert.Contains("http://127.0.0.1:23456", app.Urls);
        var health = app.Services.GetRequiredService<IntegrationHealthRegistry>();
        Assert.Equal("starting", health.Snapshot()["Desktop controls"].State);
        var connection = new DesktopBootstrap(session.ControlPort, token);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var initial = await DesktopProtocol.SendAsync(connection, "status", deadline.Token);
        Assert.Equal("starting", initial.State);
        Assert.Equal("http://127.0.0.1:23456/editor", initial.EditorUrl);
        session.SetReady();
        session.RequestOpen();
        var running = await DesktopProtocol.SendAsync(connection, "status", deadline.Token);
        Assert.Equal("running", running.State);
        Assert.Equal(1, running.OpenRequests);
        var until = DateTime.UtcNow.AddSeconds(5);
        while (health.Snapshot()["Desktop controls"].State != "running" && DateTime.UtcNow < until) await Task.Delay(20);
        Assert.Equal("running", health.Snapshot()["Desktop controls"].State);
        await session.StopMonitoringAsync();
        Assert.Equal("stopped", health.Snapshot()["Desktop controls"].State);
        var completed = DesktopProtocol.SendAsync(connection, "wait", deadline.Token);
        await session.CompleteAsync("failed");
        Assert.Equal("failed", (await completed).State);
        Assert.Null(app.Services.GetRequiredService<ApplicationLifecycle>().Operation);
    }

    [Fact]
    public async Task MissingCompanionHeartbeatsDegradeDiagnosticsWithoutStoppingTheHost()
    {
        await using var app = App();
        using var input = new MemoryStream();
        await DesktopProtocol.WriteAsync(input, new DesktopBootstrap(0, DesktopProtocol.NewSessionToken()), CancellationToken.None);
        input.Position = 0;
        await using var session = await DesktopSession.StartAsync(app, Configuration("external"), input, true);
        Assert.NotNull(session);
        var health = app.Services.GetRequiredService<IntegrationHealthRegistry>();
        var until = DateTime.UtcNow.AddSeconds(18);
        while (health.Snapshot()["Desktop controls"].State != "degraded" && DateTime.UtcNow < until) await Task.Delay(20);
        Assert.Equal("degraded", health.Snapshot()["Desktop controls"].State);
        Assert.Null(app.Services.GetRequiredService<ApplicationLifecycle>().Operation);
    }

    [Fact]
    public async Task OutputBootstrapWorksWithoutInputAndAuthenticatesOnlyTheFreshPrivateCapability()
    {
        await using var app = App();
        var configuration = Configuration("external");
        configuration["TDSBLive:DesktopBootstrap"] = "output";
        using var input = new MemoryStream(); // Models UMU's empty input stream.
        using var output = new StringWriter();
        await using var session = await DesktopSession.StartAsync(app, configuration, input, false, output, true);
        Assert.NotNull(session);
        var frame = output.ToString().TrimEnd();
        Assert.StartsWith(DesktopProtocol.BootstrapPrefix, frame);
        var bootstrap = JsonSerializer.Deserialize<DesktopBootstrap>(frame[DesktopProtocol.BootstrapPrefix.Length..])!;
        Assert.True(DesktopProtocol.ValidSessionToken(bootstrap.SessionToken));
        Assert.Equal(session.ControlPort, bootstrap.Port);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Assert.Equal("starting", (await DesktopProtocol.SendAsync(bootstrap, "status", deadline.Token)).State);
        await Assert.ThrowsAnyAsync<IOException>(() => DesktopProtocol.SendAsync(
            new DesktopBootstrap(bootstrap.Port, DesktopProtocol.NewSessionToken()), "status", deadline.Token));
        Assert.Null(configuration["TDSBLive:SessionToken"]);
    }

    [Theory]
    [InlineData(2, true, true, 0, 0x22, true)] // Wine's forwarded Unix pipe.
    [InlineData(2, false, true, 0, 0x22, false)] // Terminal.
    [InlineData(1, true, true, 0, 0x08, false)] // Regular file.
    [InlineData(2, true, true, 0, 0x15, false)] // /dev/null.
    [InlineData(2, true, true, 0, 0x50, false)] // Console device.
    [InlineData(2, true, false, 0, 0x22, false)] // No Wine compatibility path on Windows.
    [InlineData(2, true, true, -1, 0x22, false)] // Failed device query.
    [InlineData(0, true, true, 0, 0x22, false)] // Invalid handle.
    public void WinePipeCompatibilityRejectsFilesTerminalsNullAndFailedQueries(uint type, bool redirected,
        bool wine, int status, uint device, bool expected)
        => Assert.Equal(expected, DesktopSession.IsWineOutputPipe(type, redirected, wine, status, device));

    [Theory]
    [InlineData("external", "output", false)]
    [InlineData("automatic", "output", true)]
    [InlineData("external", "unknown", true)]
    public async Task UnsafeOutputBootstrapModesFailBeforeEmittingAnyCapability(string mode, string bootstrapMode, bool pipe)
    {
        await using var app = App();
        var configuration = Configuration(mode);
        configuration["TDSBLive:DesktopBootstrap"] = bootstrapMode;
        using var input = new MemoryStream();
        using var output = new StringWriter();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            DesktopSession.StartAsync(app, configuration, input, false, output, pipe));
        Assert.Empty(output.ToString());
    }
}
