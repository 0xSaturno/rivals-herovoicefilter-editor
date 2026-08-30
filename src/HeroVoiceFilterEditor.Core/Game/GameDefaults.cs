namespace HeroVoiceFilterEditor.Core.Game;

public static class GameDefaults
{
    public const string AesKey = "0C263D8C22DCB085894899C3A3796383E9BF9DE0CBFB08C9BF2DEF2E84F29D74";

    public const string SteamAppRelativePath = "steamapps/common/MarvelRivals/MarvelGame/Marvel/Content/Paks";

    public const string GlobalContainerName = "global.utoc";

    public static string NormalizeAesKey(string? key)
    {
        string trimmed = (key ?? AesKey).Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[2..];
        return trimmed;
    }
}
