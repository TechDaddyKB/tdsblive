using System.Collections.Concurrent;
using System.Diagnostics;
using ExtensionSuite.Desktop;
using Tmds.DBus.Protocol;
using Xunit;

namespace ExtensionSuite.Desktop.Tests;

public sealed class LinuxNativeTrayTests
{
    private sealed class LinuxOnlyFactAttribute : FactAttribute
    {
        public LinuxOnlyFactAttribute()
        { if (!OperatingSystem.IsLinux()) Skip = "Requires an owned native Linux session bus."; }
    }
    [LinuxOnlyFact]
    public async Task TooltipAndAllPropertiesCarryCurrentStatusAndTheSameIcon()
    {
        await using var bus = await Bus.StartAsync();
        using var tray = new LinuxNativeTray(_ => throw new InvalidOperationException(), DesktopApp.CreateTrayPixmap());
        await tray.EnsureRegisteredAsync(CancellationToken.None, address: bus.Address);
        tray.Update("TDSBLive — Running", true);
        var tooltip = await bus.Client.CallMethodAsync(Property(bus.Client, tray.ServiceName, "Get", "ToolTip"),
            static (message, _) => message.GetBodyReader().ReadVariantValue());
        Assert.Equal("TDSBLive — Running", tooltip.GetItem(2).GetString());
        Assert.Equal(32, tooltip.GetItem(1).GetItem(0).GetItem(0).GetInt32());
        Assert.Equal(32 * 32 * 4, tooltip.GetItem(1).GetItem(0).GetItem(2).GetArray<byte>().Length);
        tray.Update("TDSBLive — Restarting TDSBLive…", false);
        var all = await bus.Client.CallMethodAsync(Property(bus.Client, tray.ServiceName, "GetAll"),
            static (message, _) => message.GetBodyReader().ReadDictionaryOfStringToVariantValue());
        Assert.Equal("TDSBLive — Restarting TDSBLive…", all["ToolTip"].GetItem(2).GetString());
        Assert.Equal(all["Title"].GetString(), all["ToolTip"].GetItem(2).GetString());
        Assert.Equal(all["IconPixmap"].GetItem(0).GetItem(2).GetArray<byte>(),
            all["ToolTip"].GetItem(1).GetItem(0).GetItem(2).GetArray<byte>());
        Assert.Equal("tdsblive", all["Id"].GetString());
    }

    [LinuxOnlyFact]
    public async Task FixedMenuAndBusyCommandsAgreeWithWireEvents()
    {
        await using var bus = await Bus.StartAsync();
        var clicks = new ConcurrentQueue<string>();
        using var tray = new LinuxNativeTray(clicks.Enqueue, DesktopApp.CreateTrayPixmap());
        await tray.EnsureRegisteredAsync(CancellationToken.None, address: bus.Address);
        tray.Update("TDSBLive — Running", true);
        var menu = await Layout(bus.Client, tray.ServiceName);
        Assert.Equal(new[] { "Open editor", "Restart", "Quit" }, menu.Select(item => item.Label));
        Assert.All(menu, item => Assert.True(item.Enabled));
        foreach (var id in new[] { 1, 2, 3 }) await Event(bus.Client, tray.ServiceName, id, "clicked");
        Assert.Equal(new[] { "open", "restart", "quit" }, clicks.ToArray());
        tray.Update("TDSBLive — Restarting", false);
        Assert.All(await Layout(bus.Client, tray.ServiceName), item => Assert.False(item.Enabled));
        await Event(bus.Client, tray.ServiceName, 3, "clicked");
        tray.Update("TDSBLive — Running", true);
        await Event(bus.Client, tray.ServiceName, 99, "clicked");
        await Event(bus.Client, tray.ServiceName, 2, "opened");
        Assert.Equal(3, clicks.Count);
    }

    [LinuxOnlyFact]
    public async Task BadSignatureUnknownInterfaceAndUnknownNodeCannotExecuteCommands()
    {
        await using var bus = await Bus.StartAsync();
        var clicks = new ConcurrentQueue<string>();
        using var tray = new LinuxNativeTray(clicks.Enqueue, DesktopApp.CreateTrayPixmap());
        await tray.EnsureRegisteredAsync(CancellationToken.None, address: bus.Address);
        tray.Update("TDSBLive — Running", true);
        await Assert.ThrowsAsync<DBusErrorReplyException>(() => bus.Client.CallMethodAsync(BadEvent(bus.Client, tray.ServiceName)));
        await Assert.ThrowsAsync<DBusErrorReplyException>(() => bus.Client.CallMethodAsync(Property(bus.Client, tray.ServiceName, "Get", "ToolTip", "unexpected.Interface")));
        await Assert.ThrowsAsync<DBusErrorReplyException>(() => bus.Client.CallMethodAsync(LayoutRequest(bus.Client, tray.ServiceName, 99)));
        Assert.Empty(clicks);
        Assert.Equal(3, (await Layout(bus.Client, tray.ServiceName)).Length);
    }

    [LinuxOnlyFact]
    public async Task DisposalReleasesOnlyTheOwnedServiceAndSettlesRegistrations()
    {
        await using var bus = await Bus.StartAsync();
        using var other = new DBusConnection(bus.Address);
        await other.ConnectAsync();
        await other.RequestNameAsync("owned.OtherApplication");
        var tray = new LinuxNativeTray(_ => { }, DesktopApp.CreateTrayPixmap());
        await tray.EnsureRegisteredAsync(CancellationToken.None, address: bus.Address);
        tray.Dispose(); tray.Dispose();
        await tray.Disposal.WaitAsync(TimeSpan.FromSeconds(5));
        await tray.EnsureRegisteredAsync(CancellationToken.None, address: bus.Address);
        Assert.False(await HasOwner(bus.Client, tray.ServiceName));
        Assert.True(await HasOwner(bus.Client, "owned.OtherApplication"));
    }

    [LinuxOnlyFact]
    public async Task ReplacingWatcherKeepsOneItemAndRegistersWithTheNewOwner()
    {
        await using var bus = await Bus.StartAsync();
        var first = new Watcher();
        bus.Client.AddMethodHandler(first); await bus.Client.RequestNameAsync("org.kde.StatusNotifierWatcher");
        using var tray = new LinuxNativeTray(_ => { }, DesktopApp.CreateTrayPixmap());
        tray.Update("TDSBLive — Running", true);
        await tray.EnsureRegisteredAsync(CancellationToken.None, address: bus.Address);
        Assert.True((await LinuxTrayRegistration.ReadAsync(Environment.ProcessId, CancellationToken.None, bus.Address)).Registered);
        await tray.EnsureRegisteredAsync(CancellationToken.None, retry: true, address: bus.Address);
        Assert.Single(first.Items);
        await bus.Client.ReleaseNameAsync("org.kde.StatusNotifierWatcher");
        using var replacement = new DBusConnection(bus.Address); await replacement.ConnectAsync();
        var second = new Watcher(); replacement.AddMethodHandler(second);
        await replacement.RequestNameAsync("org.kde.StatusNotifierWatcher");
        await tray.EnsureRegisteredAsync(CancellationToken.None, address: bus.Address);
        Assert.Single(second.Items); Assert.Equal(first.Items.Single(), second.Items.Single());
        Assert.True((await LinuxTrayRegistration.ReadAsync(Environment.ProcessId, CancellationToken.None, bus.Address)).Registered);
    }

    [LinuxOnlyFact]
    public async Task MissingBusAndCancelledRegistrationDoNotInvokeAnything()
    {
        var clicks = new ConcurrentQueue<string>();
        using var tray = new LinuxNativeTray(clicks.Enqueue, DesktopApp.CreateTrayPixmap());
        await tray.EnsureRegisteredAsync(CancellationToken.None, address: "");
        await tray.EnsureRegisteredAsync(CancellationToken.None, address: "unix:path=/tmp/owned-absent-" + Guid.NewGuid().ToString("N"));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await tray.EnsureRegisteredAsync(cancelled.Token, address: "");
        tray.Update("TDSBLive — Stopped unexpectedly", false); tray.Dispose(); await tray.Disposal;
        Assert.Empty(clicks);
    }

    [LinuxOnlyFact]
    public async Task GroupQueriesFilterPropertiesAndGroupEventsIgnoreUnknownItems()
    {
        await using var bus = await Bus.StartAsync();
        var clicks = new ConcurrentQueue<string>();
        using var tray = new LinuxNativeTray(clicks.Enqueue, DesktopApp.CreateTrayPixmap());
        await tray.EnsureRegisteredAsync(CancellationToken.None, address: bus.Address);
        tray.Update("TDSBLive — Running", true);
        var labels = await bus.Client.CallMethodAsync(MenuRequest(bus.Client, tray.ServiceName,
            "GetGroupProperties", "aias", (ref MessageWriter writer) =>
            { writer.WriteArray(new[] { 1, 2, 3 }); writer.WriteArray(new[] { "label" }); }), static (message, _) =>
            {
                var reader = message.GetBodyReader(); var end = reader.ReadArrayStart(DBusType.Struct);
                var result = new List<string>();
                while (reader.HasNext(end))
                {
                    reader.AlignStruct(); _ = reader.ReadInt32();
                    var props = reader.ReadDictionaryOfStringToVariantValue(); Assert.Single(props);
                    result.Add(props["label"].GetString());
                }
                return result.ToArray();
            });
        Assert.Equal(new[] { "Open editor", "Restart", "Quit" }, labels);
        var unknown = await bus.Client.CallMethodAsync(MenuRequest(bus.Client, tray.ServiceName,
            "EventGroup", "a(isvu)", (ref MessageWriter writer) =>
            {
                var start = writer.WriteArrayStart(DBusType.Struct);
                foreach (var id in new[] { 1, 99 })
                {
                    writer.WriteStructureStart(); writer.WriteInt32(id); writer.WriteString("clicked");
                    writer.WriteVariantInt32(0); writer.WriteUInt32(0);
                }
                writer.WriteArrayEnd(start);
            }), static (message, _) => message.GetBodyReader().ReadArrayOfInt32());
        Assert.Equal(new[] { 99 }, unknown); Assert.Equal(new[] { "open" }, clicks.ToArray());
        var label = await bus.Client.CallMethodAsync(MenuRequest(bus.Client, tray.ServiceName,
            "GetProperty", "is", (ref MessageWriter writer) => { writer.WriteInt32(3); writer.WriteString("label"); }),
            static (message, _) => message.GetBodyReader().ReadVariantValue().GetString());
        Assert.Equal("Quit", label);
        await Assert.ThrowsAsync<DBusErrorReplyException>(() => bus.Client.CallMethodAsync(MenuRequest(bus.Client, tray.ServiceName,
            "GetProperty", "is", (ref MessageWriter writer) => { writer.WriteInt32(3); writer.WriteString("privateData"); })));
    }

    [LinuxOnlyFact]
    public async Task MenuOpeningAndNonactivatingSniCallsNeverExecuteCommands()
    {
        await using var bus = await Bus.StartAsync();
        var clicks = new ConcurrentQueue<string>();
        using var tray = new LinuxNativeTray(clicks.Enqueue, DesktopApp.CreateTrayPixmap());
        await tray.EnsureRegisteredAsync(CancellationToken.None, address: bus.Address);
        tray.Update("TDSBLive — Running", true); tray.Update("TDSBLive — Running", true);
        var update = await bus.Client.CallMethodAsync(MenuRequest(bus.Client, tray.ServiceName, "AboutToShow", "i",
            (ref MessageWriter writer) => writer.WriteInt32(0)), static (message, _) => message.GetBodyReader().ReadBool());
        Assert.False(update);
        var errors = await bus.Client.CallMethodAsync(MenuRequest(bus.Client, tray.ServiceName, "AboutToShowGroup", "ai",
            (ref MessageWriter writer) => writer.WriteArray(new[] { 0, 1, 99 })), static (message, _) =>
            { var reader = message.GetBodyReader(); Assert.Empty(reader.ReadArrayOfInt32()); return reader.ReadArrayOfInt32(); });
        Assert.Equal(new[] { 99 }, errors);
        foreach (var member in new[] { "SecondaryActivate", "ContextMenu", "Scroll" })
            await bus.Client.CallMethodAsync(Request(bus.Client, tray.ServiceName, LinuxNativeTray.ItemPath, LinuxNativeTray.ItemInterface,
                member, member == "Scroll" ? "is" : "ii", (ref MessageWriter writer) =>
                { writer.WriteInt32(0); if (member == "Scroll") writer.WriteString("vertical"); else writer.WriteInt32(0); }));
        Assert.Empty(clicks);
        await bus.Client.CallMethodAsync(Request(bus.Client, tray.ServiceName, LinuxNativeTray.ItemPath, LinuxNativeTray.ItemInterface,
            "Activate", "ii", (ref MessageWriter writer) => { writer.WriteInt32(0); writer.WriteInt32(0); }));
        Assert.Equal(new[] { "open" }, clicks.ToArray());
        var xml = await bus.Client.CallMethodAsync(Request(bus.Client, tray.ServiceName, LinuxNativeTray.MenuPath,
            "org.freedesktop.DBus.Introspectable", "Introspect", null), static (message, _) => message.GetBodyReader().ReadString());
        Assert.Contains("com.canonical.dbusmenu", xml);
    }

    private delegate void Payload(ref MessageWriter writer);
    private static MessageBuffer MenuRequest(DBusConnection connection, string name, string member, string signature, Payload body) =>
        Request(connection, name, LinuxNativeTray.MenuPath, LinuxNativeTray.MenuInterface, member, signature, body);
    private static MessageBuffer Request(DBusConnection connection, string name, string path, string @interface, string member, string? signature, Payload? body = null)
    {
        var writer = connection.GetMessageWriter();
        try
        {
            writer.WriteMethodCallHeader(destination: name, path: path, @interface: @interface, member: member, signature: signature);
            body?.Invoke(ref writer); return writer.CreateMessage();
        }
        finally { writer.Dispose(); }
    }

    private static MessageBuffer Property(DBusConnection connection, string name, string member, string? property = null, string @interface = LinuxNativeTray.ItemInterface)
    {
        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(destination: name, path: LinuxNativeTray.ItemPath,
            @interface: "org.freedesktop.DBus.Properties", member: member, signature: property is null ? "s" : "ss");
        writer.WriteString(@interface); if (property is not null) writer.WriteString(property);
        return writer.CreateMessage();
    }

    private static MessageBuffer LayoutRequest(DBusConnection connection, string name, int parent = 0)
    {
        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(destination: name, path: LinuxNativeTray.MenuPath,
            @interface: LinuxNativeTray.MenuInterface, member: "GetLayout", signature: "iias");
        writer.WriteInt32(parent); writer.WriteInt32(-1); writer.WriteArray(System.Array.Empty<string>());
        return writer.CreateMessage();
    }
    private sealed record MenuItem(string Label, bool Enabled);
    private static Task<MenuItem[]> Layout(DBusConnection connection, string name) => connection.CallMethodAsync(
        LayoutRequest(connection, name), static (message, _) =>
        {
            var reader = message.GetBodyReader(); _ = reader.ReadUInt32(); reader.AlignStruct();
            _ = reader.ReadInt32(); _ = reader.ReadDictionaryOfStringToVariantValue();
            var children = reader.ReadArrayStart(DBusType.Variant); var result = new List<MenuItem>();
            while (reader.HasNext(children))
            {
                var node = reader.ReadVariantValue(); var properties = node.GetItem(1).GetDictionary<string, VariantValue>();
                result.Add(new(properties["label"].GetString(), properties["enabled"].GetBool()));
            }
            return result.ToArray();
        });

    private static Task Event(DBusConnection connection, string name, int id, string eventName)
    {
        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(destination: name, path: LinuxNativeTray.MenuPath,
            @interface: LinuxNativeTray.MenuInterface, member: "Event", signature: "isvu");
        writer.WriteInt32(id); writer.WriteString(eventName); writer.WriteVariantInt32(0); writer.WriteUInt32(0);
        return connection.CallMethodAsync(writer.CreateMessage());
    }
    private static MessageBuffer BadEvent(DBusConnection connection, string name)
    {
        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(destination: name, path: LinuxNativeTray.MenuPath,
            @interface: LinuxNativeTray.MenuInterface, member: "Event", signature: "is");
        writer.WriteInt32(3); writer.WriteString("clicked"); return writer.CreateMessage();
    }
    private static Task<bool> HasOwner(DBusConnection connection, string name)
    {
        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(destination: "org.freedesktop.DBus", path: "/org/freedesktop/DBus",
            @interface: "org.freedesktop.DBus", member: "NameHasOwner", signature: "s");
        writer.WriteString(name);
        return connection.CallMethodAsync(writer.CreateMessage(), static (message, _) => message.GetBodyReader().ReadBool());
    }

    private sealed class Bus(Process daemon, string address, DBusConnection client) : IAsyncDisposable
    {
        public string Address => address;
        public DBusConnection Client => client;
        public static async Task<Bus> StartAsync()
        {
            var info = new ProcessStartInfo("/usr/bin/dbus-daemon") { UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "--session", "--nofork", "--nopidfile", "--nosyslog", "--print-address=1" }) info.ArgumentList.Add(arg);
            var process = Process.Start(info)!;
            try
            {
                var address = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(string.IsNullOrWhiteSpace(address));
                var connection = new DBusConnection(address);
                await connection.ConnectAsync(); return new(process, address, connection);
            }
            catch { if (!process.HasExited) process.Kill(); process.Dispose(); throw; }
        }
        public async ValueTask DisposeAsync()
        {
            client.Dispose(); if (!daemon.HasExited) daemon.Kill(); // Only this owned test bus.
            await daemon.WaitForExitAsync(); daemon.Dispose();
        }
    }

    private sealed class Watcher : IPathMethodHandler
    {
        public string Path => "/StatusNotifierWatcher";
        public bool HandlesChildPaths => false;
        public HashSet<string> Items { get; } = new(StringComparer.Ordinal);
        public ValueTask HandleMethodAsync(MethodContext context)
        {
            var reader = context.Request.GetBodyReader();
            if (context.Request.MemberAsString == "RegisterStatusNotifierItem")
            {
                Items.Add(reader.ReadString() + LinuxNativeTray.ItemPath);
                using var writer = context.CreateReplyWriter(null); context.Reply(writer.CreateMessage());
            }
            else
            {
                Assert.Equal("org.kde.StatusNotifierWatcher", reader.ReadString());
                var property = reader.ReadString();
                using var writer = context.CreateReplyWriter("v");
                if (property == "IsStatusNotifierHostRegistered") writer.WriteVariantBool(true);
                else { Assert.Equal("RegisteredStatusNotifierItems", property); writer.WriteSignature("as"); writer.WriteArray(Items.ToArray()); }
                context.Reply(writer.CreateMessage());
            }
            return default;
        }
    }
}
