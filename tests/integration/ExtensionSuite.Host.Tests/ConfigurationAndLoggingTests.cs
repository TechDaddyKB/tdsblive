using System.Text.Json;
using ExtensionSuite.Core;
using ExtensionSuite.Host;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class ConfigurationAndLoggingTests
{
    [Fact]
    public void LogIoFailureDoesNotInterruptApplicationExecution()
    {
        var root = Path.Combine(Path.GetTempPath(), "tdsblive-log-tests", Guid.NewGuid().ToString());
        var paths = new ApplicationPaths(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["TDSBLive:DataDirectory"] = root }).Build());
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(paths.Logs, "blocked-directory");
            using var provider = new RedactedFileLoggerProvider(paths, new ApplicationConfiguration { MinimumLogLevel = "Trace" }, new SensitiveValues());
            var logger = provider.CreateLogger("tests");
            Assert.True(logger.IsEnabled(LogLevel.Trace));
            logger.LogTrace("Synthetic diagnostic");
            Assert.Equal(1, provider.WriteFailures);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ConfigurationRejectsUnknownSecretFieldsAndLeavesPreviousFileOnValidationFailure()
    {
        var root = Path.Combine(Path.GetTempPath(), "tdsblive-config-tests", Guid.NewGuid().ToString());
        var paths = new ApplicationPaths(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["TDSBLive:DataDirectory"] = root }).Build());
        try
        {
            var store = new ConfigurationStore(paths);
            Assert.Equal(14, store.Load().LogRetentionDays);
            await store.SaveAsync(new ApplicationConfiguration { DisplayName = "Saved" }, CancellationToken.None);
            await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(new ApplicationConfiguration { LogRetentionDays = 0 }, CancellationToken.None));
            Assert.Equal("Saved", new ConfigurationStore(paths).Load().DisplayName);
            await File.WriteAllTextAsync(paths.Configuration, """{"password":"synthetic-placeholder"}""");
            Assert.Throws<InvalidOperationException>(() => store.Load());
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void StructuredLogsRedactKnownSecretsUrlsAssignmentsAndPruneOnlyExpiredDatedLogs()
    {
        var root = Path.Combine(Path.GetTempPath(), "tdsblive-log-tests", Guid.NewGuid().ToString());
        var paths = new ApplicationPaths(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["TDSBLive:DataDirectory"] = root }).Build());
        try
        {
            Directory.CreateDirectory(paths.Logs);
            var old = Path.Combine(paths.Logs, DateTime.UtcNow.AddDays(-14).ToString("yyyy-MM-dd") + ".jsonl");
            var retained = Path.Combine(paths.Logs, DateTime.UtcNow.AddDays(-13).ToString("yyyy-MM-dd") + ".jsonl");
            File.WriteAllText(old, "{}");
            File.WriteAllText(retained, "{}");
            File.WriteAllText(Path.Combine(paths.Logs, "unrelated.jsonl"), "{}");
            var sensitive = new SensitiveValues();
            sensitive.Set("test", "synthetic-private");
            using var provider = new RedactedFileLoggerProvider(paths, new ApplicationConfiguration(), sensitive);
            var logger = provider.CreateLogger("tests");
            logger.LogInformation("Known {Value} URL {Url} password=synthetic-password", "synthetic-private", "https://example.invalid/api?key=synthetic-url");
            var text = File.ReadAllText(Path.Combine(paths.Logs, DateTime.UtcNow.ToString("yyyy-MM-dd") + ".jsonl"));
            Assert.DoesNotContain("synthetic-private", text);
            Assert.DoesNotContain("synthetic-password", text);
            Assert.DoesNotContain("synthetic-url", text);
            using var json = JsonDocument.Parse(text);
            Assert.Equal("Information", json.RootElement.GetProperty("level").GetString());
            Assert.False(File.Exists(old));
            Assert.True(File.Exists(retained));
            Assert.True(File.Exists(Path.Combine(paths.Logs, "unrelated.jsonl")));
            sensitive.Remove("test");
            Assert.Empty(sensitive.Snapshot());
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
