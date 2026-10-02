using System.ComponentModel;
using System.Diagnostics;

namespace ExtensionSuite.Host;

public static class ApplicationRelauncher
{
    public static bool TryStart(string executable, string assembly, string dataDirectory,
        bool openEditor, Func<ProcessStartInfo, Process?>? start = null)
    {
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = OperatingSystem.IsWindows() };
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            info.ArgumentList.Add(assembly);
        info.ArgumentList.Add("--TDSBLive:DataDirectory");
        info.ArgumentList.Add(dataDirectory);
        info.ArgumentList.Add("--TDSBLive:OpenEditor=" + openEditor.ToString().ToLowerInvariant());
        try
        {
            using var process = (start ?? Process.Start)(info);
            return process is not null;
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException) { return false; }
    }
}
