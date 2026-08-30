using System.Text.Json;
using HeroVoiceFilterEditor.Core.Game;

namespace HeroVoiceFilterEditor.Core.Metadata;

public sealed class AppSettings
{
    public string? PaksDirectory { get; set; }

    public string AesKey { get; set; } = GameDefaults.AesKey;

    public string? WorkspaceDirectory { get; set; }

    /// Set to pin a specific usmap instead of the auto-updated cache.
    public string? UsmapOverridePath { get; set; }

    public bool CheckForUpdatesOnLaunch { get; set; } = true;

    public string EffectiveWorkspace =>
        string.IsNullOrWhiteSpace(WorkspaceDirectory) ? AppPaths.DefaultWorkspaceDirectory : WorkspaceDirectory;

    /// Fills in anything not configured yet, so a first run needs no setup when Steam is present.
    public bool ApplyDefaults()
    {
        bool changed = false;

        if (!GameLocator.IsPaksDirectory(PaksDirectory))
        {
            string? detected = GameLocator.FindCandidates().FirstOrDefault();
            if (detected is not null)
            {
                PaksDirectory = detected;
                changed = true;
            }
        }

        if (string.IsNullOrWhiteSpace(AesKey))
        {
            AesKey = GameDefaults.AesKey;
            changed = true;
        }

        return changed;
    }
}

public static class SettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public static AppSettings Load(string? path = null)
    {
        string file = path ?? AppPaths.ConfigFile;

        if (!File.Exists(file))
            return new AppSettings();

        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(file)) ?? new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings, string? path = null)
    {
        string file = path ?? AppPaths.ConfigFile;
        AppPaths.Ensure(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, JsonSerializer.Serialize(settings, SerializerOptions));
    }
}
