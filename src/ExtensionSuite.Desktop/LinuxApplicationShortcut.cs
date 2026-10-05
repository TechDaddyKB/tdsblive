using System.Text;

namespace ExtensionSuite.Desktop;

internal static class LinuxApplicationShortcut
{
    internal const string FileName = "io.github.TechDaddyKB.TDSBLive.desktop";

    public static string Install(string executable, string icon, string? dataHome = null)
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Applications-menu installation requires Linux.");
        executable = ValidatePath(executable);
        icon = ValidatePath(icon);
        if (Path.GetFileName(executable) != "TDSBLive.Desktop" || !File.Exists("/usr/bin/env") || !File.Exists(executable) || !File.Exists(icon) ||
            (File.GetUnixFileMode(executable) & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) == 0)
            throw new ArgumentException("Keep the complete Linux app folder together before adding its shortcut.");
        dataHome ??= Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrWhiteSpace(dataHome) || !Path.IsPathFullyQualified(dataHome))
            dataHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        var directory = Path.Combine(ValidatePath(dataHome), "applications");
        Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var destination = Path.Combine(directory, FileName);
        var temporary = Path.Combine(directory, ".tdsblive-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, new FileStreamOptions
            {
                Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None,
                UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
            }))
            {
                // GIO resolves the executable before expanding literal %%.
                // Keep the executable name fixed and pass the original folder
                // as a directory argument. env execs the app without a shell.
                var command = "/usr/bin/env " + QuoteArgument("--chdir=" + Path.GetDirectoryName(executable)) + " ./TDSBLive.Desktop";
                var content = "[Desktop Entry]\nVersion=1.0\nType=Application\nName=TDSBLive\n" +
                    "Comment=Open your streaming editor and desktop controls\nExec=" + command +
                    "\nIcon=" + EscapeValue(icon) + "\nTerminal=false\nStartupNotify=false\nCategories=AudioVideo;\n" +
                    "Actions=Setup;\n\n[Desktop Action Setup]\nName=Change Linux setup\nExec=" + command + " --setup\n";
                stream.Write(Encoding.UTF8.GetBytes(content));
                stream.Flush(flushToDisk: true);
            }
            // Replacing a symlink replaces the shortcut itself, never its target.
            File.Move(temporary, destination, overwrite: true);
            return destination;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    internal static string QuoteArgument(string value)
    {
        var quoted = value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\u0060", "\\\u0060").Replace("$", "\\$").Replace("%", "%%");
        // Desktop string decoding precedes Exec argument decoding. Escape both
        // layers; the launcher executes the program directly, never a shell.
        return EscapeValue("\"" + quoted + "\"");
    }
    private static string EscapeValue(string value) => value.Replace("\\", "\\\\");
    private static string ValidatePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value) || value.Any(char.IsControl))
            throw new ArgumentException("Choose a complete local Linux app folder path.");
        return Path.GetFullPath(value);
    }
}
