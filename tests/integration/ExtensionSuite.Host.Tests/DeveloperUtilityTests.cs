using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ExtensionSuite.Host;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class DeveloperUtilityTests(FoundationHostFactory factory) : IClassFixture<FoundationHostFactory>
{
    [Theory]
    [InlineData(false, "rumble-replay")]
    [InlineData(true, "rumble-replay")]
    [InlineData(false, "sanitize-logs")]
    [InlineData(true, "sanitize-logs")]
    public async Task ActualScannerApprovedPlainAndExpandedInputRunsWithoutStartingHost(bool gzip, string command)
    {
        var path = Path.Combine(Path.GetTempPath(), "tdsblive-owned-utility-" + Guid.NewGuid() + (gzip ? ".jsonl.gz" : ".jsonl"));
        var bytes = Encoding.UTF8.GetBytes("""{"observed_at":"2026-01-01T00:00:00Z","payload":{},"outcome":"ok"}""");
        try
        {
            if (gzip)
            {
                await using var file = File.Create(path);
                await using var compressed = new System.IO.Compression.GZipStream(file, System.IO.Compression.CompressionMode.Compress);
                await compressed.WriteAsync(bytes);
            }
            else await File.WriteAllBytesAsync(path, bytes);
            Assert.Equal(0, await DeveloperCommands.RunAsync(["tools", command, path]));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task UtilityRejectsInvalidCommandAndUnreadableInputWithoutPrintingSourceValues()
    {
        Assert.Null(await DeveloperCommands.RunAsync([]));
        Assert.Equal(2, await DeveloperCommands.RunAsync(["tools"]));
        Assert.Equal(2, await DeveloperCommands.RunAsync(["tools", "unknown", "unused"]));
        Assert.Equal(1, await DeveloperCommands.RunAsync(["tools", "rumble-replay", Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())]));
    }

    [Theory]
    [InlineData("{\"observed_at\":5}")]
    [InlineData("{\"observed_at\":\"owned-invalid-date\"}")]
    [InlineData("[]")]
    public async Task MalformedApprovedInputReturnsSafeFailureInsteadOfUnhandledParserException(string input)
    {
        var path = Path.Combine(Path.GetTempPath(), "tdsblive-owned-invalid-" + Guid.NewGuid() + ".jsonl");
        try
        {
            await File.WriteAllTextAsync(path, input);
            Assert.Equal(1, await DeveloperCommands.RunAsync(["tools", "rumble-replay", path]));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ReplayRejectsInvalidMissingNonUtcAndOversizedRows()
    {
        foreach (var input in new[] { "", "null", "{}", "{\"observedAt\":\"2026-01-01T00:00:00+01:00\"}", "{}\n" + new string(' ', 2 * 1024 * 1024 + 1), string.Concat(Enumerable.Repeat("{}\n", 100001)) })
            Assert.ThrowsAny<Exception>(() => DeveloperCommands.Replay(Encoding.UTF8.GetBytes(input)));
        var canonical = """{"observedAt":"2026-01-01T00:00:00Z","outcome":"timeout"}""";
        Assert.Contains("\"polls\":1", JsonSerializer.Serialize(DeveloperCommands.Replay(Encoding.UTF8.GetBytes(canonical))));
    }

    [Fact]
    public void OfflineReplayUsesActualEngineAndRejectsReverseChronology()
    {
        var rows = """
        {"observed_at":"2026-01-01T00:00:00Z","payload":{},"outcome":"ok"}
        {"observed_at":"2026-01-01T00:00:07Z","payload":{},"outcome":"ok"}
        """;
        var result = JsonSerializer.Serialize(DeveloperCommands.Replay(Encoding.UTF8.GetBytes(rows)));
        Assert.Contains("\"polls\":2", result); Assert.Contains("\"liveActionsAllowed\":false", result);
        Assert.Throws<InvalidDataException>(() => DeveloperCommands.Replay(Encoding.UTF8.GetBytes(string.Join('\n', rows.Split('\n').Reverse()))));
    }

    [Fact]
    public async Task DiagnosticExportContainsOnlyAggregateCountsAndNoConfigurationOrUserData()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://127.0.0.1") });
        var content = await client.GetStringAsync("/api/diagnostics/export");
        var result = JsonNode.Parse(content)!;
        Assert.False(result["includesUserData"]!.GetValue<bool>());
        Assert.False(result["includesConfiguration"]!.GetValue<bool>());
        Assert.False(result["includesLogs"]!.GetValue<bool>());
        Assert.True(result["database"]!["events"]!.GetValue<int>() >= 0);
        Assert.DoesNotContain(factory.DirectoryPath, content);
    }
}
