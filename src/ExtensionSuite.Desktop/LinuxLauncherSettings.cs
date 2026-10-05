using System.Diagnostics;

namespace ExtensionSuite.Desktop;

public enum LinuxRunnerKind { Wine, Umu }

// Nonsensitive selections only. Desktop capabilities belong to redirected pipes,
// never this record, process arguments or saved launcher settings.
public sealed record LinuxLauncherSettings(LinuxRunnerKind Runner, string RunnerPath,
    string PrefixDirectory, string ApplicationPath, string DataDirectory, string? ProtonDirectory = null)
{
    public LinuxLauncherSettings Normalize(bool createNewProfile = false)
    {
        if (!Enum.IsDefined(Runner)) throw new ArgumentException("Choose Wine or Proton (UMU).");
        var runner = Absolute(RunnerPath, "Choose the installed Wine or UMU program.");
        var expected = Runner == LinuxRunnerKind.Wine ? new[] { "wine", "wine64" } : new[] { "umu-run" };
        if (!File.Exists(runner) || !expected.Contains(Path.GetFileName(runner), StringComparer.Ordinal))
            throw new ArgumentException("Choose the installed wine, wine64 or umu-run program for this runner.");
        var application = Absolute(ApplicationPath, "Choose TDSBLive.exe in the extracted Windows app folder.");
        if (!File.Exists(application) || !Path.GetFileName(application).Equals("TDSBLive.exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Choose TDSBLive.exe in the fully extracted Windows app folder.");
        var prefix = Absolute(PrefixDirectory, "Choose the folder holding your Windows app settings.");
        var data = Absolute(DataDirectory, "Choose the TDSBLive setup you want to keep.");
        if (!createNewProfile && !Directory.Exists(Path.Combine(prefix, "drive_c")))
            throw new ArgumentException("This Windows settings folder is missing. Choose your existing folder; a new folder starts an empty setup.");
        if (!createNewProfile && !IsProfile(data))
            throw new ArgumentException("This TDSBLive setup is missing. Choose the existing setup instead of starting an empty one.");
        if (createNewProfile && Directory.Exists(data) && Directory.EnumerateFileSystemEntries(data).Any())
            throw new ArgumentException("That folder already contains files. Choose the existing setup or a separate empty folder.");
        var proton = string.IsNullOrWhiteSpace(ProtonDirectory) ? null : Absolute(ProtonDirectory, "Choose your installed Proton folder.");
        if (Runner == LinuxRunnerKind.Umu && (proton is null || !File.Exists(Path.Combine(proton, "proton")) ||
            !File.Exists(Path.Combine(proton, "toolmanifest.vdf"))))
            throw new ArgumentException("Choose the installed Proton folder containing proton and toolmanifest.vdf.");
        var normalized = this with { RunnerPath = runner, ApplicationPath = application, PrefixDirectory = prefix,
            DataDirectory = data, ProtonDirectory = proton };
        _ = normalized.WindowsDataDirectory();
        return normalized;
    }

    public ProcessStartInfo CreateStartInfo(bool createNewProfile = false)
    {
        var settings = Normalize(createNewProfile);
        var info = new ProcessStartInfo(settings.RunnerPath)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(settings.ApplicationPath)!
        };
        info.ArgumentList.Add(settings.ApplicationPath);
        info.ArgumentList.Add("--TDSBLive:DesktopMode=external");
        info.ArgumentList.Add("--TDSBLive:OpenEditor=false");
        info.ArgumentList.Add("--TDSBLive:DataDirectory=" + settings.WindowsDataDirectory());
        info.Environment["WINEPREFIX"] = settings.PrefixDirectory;
        // An inherited loader/server can belong to another runner. These choices
        // apply only to this child; no prefix registry or user's environment changes.
        foreach (var name in new[] { "WINELOADER", "WINESERVER", "WINEARCH" }) info.Environment.Remove(name);
        info.Environment["WINEDEBUG"] = "-all";
        info.Environment.TryGetValue("WINEDLLOVERRIDES", out var overrides);
        info.Environment["WINEDLLOVERRIDES"] = BackendOverrides(overrides);
        if (settings.Runner == LinuxRunnerKind.Umu)
        {
            info.Environment["GAMEID"] = "0";
            info.Environment["PROTONPATH"] = settings.ProtonDirectory;
            info.Environment["PROTON_VERB"] = "waitforexitandrun";
            info.Environment.Remove("STORE");
        }
        return info;
    }

    public string WindowsDataDirectory()
    {
        var drive = Path.GetFullPath(Path.Combine(PrefixDirectory, "drive_c"));
        var data = Path.GetFullPath(DataDirectory);
        if (data.Equals(drive, StringComparison.Ordinal))
            throw new ArgumentException("Choose the TDSBLive setup folder, not the whole Windows drive.");
        if (Within(data, drive)) return WindowsPath('C', Path.GetRelativePath(drive, data));
        var mappings = Path.Combine(PrefixDirectory, "dosdevices");
        if (Directory.Exists(mappings))
        {
            foreach (var mapping in Directory.EnumerateFileSystemEntries(mappings).Order(StringComparer.Ordinal))
            {
                var name = Path.GetFileName(mapping);
                if (name.Length != 2 || name[1] != ':' || name[0] is < 'a' or > 'z') continue;
                var target = new DirectoryInfo(mapping).ResolveLinkTarget(true);
                if (target is not null && Within(data, target.FullName))
                    return WindowsPath(char.ToUpperInvariant(name[0]), Path.GetRelativePath(target.FullName, data));
            }
        }
        throw new ArgumentException("This setup folder is not available inside the selected Windows settings folder. Choose its existing mapped setup folder.");
    }

    public static IReadOnlyList<string> FindProfiles(string prefixDirectory)
    {
        var drive = Path.Combine(Absolute(prefixDirectory, "Choose a Windows settings folder."), "drive_c");
        var candidates = new List<string> { Path.Combine(drive, "TDSBLiveData") };
        var users = Path.Combine(drive, "users");
        if (Directory.Exists(users))
            candidates.AddRange(Directory.EnumerateDirectories(users).Take(256)
                .Select(user => Path.Combine(user, "AppData", "Local", "TDSBLive")));
        return candidates.Where(IsProfile).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    internal static string BackendOverrides(string? inherited)
    {
        var retained = new List<string>();
        foreach (var entry in (inherited ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pieces = entry.Split('=', 2);
            if (pieces.Length != 2) { retained.Add(entry); continue; }
            var names = pieces[0].Split(',').Where(name => name.Trim().TrimStart('*').ToLowerInvariant() is not ("mscoree" or "mshtml"));
            var remaining = string.Join(',', names);
            if (remaining.Length > 0) retained.Add(remaining + "=" + pieces[1]);
        }
        retained.Add("mscoree=b");
        retained.Add("mshtml=");
        return string.Join(';', retained);
    }

    private static bool IsProfile(string path) => Directory.Exists(path) &&
        (File.Exists(Path.Combine(path, "tdsblive.db")) || File.Exists(Path.Combine(path, "configuration.json")));

    private static string Absolute(string path, string explanation)
    {
        if (string.IsNullOrWhiteSpace(path) || path.IndexOfAny(new[] { '\0', '\r', '\n' }) >= 0 || !Path.IsPathFullyQualified(path))
            throw new ArgumentException(explanation);
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    private static bool Within(string path, string root)
    {
        var directory = Path.GetFullPath(root);
        if (!Path.EndsInDirectorySeparator(directory)) directory += Path.DirectorySeparatorChar;
        return path.StartsWith(directory, StringComparison.Ordinal);
    }

    private static string WindowsPath(char drive, string relative)
    {
        if (relative.Split(Path.DirectorySeparatorChar).Any(segment => segment.IndexOfAny(new[] { ':', '\\', '"', '<', '>', '|', '?', '*' }) >= 0))
            throw new ArgumentException("Choose a setup folder whose name is supported by Windows.");
        return drive + ":\\" + relative.Replace(Path.DirectorySeparatorChar, '\\');
    }
}
