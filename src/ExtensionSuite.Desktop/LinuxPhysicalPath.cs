namespace ExtensionSuite.Desktop;

// Wine and Proton can expose one profile through prefix/user directory aliases.
// Resolve directory links without modifying those links or the saved selections.
internal static class LinuxPhysicalPath
{
    public static string Resolve(string path)
    {
        var absolute = Path.GetFullPath(path);
        if (!OperatingSystem.IsLinux()) return absolute;
        var remaining = 64;
        return ResolveLinks(absolute, ref remaining);
    }

    private static string ResolveLinks(string path, ref int remaining)
    {
        var current = Path.GetPathRoot(path)!;
        foreach (var part in path[current.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(current, part);
            var target = new DirectoryInfo(candidate).LinkTarget;
            if (target is null) current = candidate;
            else
            {
                if (--remaining < 0) throw new IOException("The Windows settings folder contains too many directory links. Choose its original folder.");
                current = ResolveLinks(Path.GetFullPath(target, current), ref remaining);
            }
        }
        return Path.TrimEndingDirectorySeparator(current);
    }
}
