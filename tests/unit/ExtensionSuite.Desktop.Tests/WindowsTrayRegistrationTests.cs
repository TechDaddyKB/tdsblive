using ExtensionSuite.Desktop;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Xunit;

namespace ExtensionSuite.Desktop.Tests;

public sealed class WindowsTrayRegistrationTests
{
    [WindowsOnlyFact]
    public void OtherApplicationsTrayIconsCannotBeMistakenForThisCompanion()
    {
        // This test process owns no native notification icon. A successful
        // desktop API call must still report that this process is unregistered.
        if (OperatingSystem.IsWindows()) Assert.False(WindowsTrayRegistration.IsAvailable());
    }

    [WindowsOnlyFact]
    public void OwnedMessageWindowWithoutAnIconIsNotRegisteredByTheProbe()
    {
        if (!OperatingSystem.IsWindows()) return;
        // A real owned HWND with Avalonia's class prefix must still fail when
        // no icon was added. Repeated flags-zero probes cannot create one.
        using var window = new OwnedMessageWindow();
        Assert.False(WindowsTrayRegistration.IsAvailable());
        Assert.False(WindowsTrayRegistration.IsAvailable());
    }

    private sealed class OwnedMessageWindow : IDisposable
    {
        private readonly string name = "AvaloniaMessageWindow owned-unregistered-" + Guid.NewGuid().ToString("N");
        private readonly WindowProcedure procedure = DefWindowProcW;
        private readonly IntPtr instance = GetModuleHandleW(null);
        private readonly IntPtr window;

        public OwnedMessageWindow()
        {
            var definition = new WindowClass
            {
                Size = (uint)Marshal.SizeOf<WindowClass>(), Procedure = procedure,
                Instance = instance, ClassName = name
            };
            if (RegisterClassExW(ref definition) == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            window = CreateWindowExW(0, name, "Owned registration test", 0, 0, 0, 0, 0,
                IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);
            if (window == IntPtr.Zero)
            {
                var error = Marshal.GetLastWin32Error();
                UnregisterClassW(name, instance);
                throw new Win32Exception(error);
            }
        }

        public void Dispose()
        {
            DestroyWindow(window);
            UnregisterClassW(name, instance);
            GC.KeepAlive(procedure);
        }

        private delegate IntPtr WindowProcedure(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WindowClass
        {
            public uint Size, Style;
            public WindowProcedure Procedure;
            public int ClassBytes, WindowBytes;
            public IntPtr Instance, Icon, Cursor, Background;
            public string? MenuName;
            public string ClassName;
            public IntPtr SmallIcon;
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandleW(string? module);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern ushort RegisterClassExW(ref WindowClass definition);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWindowExW(uint extendedStyle, string className, string title, uint style,
            int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyWindow(IntPtr window);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnregisterClassW(string className, IntPtr instance);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr DefWindowProcW(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    }
}

internal sealed class WindowsOnlyFactAttribute : FactAttribute
{
    public WindowsOnlyFactAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "Requires actual Windows notification-area APIs.";
    }
}
