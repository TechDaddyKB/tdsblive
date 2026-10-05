using Tmds.DBus.Protocol;
using Pixmap = Tmds.DBus.Protocol.Struct<int, int, Tmds.DBus.Protocol.Array<byte>>;

namespace ExtensionSuite.Desktop;

// Avalonia 12.1.3 exports empty SNI tooltip fields. Keep its native dialogs,
// but own this small, fixed menu through the already-pinned D-Bus transport.
// No private framework reflection, integration data or desktop capabilities.
public sealed class LinuxNativeTray(Action<string> activate, byte[] argbPixels) : IDisposable
{
    internal const string ItemPath = "/StatusNotifierItem";
    internal const string MenuPath = "/Menu";
    internal const string ItemInterface = "org.kde.StatusNotifierItem";
    internal const string MenuInterface = "com.canonical.dbusmenu";
    private const string Watcher = "org.kde.StatusNotifierWatcher";
    private readonly SemaphoreSlim registration = new(1, 1);
    private readonly (CancellationTokenSource Source, CancellationToken Token) lifetime = NewLifetime();
    private DBusConnection? connection;
    private string? watcherOwner;
    private volatile bool disposed;
    private volatile Snapshot snapshot = new("TDSBLive — Starting", false, 1);
    internal sealed record Snapshot(string Title, bool Enabled, uint Revision);
    internal string ServiceName { get; } = "org.kde.StatusNotifierItem-" + Environment.ProcessId + "-tdsblive";
    private readonly Tmds.DBus.Protocol.Array<Pixmap> pixmaps = new([new(32, 32, new(argbPixels))]);

    public async Task EnsureRegisteredAsync(CancellationToken cancellationToken, bool retry = false, string? address = null)
    {
        if (disposed) return;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(2));
        var acquired = false;
        try
        {
            await registration.WaitAsync(deadline.Token);
            acquired = true;
            if (disposed) return;
            connection ??= await CreateConnectionAsync(address, deadline.Token);
            if (connection is null) return;
            var owner = await connection.CallMethodAsync(Call(connection, "org.freedesktop.DBus",
                "/org/freedesktop/DBus", "org.freedesktop.DBus", "GetNameOwner", Watcher),
                static (message, _) => message.GetBodyReader().ReadString()).WaitAsync(deadline.Token);
            if (retry || owner != watcherOwner)
            {
                await connection.CallMethodAsync(Call(connection, owner, "/StatusNotifierWatcher",
                    Watcher, "RegisterStatusNotifierItem", ServiceName)).WaitAsync(deadline.Token);
                watcherOwner = owner;
            }
        }
        catch (DBusErrorReplyException) { watcherOwner = null; }
        catch (Exception error) when (error is DBusExceptionBase or IOException or InvalidOperationException or ArgumentException or OperationCanceledException)
        {
            // Missing/dead session bus is a control-window fallback, never a
            // reason to stop the backend or restart the desktop's tray service.
            if (acquired) { connection?.Dispose(); connection = null; watcherOwner = null; }
        }
        finally { if (acquired) registration.Release(); }
    }

    private async Task<DBusConnection?> CreateConnectionAsync(string? address, CancellationToken cancellationToken)
    {
        address ??= DBusAddress.Session;
        if (string.IsNullOrWhiteSpace(address)) return null;
        var candidate = new DBusConnection(address);
        try
        {
            await candidate.ConnectAsync().AsTask().WaitAsync(cancellationToken);
            await candidate.RequestNameAsync(ServiceName).WaitAsync(cancellationToken);
            candidate.AddMethodHandler(new Handler(this, ItemPath));
            candidate.AddMethodHandler(new Handler(this, MenuPath));
            cancellationToken.ThrowIfCancellationRequested();
            return candidate;
        }
        catch { candidate.Dispose(); throw; }
    }

    public void Update(string title, bool enabled)
    {
        var old = snapshot;
        if (old.Title == title && old.Enabled == enabled) return;
        snapshot = new(title, enabled, unchecked(old.Revision + 1));
        if (connection is not { } bus || disposed) return;
        foreach (var member in new[] { "NewTitle", "NewToolTip" })
        {
            using var writer = bus.GetMessageWriter();
            writer.WriteSignalHeader(path: ItemPath, @interface: ItemInterface, member: member);
            bus.TrySendMessage(writer.CreateMessage());
        }
        if (old.Enabled != enabled)
        {
            using var writer = bus.GetMessageWriter();
            writer.WriteSignalHeader(path: MenuPath, @interface: MenuInterface, member: "LayoutUpdated", signature: "ui");
            writer.WriteUInt32(snapshot.Revision); writer.WriteInt32(0);
            bus.TrySendMessage(writer.CreateMessage());
        }
    }

    internal Dictionary<string, VariantValue> ItemProperties(Snapshot state) => new()
    {
        ["Category"] = "ApplicationStatus", ["Id"] = "tdsblive", ["Title"] = state.Title,
        ["Status"] = "Active", ["WindowId"] = 0, ["IconThemePath"] = "",
        ["Menu"] = new ObjectPath(MenuPath), ["ItemIsMenu"] = false,
        ["IconName"] = "", ["IconPixmap"] = pixmaps,
        ["OverlayIconName"] = "", ["OverlayIconPixmap"] = new Tmds.DBus.Protocol.Array<Pixmap>(),
        ["AttentionIconName"] = "", ["AttentionIconPixmap"] = new Tmds.DBus.Protocol.Array<Pixmap>(),
        ["AttentionMovieName"] = "", ["ToolTip"] = VariantValue.Struct("", pixmaps, state.Title, "")
    };

    private static Dictionary<string, VariantValue> MenuProperties() => new()
    { ["Version"] = 3u, ["TextDirection"] = "ltr", ["Status"] = "normal", ["IconThemePath"] = VariantValue.Array(System.Array.Empty<string>()) };

    private static Dictionary<string, VariantValue> NodeProperties(int id, Snapshot state) => id == 0
        ? new() { ["children-display"] = "submenu" }
        : new() { ["label"] = id switch { 1 => "Open editor", 2 => "Restart", 3 => "Quit", _ => "" },
            ["enabled"] = state.Enabled, ["visible"] = true, ["type"] = "standard" };

    private static Dictionary<string, VariantValue> Filter(Dictionary<string, VariantValue> properties, string[] names) =>
        names.Length == 0 ? properties : properties.Where(entry => names.Contains(entry.Key, StringComparer.Ordinal)).ToDictionary();

    private static void WriteNode(ref MessageWriter writer, int id, int depth, Snapshot state, string[] names)
    {
        writer.WriteStructureStart(); writer.WriteInt32(id); writer.WriteDictionary(Filter(NodeProperties(id, state), names));
        var children = id == 0 && depth != 0 ? new[] { 1, 2, 3 }.Select(child => VariantValue.Struct(child,
            new Dict<string, VariantValue>(Filter(NodeProperties(child, state), names)), VariantValue.ArrayOfVariant(System.Array.Empty<VariantValue>()))).ToArray()
            : System.Array.Empty<VariantValue>();
        writer.WriteArray(children);
    }

    private void Click(int id, string eventName)
    {
        if (disposed || !snapshot.Enabled || eventName != "clicked") return;
        var command = id switch { 1 => "open", 2 => "restart", 3 => "quit", _ => null };
        if (command is not null) activate(command);
    }

    private sealed class Handler(LinuxNativeTray tray, string path) : IPathMethodHandler
    {
        public string Path => path;
        public bool HandlesChildPaths => false;
        public ValueTask HandleMethodAsync(MethodContext context)
        {
            try { return Dispatch(context); }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or DBusExceptionBase or IOException)
            {
                if (!context.ReplySent) Invalid(context);
                return default;
            }
        }
        private ValueTask Dispatch(MethodContext context)
        {
            var message = context.Request;
            var expectedSignature = message.InterfaceAsString switch
            {
                "org.freedesktop.DBus.Properties" => message.MemberAsString switch { "Get" => "ss", "GetAll" => "s", "Set" => "ssv", _ => null },
                "org.freedesktop.DBus.Introspectable" => "",
                ItemInterface => message.MemberAsString == "Scroll" ? "is" : "ii",
                MenuInterface => message.MemberAsString switch
                {
                    "GetLayout" => "iias", "GetGroupProperties" => "aias", "GetProperty" => "is",
                    "Event" => "isvu", "EventGroup" => "a(isvu)", "AboutToShow" => "i", "AboutToShowGroup" => "ai", _ => null
                },
                _ => null
            };
            if (expectedSignature is null) { context.ReplyUnknownMethodError(); return default; }
            if ((message.SignatureAsString ?? "") != expectedSignature) { Invalid(context); return default; }
            if (message.InterfaceAsString == "org.freedesktop.DBus.Properties") Properties(context);
            else if (message.InterfaceAsString == "org.freedesktop.DBus.Introspectable" && message.MemberAsString == "Introspect")
            {
                using var writer = context.CreateReplyWriter("s");
                writer.WriteString(path == ItemPath ? ItemXml : MenuXml); context.Reply(writer.CreateMessage());
            }
            else if (path == ItemPath && message.InterfaceAsString == ItemInterface) Item(context);
            else if (path == MenuPath && message.InterfaceAsString == MenuInterface) Menu(context);
            else context.ReplyUnknownMethodError();
            return default;
        }

        private void Properties(MethodContext context)
        {
            var reader = context.Request.GetBodyReader();
            var expected = path == ItemPath ? ItemInterface : MenuInterface;
            if (reader.ReadString() != expected) { context.ReplyUnknownMethodError(); return; }
            var properties = path == ItemPath ? tray.ItemProperties(tray.snapshot) : MenuProperties();
            if (context.Request.MemberAsString == "GetAll")
            {
                using var writer = context.CreateReplyWriter("a{sv}"); writer.WriteDictionary(properties); context.Reply(writer.CreateMessage());
            }
            else if (context.Request.MemberAsString == "Get" && properties.TryGetValue(reader.ReadString(), out var value))
            {
                using var writer = context.CreateReplyWriter("v"); writer.WriteVariant(value); context.Reply(writer.CreateMessage());
            }
            else context.ReplyError("org.freedesktop.DBus.Error.UnknownProperty", "This tray property is unavailable or read-only.");
        }

        private void Item(MethodContext context)
        {
            var member = context.Request.MemberAsString;
            if (member is not ("Activate" or "SecondaryActivate" or "ContextMenu" or "Scroll")) { context.ReplyUnknownMethodError(); return; }
            var reader = context.Request.GetBodyReader(); _ = reader.ReadInt32();
            if (member == "Scroll") _ = reader.ReadString(); else _ = reader.ReadInt32();
            if (member == "Activate") tray.Click(1, "clicked");
            using var writer = context.CreateReplyWriter(null); context.Reply(writer.CreateMessage());
        }

        private void Menu(MethodContext context)
        {
            switch (context.Request.MemberAsString)
            {
                case "GetLayout": GetLayout(context); break;
                case "GetGroupProperties": GetGroupProperties(context); break;
                case "GetProperty": GetProperty(context); break;
                case "Event": Event(context); break;
                case "EventGroup": EventGroup(context); break;
                case "AboutToShow": AboutToShow(context); break;
                case "AboutToShowGroup": AboutToShowGroup(context); break;
                default: context.ReplyUnknownMethodError(); break;
            }
        }

        private void GetLayout(MethodContext context)
        {
            var reader = context.Request.GetBodyReader();
            var id = reader.ReadInt32(); var depth = reader.ReadInt32();
            var names = reader.ReadArrayOfString();
            if (id is < 0 or > 3 || names.Length > 64) { Invalid(context); return; }
            var writer = context.CreateReplyWriter("u(ia{sv}av)");
            try
            {
                var state = tray.snapshot; writer.WriteUInt32(state.Revision); WriteNode(ref writer, id, depth, state, names);
                context.Reply(writer.CreateMessage());
            }
            finally { writer.Dispose(); }
            return;
        }

        private void GetGroupProperties(MethodContext context)
        {
            var reader = context.Request.GetBodyReader();
            var ids = reader.ReadArrayOfInt32();
            var names = reader.ReadArrayOfString();
            if (ids.Length > 256 || names.Length > 64) { Invalid(context); return; }
            if (ids.Length == 0) ids = [0, 1, 2, 3];
            using var writer = context.CreateReplyWriter("a(ia{sv})");
            var start = writer.WriteArrayStart(DBusType.Struct);
            foreach (var id in ids.Where(id => id is >= 0 and <= 3))
            { writer.WriteStructureStart(); writer.WriteInt32(id); writer.WriteDictionary(Filter(NodeProperties(id, tray.snapshot), names)); }
            writer.WriteArrayEnd(start); context.Reply(writer.CreateMessage()); return;
        }

        private void GetProperty(MethodContext context)
        {
            var reader = context.Request.GetBodyReader();
            var id = reader.ReadInt32(); var name = reader.ReadString();
            if (id is < 0 or > 3 || !NodeProperties(id, tray.snapshot).TryGetValue(name, out var value)) { Invalid(context); return; }
            using var writer = context.CreateReplyWriter("v"); writer.WriteVariant(value); context.Reply(writer.CreateMessage()); return;
        }

        private void Event(MethodContext context)
        {
            var reader = context.Request.GetBodyReader();
            var id = reader.ReadInt32(); var name = reader.ReadString();
            _ = reader.ReadVariantValue(); _ = reader.ReadUInt32();
            tray.Click(id, name); Empty(context); return;
        }

        private void EventGroup(MethodContext context)
        {
            var reader = context.Request.GetBodyReader();
            var events = new List<(int Id, string Name)>();
            var end = reader.ReadArrayStart(DBusType.Struct);
            while (reader.HasNext(end))
            {
                if (events.Count >= 64) { Invalid(context); return; }
                reader.AlignStruct();
                var id = reader.ReadInt32(); var name = reader.ReadString();
                _ = reader.ReadVariantValue(); _ = reader.ReadUInt32();
                events.Add((id, name));
            }
            foreach (var entry in events) tray.Click(entry.Id, entry.Name);
            using var writer = context.CreateReplyWriter("ai");
            writer.WriteArray(events.Where(entry => entry.Id is < 0 or > 3).Select(entry => entry.Id).ToArray());
            context.Reply(writer.CreateMessage()); return;
        }

        private void AboutToShow(MethodContext context)
        {
            using var writer = context.CreateReplyWriter("b"); writer.WriteBool(false); context.Reply(writer.CreateMessage()); return;
        }

        private void AboutToShowGroup(MethodContext context)
        {
            var reader = context.Request.GetBodyReader();
            var ids = reader.ReadArrayOfInt32();
            if (ids.Length > 256) { Invalid(context); return; }
            using var writer = context.CreateReplyWriter("aiai");
            writer.WriteArray(System.Array.Empty<int>()); writer.WriteArray(ids.Where(id => id is < 0 or > 3).ToArray());
            context.Reply(writer.CreateMessage()); return;
        }

        private static void Empty(MethodContext context) { using var writer = context.CreateReplyWriter(null); context.Reply(writer.CreateMessage()); }
        private static void Invalid(MethodContext context) => context.ReplyError("org.freedesktop.DBus.Error.InvalidArgs", "Unknown or oversized tray-menu request.");
    }

    private static MessageBuffer Call(DBusConnection connection, string destination, string path, string @interface, string member, string argument)
    {
        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(destination: destination, path: path, @interface: @interface, member: member, signature: "s");
        writer.WriteString(argument); return writer.CreateMessage();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true; connection?.Dispose(); connection = null;
        lifetime.Source.Cancel();
        Disposal = DisposeRegistrationAsync();
    }

    internal Task Disposal { get; private set; } = Task.CompletedTask;
    private async Task DisposeRegistrationAsync()
    {
        // Cancel queued registrations, then wait for the current owner before
        // disposing its semaphore. Never block the native UI while doing this.
        await registration.WaitAsync();
        connection?.Dispose(); connection = null;
        registration.Dispose(); lifetime.Source.Dispose();
    }
    private static (CancellationTokenSource, CancellationToken) NewLifetime()
    { var source = new CancellationTokenSource(); return (source, source.Token); }

    private const string ItemXml = """
        <node><interface name="org.kde.StatusNotifierItem">
        <method name="Activate"><arg type="i" direction="in"/><arg type="i" direction="in"/></method>
        <method name="SecondaryActivate"><arg type="i" direction="in"/><arg type="i" direction="in"/></method>
        <method name="ContextMenu"><arg type="i" direction="in"/><arg type="i" direction="in"/></method>
        <method name="Scroll"><arg type="i" direction="in"/><arg type="s" direction="in"/></method>
        <property name="Title" type="s" access="read"/><property name="Id" type="s" access="read"/>
        <property name="Category" type="s" access="read"/><property name="Status" type="s" access="read"/>
        <property name="WindowId" type="i" access="read"/><property name="IconThemePath" type="s" access="read"/>
        <property name="Menu" type="o" access="read"/><property name="ItemIsMenu" type="b" access="read"/>
        <property name="IconName" type="s" access="read"/><property name="IconPixmap" type="a(iiay)" access="read"/>
        <property name="ToolTip" type="(sa(iiay)ss)" access="read"/>
        <property name="OverlayIconName" type="s" access="read"/><property name="OverlayIconPixmap" type="a(iiay)" access="read"/>
        <property name="AttentionIconName" type="s" access="read"/><property name="AttentionIconPixmap" type="a(iiay)" access="read"/>
        <property name="AttentionMovieName" type="s" access="read"/>
        <signal name="NewTitle"/><signal name="NewToolTip"/><signal name="NewIcon"/>
        <signal name="NewAttentionIcon"/><signal name="NewOverlayIcon"/><signal name="NewStatus"><arg type="s"/></signal>
        </interface></node>
        """;
    private const string MenuXml = """
        <node><interface name="com.canonical.dbusmenu">
        <method name="GetLayout"><arg type="i" direction="in"/><arg type="i" direction="in"/><arg type="as" direction="in"/><arg type="u" direction="out"/><arg type="(ia{sv}av)" direction="out"/></method>
        <method name="GetGroupProperties"><arg type="ai" direction="in"/><arg type="as" direction="in"/><arg type="a(ia{sv})" direction="out"/></method>
        <method name="GetProperty"><arg type="i" direction="in"/><arg type="s" direction="in"/><arg type="v" direction="out"/></method>
        <method name="Event"><arg type="i" direction="in"/><arg type="s" direction="in"/><arg type="v" direction="in"/><arg type="u" direction="in"/></method>
        <method name="AboutToShow"><arg type="i" direction="in"/><arg type="b" direction="out"/></method>
        <method name="EventGroup"><arg type="a(isvu)" direction="in"/><arg type="ai" direction="out"/></method>
        <method name="AboutToShowGroup"><arg type="ai" direction="in"/><arg type="ai" direction="out"/><arg type="ai" direction="out"/></method>
        <property name="Version" type="u" access="read"/><property name="TextDirection" type="s" access="read"/>
        <property name="Status" type="s" access="read"/><property name="IconThemePath" type="as" access="read"/>
        <signal name="LayoutUpdated"><arg type="u"/><arg type="i"/></signal>
        </interface></node>
        """;
}
