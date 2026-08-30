using System.Text.RegularExpressions;

namespace HeroVoiceFilterEditor.Core.Game;

/// Best-effort Steam autodetect. The settings screen always allows a manual override.
public static partial class GameLocator
{
    [GeneratedRegex("\"path\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex LibraryPathPattern();

    public static bool IsPaksDirectory(string? path) =>
        !string.IsNullOrWhiteSpace(path) && File.Exists(Path.Combine(path, GameDefaults.GlobalContainerName));

    public static IReadOnlyList<string> FindCandidates()
    {
        var found = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string library in EnumerateSteamLibraries())
        {
            string paks = Path.Combine(library, GameDefaults.SteamAppRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (IsPaksDirectory(paks) && seen.Add(Path.GetFullPath(paks)))
                found.Add(Path.GetFullPath(paks));
        }

        return found;
    }

    private static IEnumerable<string> EnumerateSteamLibraries()
    {
        var libraries = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string root in EnumerateSteamRoots())
        {
            if (seen.Add(root))
                libraries.Add(root);

            foreach (string vdf in new[]
                     {
                         Path.Combine(root, "steamapps", "libraryfolders.vdf"),
                         Path.Combine(root, "config", "libraryfolders.vdf")
                     })
            {
                if (!File.Exists(vdf))
                    continue;

                foreach (string library in ParseLibraryFolders(vdf))
                {
                    if (seen.Add(library))
                        libraries.Add(library);
                }
            }
        }

        return libraries;
    }

    private static IEnumerable<string> ParseLibraryFolders(string vdfPath)
    {
        string text;
        try
        {
            text = File.ReadAllText(vdfPath);
        }
        catch (IOException)
        {
            yield break;
        }

        foreach (Match match in LibraryPathPattern().Matches(text))
        {
            string path = match.Groups[1].Value.Replace("\\\\", "\\");
            if (Directory.Exists(path))
                yield return path;
        }
    }

    private static IEnumerable<string> EnumerateSteamRoots()
    {
        foreach (string variable in new[] { "ProgramFiles(x86)", "ProgramFiles", "ProgramW6432" })
        {
            string? baseDir = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrEmpty(baseDir))
            {
                string candidate = Path.Combine(baseDir, "Steam");
                if (Directory.Exists(candidate))
                    yield return candidate;
            }
        }

        foreach (DriveInfo drive in SafeDrives())
        {
            foreach (string relative in new[] { "Steam", "SteamLibrary", Path.Combine("Games", "SteamLibrary"), Path.Combine("Games", "Steam") })
            {
                string candidate = Path.Combine(drive.RootDirectory.FullName, relative);
                if (Directory.Exists(candidate))
                    yield return candidate;
            }
        }
    }

    private static IEnumerable<DriveInfo> SafeDrives()
    {
        DriveInfo[] drives;
        try
        {
            drives = DriveInfo.GetDrives();
        }
        catch (IOException)
        {
            yield break;
        }

        foreach (DriveInfo drive in drives)
        {
            bool usable;
            try
            {
                usable = drive.IsReady && drive.DriveType is DriveType.Fixed or DriveType.Removable;
            }
            catch (IOException)
            {
                usable = false;
            }

            if (usable)
                yield return drive;
        }
    }
}
