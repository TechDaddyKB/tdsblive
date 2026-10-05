using System.Diagnostics;
using ExtensionSuite.Desktop;
using Tmds.DBus.Protocol;
using Xunit;

namespace ExtensionSuite.Desktop.Tests;

public sealed class LinuxTrayRegistrationTests
{
    [LinuxOnlyFact]
    public async Task MissingWatcherOrHostDoesNotCountAnExportedItemAsRegistered()
    {
        await using var bus = await OwnedBus.StartAsync();
        Assert.False((await LinuxTrayRegistration.ReadAsync(Environment.ProcessId, CancellationToken.None, bus.Address)).Registered);
        using var item = await bus.ItemAsync();
        using var watcher = await bus.WatcherAsync(false, item.Name);
        var result = await LinuxTrayRegistration.ReadAsync(Environment.ProcessId, CancellationToken.None, bus.Address);
        Assert.False(result.HostAvailable);
        Assert.False(result.Registered);
    }

    [LinuxOnlyFact]
    public async Task ActualBusOwnerAndApplicationPropertiesAreRequired()
    {
        await using var bus = await OwnedBus.StartAsync();
        var claimedPid = Environment.ProcessId + 1000;
        using var item = await bus.ItemAsync(claimedPid: claimedPid);
        using var watcher = await bus.WatcherAsync(true, item.Name);
        var foreign = await LinuxTrayRegistration.ReadAsync(claimedPid, CancellationToken.None, bus.Address);
        Assert.True(foreign.HostAvailable);
        Assert.Equal(0, foreign.OwnedItems);
        var owned = await LinuxTrayRegistration.ReadAsync(Environment.ProcessId, CancellationToken.None, bus.Address);
        Assert.True(owned.Registered);
        Assert.Equal(1, owned.OwnedItems);
        item.Handler.Title = "Another application";
        Assert.False((await LinuxTrayRegistration.ReadAsync(Environment.ProcessId, CancellationToken.None, bus.Address)).Registered);
        item.Handler.Title = "TDSBLive — Running";
        item.Handler.Status = "Passive";
        Assert.False((await LinuxTrayRegistration.ReadAsync(Environment.ProcessId, CancellationToken.None, bus.Address)).Registered);
    }

    [LinuxOnlyFact]
    public async Task AliasesOfOneItemAreDeduplicatedButMultipleOwnedItemsRemainVisibleAsAnError()
    {
        await using var bus = await OwnedBus.StartAsync();
        using var first = await bus.ItemAsync();
        using var second = await bus.ItemAsync();
        using var watcher = await bus.WatcherAsync(true, first.Name, first.Connection.UniqueName! + "/StatusNotifierItem");
        var result = await LinuxTrayRegistration.ReadAsync(Environment.ProcessId, CancellationToken.None, bus.Address);
        Assert.True(result.Registered);
        Assert.Equal(1, result.OwnedItems);
        watcher.Handler.Items = [first.Name, second.Name];
        result = await LinuxTrayRegistration.ReadAsync(Environment.ProcessId, CancellationToken.None, bus.Address);
        Assert.True(result.HostAvailable);
        Assert.Equal(2, result.OwnedItems);
        Assert.False(result.Registered);
    }

    [LinuxOnlyFact]
    public async Task StaleInvalidAndForeignEntriesDoNotHideTheOneOwnedItem()
    {
        await using var bus = await OwnedBus.StartAsync();
        using var item = await bus.ItemAsync();
        using var watcher = await bus.WatcherAsync(true, "org.kde.StatusNotifierItem-999999-0",
            "unrelated-service/StatusNotifierItem", ":invalid/WrongPath", new string('x', 513), item.Name);
        var result = await LinuxTrayRegistration.ReadAsync(Environment.ProcessId, CancellationToken.None, bus.Address);
        Assert.True(result.Registered);
        Assert.Equal(1, result.OwnedItems);
    }

    [LinuxOnlyFact]
    public async Task DisappearingAndReturningWatcherIsQueriedFresh()
    {
        await using var bus = await OwnedBus.StartAsync();
        using var item = await bus.ItemAsync();
        using var first = await bus.WatcherAsync(true, item.Name);
        Assert.True((await LinuxTrayRegistration.ReadAsync(Environment.ProcessId, CancellationToken.None, bus.Address)).Registered);
        await first.Connection.ReleaseNameAsync("org.kde.StatusNotifierWatcher");
        Assert.False((await LinuxTrayRegistration.ReadAsync(Environment.ProcessId, CancellationToken.None, bus.Address)).Registered);
        using var second = await bus.WatcherAsync(true);
        Assert.False((await LinuxTrayRegistration.ReadAsync(Environment.ProcessId, CancellationToken.None, bus.Address)).Registered);
        second.Handler.Items = [item.Name];
        Assert.True((await LinuxTrayRegistration.ReadAsync(Environment.ProcessId, CancellationToken.None, bus.Address)).Registered);
    }

    [LinuxOnlyFact]
    public async Task WatcherOwnershipChangeDuringAProbeCannotReturnTheOldList()
    {
        await using var bus = await OwnedBus.StartAsync();
        using var item = await bus.ItemAsync();
        using var first = await bus.WatcherAsync(true, item.Name);
        first.Handler.BeforeItemsReply = () => first.Connection.ReleaseNameAsync("org.kde.StatusNotifierWatcher");
        Assert.False((await LinuxTrayRegistration.ReadAsync(Environment.ProcessId, CancellationToken.None, bus.Address)).Registered);
    }

    [LinuxOnlyFact]
    public async Task CancellationAndMissingBusReturnFallbackWithoutChangingTheOwnedWatcher()
    {
        await using var bus = await OwnedBus.StartAsync();
        using var item = await bus.ItemAsync();
        using var watcher = await bus.WatcherAsync(true, item.Name);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.False((await LinuxTrayRegistration.ReadAsync(Environment.ProcessId, canceled.Token, bus.Address)).Registered);
        Assert.False((await LinuxTrayRegistration.ReadAsync(Environment.ProcessId, CancellationToken.None,
            "unix:path=/tmp/tdsblive-missing-bus-" + Guid.NewGuid().ToString("N"))).Registered);
        Assert.True((await LinuxTrayRegistration.ReadAsync(Environment.ProcessId, CancellationToken.None, bus.Address)).Registered);
    }

    private sealed class LinuxOnlyFactAttribute : FactAttribute
    {
        public LinuxOnlyFactAttribute()
        {
            if (!OperatingSystem.IsLinux() || !File.Exists("/usr/bin/dbus-daemon"))
                Skip = "Actual isolated D-Bus protocol cases require Linux and dbus-daemon.";
        }
    }

    private sealed class OwnedBus(Process daemon, string address) : IAsyncDisposable
    {
        public string Address { get; } = address;
        private int sequence;
        public static async Task<OwnedBus> StartAsync()
        {
            var info = new ProcessStartInfo("/usr/bin/dbus-daemon") { UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "--session", "--nofork", "--nopidfile", "--nosyslog", "--print-address=1" })
                info.ArgumentList.Add(argument);
            var daemon = Process.Start(info)!;
            try
            {
                var address = await daemon.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5));
                if (string.IsNullOrWhiteSpace(address)) throw new InvalidOperationException("Owned test bus did not start.");
                return new(daemon, address);
            }
            catch { if (!daemon.HasExited) daemon.Kill(); daemon.Dispose(); throw; }
        }

        public async Task<OwnedItem> ItemAsync(int? claimedPid = null)
        {
            var connection = new DBusConnection(Address);
            await connection.ConnectAsync();
            var name = "org.kde.StatusNotifierItem-" + (claimedPid ?? Environment.ProcessId) + "-" + sequence++;
            await connection.RequestNameAsync(name);
            var handler = new ItemHandler();
            connection.AddMethodHandler(handler);
            return new(connection, handler, name + "/StatusNotifierItem");
        }

        public async Task<OwnedWatcher> WatcherAsync(bool hosted, params string[] items)
        {
            var connection = new DBusConnection(Address);
            await connection.ConnectAsync();
            var handler = new WatcherHandler { Hosted = hosted, Items = items };
            connection.AddMethodHandler(handler);
            await connection.RequestNameAsync("org.kde.StatusNotifierWatcher");
            return new(connection, handler);
        }

        public async ValueTask DisposeAsync()
        {
            if (!daemon.HasExited) daemon.Kill(); // Only this owned private bus process.
            await daemon.WaitForExitAsync();
            daemon.Dispose();
        }
    }

    private sealed record OwnedItem(DBusConnection Connection, ItemHandler Handler, string Name) : IDisposable
    {
        public void Dispose() => Connection.Dispose();
    }
    private sealed record OwnedWatcher(DBusConnection Connection, WatcherHandler Handler) : IDisposable
    {
        public void Dispose() => Connection.Dispose();
    }

    private sealed class WatcherHandler : IPathMethodHandler
    {
        public string Path => "/StatusNotifierWatcher";
        public bool HandlesChildPaths => false;
        public bool Hosted { get; init; }
        public string[] Items { get; set; } = [];
        public Func<Task>? BeforeItemsReply { get; set; }
        public ValueTask HandleMethodAsync(MethodContext context)
        {
            var reader = context.Request.GetBodyReader();
            Assert.Equal("org.kde.StatusNotifierWatcher", reader.ReadString());
            var property = reader.ReadString();
            return ReplyAsync(context, property);
        }
        private async ValueTask ReplyAsync(MethodContext context, string property)
        {
            if (property == "RegisteredStatusNotifierItems" && BeforeItemsReply is not null) await BeforeItemsReply();
            using var writer = context.CreateReplyWriter("v");
            if (property == "IsStatusNotifierHostRegistered") { writer.WriteSignature("b"); writer.WriteBool(Hosted); }
            else { Assert.Equal("RegisteredStatusNotifierItems", property); writer.WriteSignature("as"); writer.WriteArray(Items); }
            context.Reply(writer.CreateMessage());
        }
    }

    private sealed class ItemHandler : IPathMethodHandler
    {
        public string Path => "/StatusNotifierItem";
        public bool HandlesChildPaths => false;
        public string Title { get; set; } = "TDSBLive — Running";
        public string Status { get; set; } = "Active";
        public ValueTask HandleMethodAsync(MethodContext context)
        {
            var reader = context.Request.GetBodyReader();
            Assert.Equal("org.kde.StatusNotifierItem", reader.ReadString());
            var property = reader.ReadString();
            using var writer = context.CreateReplyWriter("v");
            writer.WriteSignature("s");
            writer.WriteString(property switch { "Title" => Title, "Status" => Status, _ => throw new InvalidOperationException() });
            context.Reply(writer.CreateMessage());
            return default;
        }
    }
}
