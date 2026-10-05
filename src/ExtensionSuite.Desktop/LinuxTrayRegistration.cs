using Tmds.DBus.Protocol;

namespace ExtensionSuite.Desktop;

public sealed record LinuxTrayState(bool HostAvailable, int OwnedItems)
{
    public bool Registered => HostAvailable && OwnedItems == 1;
    public static LinuxTrayState Unavailable { get; } = new(false, 0);
}

public static class LinuxTrayRegistration
{
    private const string Watcher = "org.kde.StatusNotifierWatcher";
    private const string WatcherPath = "/StatusNotifierWatcher";
    private const string ItemInterface = "org.kde.StatusNotifierItem";

    public static Task<LinuxTrayState> ReadAsync(CancellationToken cancellationToken) =>
        ReadAsync(Environment.ProcessId, cancellationToken);

    internal static async Task<LinuxTrayState> ReadAsync(int processId, CancellationToken cancellationToken, string? address = null)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            address ??= DBusAddress.Session;
            if (string.IsNullOrWhiteSpace(address)) return LinuxTrayState.Unavailable;
            using var connection = new DBusConnection(address);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(2));
            await connection.ConnectAsync().AsTask().WaitAsync(deadline.Token);
            var owner = await NameOwnerAsync(connection, Watcher).WaitAsync(deadline.Token);
            var hosted = await connection.CallMethodAsync(Property(connection, owner, WatcherPath, Watcher,
                "IsStatusNotifierHostRegistered"), static (message, _) =>
                {
                    var reader = message.GetBodyReader(); reader.ReadSignature("b"); return reader.ReadBool();
                }).WaitAsync(deadline.Token);
            if (!hosted) return LinuxTrayState.Unavailable;
            var items = await connection.CallMethodAsync(Property(connection, owner, WatcherPath, Watcher,
                "RegisteredStatusNotifierItems"), static (message, _) =>
                {
                    var reader = message.GetBodyReader(); reader.ReadSignature("as"); return reader.ReadArrayOfString();
                }).WaitAsync(deadline.Token);
            if (items.Length > 1024) return LinuxTrayState.Unavailable;
            var owned = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                if (!TryItemAddress(item, out var service, out var path)) continue;
                try
                {
                    // A service name containing our PID can be claimed by another
                    // app. Ask the bus which process actually owns its connection.
                    var itemOwner = await NameOwnerAsync(connection, service).WaitAsync(deadline.Token);
                    if (owned.Contains(itemOwner + path)) continue;
                    var pid = await connection.CallMethodAsync(BusMethod(connection, "GetConnectionUnixProcessID", itemOwner),
                        static (message, _) => message.GetBodyReader().ReadUInt32()).WaitAsync(deadline.Token);
                    if (pid != processId) continue;
                    var title = await StringPropertyAsync(connection, itemOwner, path, "Title").WaitAsync(deadline.Token);
                    var status = await StringPropertyAsync(connection, itemOwner, path, "Status").WaitAsync(deadline.Token);
                    if (title.StartsWith("TDSBLive — ", StringComparison.Ordinal) && status == "Active")
                        owned.Add(itemOwner + path);
                }
                catch (DBusErrorReplyException)
                {
                    // A watcher can briefly retain an item whose owner disappeared.
                    // Skip that stale item; never treat its old name as registration.
                }
            }
            // Do not report an old watcher's list after its bus name changes hands.
            if (await NameOwnerAsync(connection, Watcher).WaitAsync(deadline.Token) != owner)
                return LinuxTrayState.Unavailable;
            return new(true, owned.Count);
        }
        catch (Exception error) when (error is DBusExceptionBase or
            IOException or InvalidOperationException or ArgumentException or OperationCanceledException)
        {
            return LinuxTrayState.Unavailable;
        }
        // Disposing only this read-only connection also settles timed-out calls.
        // It never disconnects Avalonia's exporter or restarts the desktop service.
    }

    internal static bool TryItemAddress(string item, out string service, out string path)
    {
        service = path = "";
        if (string.IsNullOrWhiteSpace(item) || item.Length > 512) return false;
        var slash = item.IndexOf('/');
        service = slash < 0 ? item : item[..slash];
        path = slash < 0 ? "/StatusNotifierItem" : item[slash..];
        return path == "/StatusNotifierItem" && service.Length is > 0 and <= 255 &&
            (service.StartsWith(':') || service.StartsWith("org.kde.StatusNotifierItem-", StringComparison.Ordinal));
    }

    private static Task<string> NameOwnerAsync(DBusConnection connection, string name) => connection.CallMethodAsync(
        BusMethod(connection, "GetNameOwner", name), static (message, _) => message.GetBodyReader().ReadString());

    private static Task<string> StringPropertyAsync(DBusConnection connection, string destination, string path, string property) =>
        connection.CallMethodAsync(Property(connection, destination, path, ItemInterface, property), static (message, _) =>
        {
            var reader = message.GetBodyReader(); reader.ReadSignature("s"); return reader.ReadString();
        });

    private static MessageBuffer BusMethod(DBusConnection connection, string member, string name)
    {
        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(destination: "org.freedesktop.DBus", path: "/org/freedesktop/DBus",
            @interface: "org.freedesktop.DBus", member: member, signature: "s");
        writer.WriteString(name); return writer.CreateMessage();
    }

    private static MessageBuffer Property(DBusConnection connection, string destination, string path, string @interface, string property)
    {
        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(destination: destination, path: path, @interface: "org.freedesktop.DBus.Properties", member: "Get", signature: "ss");
        writer.WriteString(@interface); writer.WriteString(property); return writer.CreateMessage();
    }
}
