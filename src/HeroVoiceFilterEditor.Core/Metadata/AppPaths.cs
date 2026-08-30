namespace HeroVoiceFilterEditor.Core.Metadata;

public static class AppPaths
{
    public const string AppFolderName = "HeroVoiceFilterEditor";

    public static string ConfigDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppFolderName);

    public static string CacheDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolderName);

    public static string ConfigFile => Path.Combine(ConfigDirectory, "config.json");

    public static string UsmapCacheDirectory => Path.Combine(CacheDirectory, "usmap");

    public static string MetadataCacheDirectory => Path.Combine(CacheDirectory, "metadata");

    public static string DefaultWorkspaceDirectory => Path.Combine(CacheDirectory, "workspace");

    public static string Ensure(string directory)
    {
        Directory.CreateDirectory(directory);
        return directory;
    }
}
