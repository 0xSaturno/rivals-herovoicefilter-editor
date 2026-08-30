using UAssetAPI;

namespace HeroVoiceFilterEditor.Core;

public static class BackendInfo
{
    public static string UAssetApiVersion => VersionOf(typeof(UAsset));

    public static string UAssetToolVersion => VersionOf(typeof(UAssetTool.Program));

    public static string Describe() => $"UAssetAPI {UAssetApiVersion}  ·  UAssetTool {UAssetToolVersion}";

    private static string VersionOf(Type type) => type.Assembly.GetName().Version?.ToString() ?? "unknown";
}
