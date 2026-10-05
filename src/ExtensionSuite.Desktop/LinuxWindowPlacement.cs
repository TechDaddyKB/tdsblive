using Avalonia;
using Avalonia.Controls;

namespace ExtensionSuite.Desktop;

internal static class LinuxWindowPlacement
{
    public static void Fit(Window window)
    {
        var screen = window.Screens.ScreenFromWindow(window) ?? window.Screens.Primary;
        if (screen is null) return;
        var area = screen.WorkingArea;
        var scaling = screen.Scaling;
        window.MaxWidth = Math.Max(1, area.Width / scaling - 24);
        window.MaxHeight = Math.Max(1, area.Height / scaling - 48);
        window.MinWidth = Math.Min(window.MinWidth, window.MaxWidth);
        window.MinHeight = Math.Min(window.MinHeight, window.MaxHeight);
        // Explicit setup sizes shrink; auto-sized recovery content remains
        // constrained by MaxHeight and can scroll inside its viewport.
        if (double.IsFinite(window.Width)) window.Width = Math.Min(window.Width, window.MaxWidth);
        if (double.IsFinite(window.Height)) window.Height = Math.Min(window.Height, window.MaxHeight);
        var width = Math.Min(double.IsFinite(window.Width) ? window.Width : window.Bounds.Width, window.MaxWidth);
        var height = Math.Min(double.IsFinite(window.Height) ? window.Height : window.Bounds.Height, window.MaxHeight);
        window.Position = new PixelPoint(area.X + Math.Max(0, (area.Width - (int)Math.Ceiling(width * scaling)) / 2),
            area.Y + Math.Max(0, (area.Height - (int)Math.Ceiling(height * scaling)) / 2));
    }
}
