using UAssetTool.ZenPackage;

namespace HeroVoiceFilterEditor.Core.Game;

public static class WorkspaceExtractor
{
    public static string SnapshotRootFor(string workspaceRoot, string build) =>
        Path.Combine(workspaceRoot, "vanilla", build);

    public static VanillaSnapshot? LoadExisting(string workspaceRoot, string build) =>
        VanillaSnapshot.Load(SnapshotRootFor(workspaceRoot, build));

    /// Converts the winning MarvelHeroVoiceData chunk to legacy .uasset/.uexp under the workspace.
    public static VanillaSnapshot ExtractVoiceData(
        GameContainerSet containers,
        string workspaceRoot,
        IProgress<string>? log = null)
    {
        ulong packageId = containers.ResolvePackage(VoiceDataPaths.VoiceDataPackage)
            ?? throw new FileNotFoundException($"Package not found in any container: {VoiceDataPaths.VoiceDataPackage}");

        string sourceContainer = containers.ContainerNameFor(packageId);
        log?.Report($"Found {VoiceDataPaths.VoiceDataPackage} in {sourceContainer}");

        var converter = new ZenToLegacyConverter(containers.Context, packageId);
        LegacyAssetBundle bundle = converter.Convert();

        string relativeAssetPath = containers.Context.GetContainerPath(packageId) ?? VoiceDataPaths.VoiceDataContainerPath;
        string snapshotRoot = SnapshotRootFor(workspaceRoot, containers.Build);
        string assetPath = Path.Combine(snapshotRoot, relativeAssetPath.Replace('/', Path.DirectorySeparatorChar));

        Directory.CreateDirectory(Path.GetDirectoryName(assetPath)!);
        File.WriteAllBytes(assetPath, bundle.AssetData);
        File.WriteAllBytes(Path.ChangeExtension(assetPath, ".uexp"), bundle.ExportsData);

        var snapshot = new VanillaSnapshot
        {
            Build = containers.Build,
            SourceContainer = sourceContainer,
            PackagePath = VoiceDataPaths.VoiceDataPackage,
            RelativeAssetPath = relativeAssetPath,
            ExtractedUtc = DateTimeOffset.UtcNow,
            SnapshotRoot = snapshotRoot
        };
        snapshot.Save();

        log?.Report($"Extracted {bundle.AssetData.Length} B uasset + {bundle.ExportsData.Length} B uexp to {assetPath}");
        return snapshot;
    }
}
