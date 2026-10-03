using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using ExtensionSuite.Core;
using ExtensionSuite.Rumble;

namespace ExtensionSuite.Host;

public static class DeveloperCommands
{
    private const int MaximumBytes = 64 * 1024 * 1024;

    public static async Task<int?> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] != "tools") return null;
        if (args.Length != 3 || args[1] is not ("rumble-replay" or "sanitize-logs"))
        {
            Console.Error.WriteLine("Usage: TDSBLive tools rumble-replay|sanitize-logs <scanner-approved JSONL[.gz]>");
            return 2;
        }
        try
        {
            var input = await ReadApprovedAsync(args[2]);
            if (args[1] == "rumble-replay") Console.WriteLine(JsonSerializer.Serialize(Replay(input)));
            else
            {
                var rows = ParseLines(input);
                foreach (var row in rows) Console.WriteLine(DiagnosticSanitizer.Shape(row)?.ToJsonString() ?? "null");
            }
            return 0;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or ArgumentException or FormatException or InvalidOperationException or OverflowException or System.ComponentModel.Win32Exception)
        {
            // Never echo the input, path, parser exception or scanner output.
            Console.Error.WriteLine("Utility refused the input. Require valid bounded chronological JSONL and a successful secrets scan. If credentials are present, rotate them at their source and remove them before retrying.");
            return 1;
        }
    }

    public static object Replay(byte[] input)
    {
        var engine = new RumbleSnapshotEngine(new SensitiveValues());
        var state = new RumbleState(); DateTimeOffset? previous = null;
        var events = new Dictionary<string, int>(StringComparer.Ordinal);
        var diagnostics = new Dictionary<string, int>(StringComparer.Ordinal);
        var count = 0;
        foreach (var row in ParseLines(input))
        {
            var poll = row["observed_at"] is { } capturedTime
                ? new RumblePoll(DateTimeOffset.Parse(capturedTime.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture),
                    row["outcome"]?.GetValue<string>() ?? "ok", row["http_status"]?.GetValue<int>() ?? 200, row["payload"] as JsonObject)
                : row.Deserialize<RumblePoll>(new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw new InvalidDataException();
            if (poll.ObservedAt == default) throw new InvalidDataException();
            if (poll.ObservedAt.Offset != TimeSpan.Zero || previous is { } last && poll.ObservedAt < last) throw new InvalidDataException();
            var batch = engine.Reconcile(state, poll, "offline-replay", count == 0, EventProvenance.Replay);
            state = batch.State; previous = poll.ObservedAt; count++;
            foreach (var item in batch.Events) events[item.Type] = events.GetValueOrDefault(item.Type) + 1;
            foreach (var item in batch.Diagnostics) diagnostics[item.Code] = diagnostics.GetValueOrDefault(item.Code) + 1;
        }
        return new { polls = count, events, diagnostics, provenance = "replay", persisted = false, liveActionsAllowed = false };
    }

    private static JsonNode[] ParseLines(byte[] input)
    {
        if (input.Length > MaximumBytes) throw new InvalidDataException();
        var lines = System.Text.Encoding.UTF8.GetString(input).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length is 0 or > 100000 || lines.Any(line => line.Length > 2 * 1024 * 1024)) throw new InvalidDataException();
        return lines.Select(line => JsonNode.Parse(line, documentOptions: new JsonDocumentOptions { MaxDepth = 32 }) ?? throw new InvalidDataException()).ToArray();
    }

    private static async Task<byte[]> ReadApprovedAsync(string path)
    {
        if (new FileInfo(path).Length > MaximumBytes) throw new InvalidDataException();
        await ScanAsync(path);
        await using var file = File.OpenRead(path);
        using var data = new MemoryStream();
        if (path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            await using var gzip = new GZipStream(file, CompressionMode.Decompress);
            var buffer = new byte[65536]; int read;
            while ((read = await gzip.ReadAsync(buffer)) != 0)
            {
                if (data.Length + read > MaximumBytes) throw new InvalidDataException();
                data.Write(buffer, 0, read);
            }
            var temporary = Path.GetTempFileName();
            try { await File.WriteAllBytesAsync(temporary, data.ToArray()); await ScanAsync(temporary); }
            finally { File.Delete(temporary); }
        }
        else await file.CopyToAsync(data);
        if (data.Length > MaximumBytes) throw new InvalidDataException();
        return data.ToArray();
    }

    private static async Task ScanAsync(string path)
    {
        var start = new ProcessStartInfo("sonar") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in new[] { "analyze", "secrets", "--", path }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidDataException();
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(); await error;
        if (process.ExitCode != 0 || !(await output).Contains("No secrets found", StringComparison.Ordinal)) throw new InvalidDataException();
    }
}
