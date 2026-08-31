using System.Diagnostics;

namespace HeroVoiceFilterEditor.Services;

public static class FileExplorer
{
    /// Opens a folder in the OS file browser, creating it first if it doesn't exist yet
    /// (e.g. the workspace before the first extraction).
    public static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);

        if (OperatingSystem.IsWindows())
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        else if (OperatingSystem.IsMacOS())
            Process.Start(new ProcessStartInfo("open", $"\"{path}\"") { UseShellExecute = true });
        else
            Process.Start(new ProcessStartInfo("xdg-open", $"\"{path}\"") { UseShellExecute = true });
    }
}
