using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace ExtensionSuite.Desktop;

// The pinned Avalonia Win32 implementation owns one notification icon (ID 1)
// on its AvaloniaMessageWindow. Query the shell, rather than treating creation
// of a managed TrayIcon as proof that Explorer registered it. Qualification must
// recheck this adapter when updating Avalonia.
[SupportedOSPlatform("windows")]
internal static partial class WindowsTrayRegistration
{
    public static bool IsShellAvailable() => FindWindow("Shell_TrayWnd", null) != IntPtr.Zero;

    public static bool IsAvailable()
    {
        // Published Windows packages are x64; the flags-zero probe below uses
        // the documented x64 NOTIFYICONDATAW layout.
        if (IntPtr.Size != 8) return false;
        var available = false;
        EnumWindows((window, parameter) =>
        {
            if (GetWindowThreadProcessId(window, out var processId) == 0) return true;
            if (processId != (uint)Environment.ProcessId) return true;
            var name = new StringBuilder(256);
            if (GetClassName(window, name, name.Capacity) == 0) return true;
            if (!name.ToString().StartsWith("AvaloniaMessageWindow ", StringComparison.Ordinal)) return true;
            var identifier = new IconIdentifier { Size = (uint)Marshal.SizeOf<IconIdentifier>(), Window = window, Id = 1 };
            available = Shell_NotifyIconGetRect(ref identifier, out _) == 0;
            if (!available)
            {
                // A location query is not a registration query. Ask Explorer
                // to acknowledge this existing HWND/ID with no valid fields
                // to change. This cannot add an icon or alter its appearance.
                var probe = new NotifyIconProbe { Size = 976, Window = window, Id = 1 };
                available = Shell_NotifyIconW(1, ref probe); // NIM_MODIFY, uFlags=0.
            }
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
    [StructLayout(LayoutKind.Explicit, Size = 976)]
    private struct NotifyIconProbe
    {
        [FieldOffset(0)] public uint Size;
        [FieldOffset(8)] public IntPtr Window;
        [FieldOffset(16)] public uint Id;
        // uFlags at offset 20 and all remaining bytes stay zero.
    }
    private delegate bool WindowVisitor(IntPtr window, IntPtr parameter);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumWindows(WindowVisitor visitor, IntPtr parameter);
    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, StringBuilder name, int maximum);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string className, string? windowName);
    [LibraryImport("shell32.dll")]
    private static partial int Shell_NotifyIconGetRect(ref IconIdentifier identifier, out Rectangle rectangle);
    [LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool Shell_NotifyIconW(uint message, ref NotifyIconProbe data);
}
