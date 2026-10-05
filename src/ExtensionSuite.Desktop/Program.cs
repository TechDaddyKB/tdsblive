using Avalonia;

namespace ExtensionSuite.Desktop;

internal static class Program
{
    public static string[] Arguments { get; private set; } = [];

    [STAThread]
    public static int Main(string[] args)
    {
        Arguments = args;
        // Stable X11 on Linux, including XWayland; no native Wayland opt-in.
        return AppBuilder.Configure<DesktopApp>().UsePlatformDetect().WithInterFont().StartWithClassicDesktopLifetime(args);
    }
}
