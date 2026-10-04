using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace ExtensionSuite.Desktop;

// The pinned Avalonia Win32 implementation owns one notification icon (ID 1)
// on its AvaloniaMessageWindow. Query the shell, rather than treating creation
// of a managed TrayIcon as proof that Explorer registered it. Qualification must
// recheck this adapter when updating Avalonia.
[SupportedOSPlatform("windows")]
internal static class WindowsTrayRegistration
{
    public static bool IsAvailable()
    {
        var available = false;
        EnumWindows((window, parameter) =>
        {
            GetWindowThreadProcessId(window, out var processId);
            if (processId != (uint)Environment.ProcessId) return true;
            var name = new StringBuilder(256);
            GetClassName(window, name, name.Capacity);
            if (!name.ToString().StartsWith("AvaloniaMessageWindow ", StringComparison.Ordinal)) return true;
            var identifier = new IconIdentifier { Size = (uint)Marshal.SizeOf<IconIdentifier>(), Window = window, Id = 1 };
            available = Shell_NotifyIconGetRect(ref identifier, out _) == 0;
            return !available;
        }, IntPtr.Zero);
        return available;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IconIdentifier
    {
        public uint Size;
        public IntPtr Window;
        public uint Id;
        public Guid Guid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rectangle { public int Left, Top, Right, Bottom; }
    private delegate bool WindowVisitor(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(WindowVisitor visitor, IntPtr parameter);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, StringBuilder name, int maximum);
    [DllImport("shell32.dll")]
    private static extern int Shell_NotifyIconGetRect(ref IconIdentifier identifier, out Rectangle rectangle);
}
