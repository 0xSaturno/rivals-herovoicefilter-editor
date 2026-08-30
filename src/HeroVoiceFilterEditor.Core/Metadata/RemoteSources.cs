namespace HeroVoiceFilterEditor.Core.Metadata;

public static class RemoteSources
{
    public const string UsmapManifest =
        "https://raw.githubusercontent.com/SpaceDepot/rivals-depot/refs/heads/main/Mappings.json";

    public const string HeroSkinIds =
        "https://raw.githubusercontent.com/donutman07/MarvelRivalsCharacterIDs/refs/heads/main/MarvelRivalsCharacterIDs.md";

    private static readonly Lazy<HttpClient> Shared = new(() =>
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"{AppPaths.AppFolderName}/1.0");
        return client;
    });

    public static HttpClient Client => Shared.Value;
}

public enum CacheStatus
{
    /// Cache matches what the remote offers.
    UpToDate,

    /// A newer version is available remotely.
    UpdateAvailable,

    /// Freshly downloaded during this call.
    Downloaded,

    /// Remote unreachable, serving from cache.
    Offline,

    /// Remote unreachable and nothing cached.
    Unavailable
}
