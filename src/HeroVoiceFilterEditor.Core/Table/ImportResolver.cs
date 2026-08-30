using HeroVoiceFilterEditor.Core.Effects;
using UAssetAPI;
using UAssetAPI.UnrealTypes;

namespace HeroVoiceFilterEditor.Core.Table;

/// Maps between effect package paths and the import-table entries the slots point at.
/// New effects append a Package import plus an AkEffectShareSet import that outers to it.
public sealed class ImportResolver
{
    private readonly UAsset _asset;
    private readonly Dictionary<string, FPackageIndex> _byPackagePath = new(StringComparer.OrdinalIgnoreCase);

    public ImportResolver(UAsset asset)
    {
        _asset = asset;

        for (int i = 0; i < asset.Imports.Count; i++)
        {
            Import import = asset.Imports[i];
            if (!string.Equals(NameText(import.ClassName), VoiceDataPaths.EffectClassName, StringComparison.Ordinal))
                continue;

            string? packagePath = OuterPackagePath(import);
            if (packagePath is not null)
                _byPackagePath[packagePath] = FPackageIndex.FromImport(i);
        }
    }

    public int AppendedImportCount { get; private set; }

    public EffectReference? Describe(FPackageIndex? index)
    {
        if (index is null || !index.IsImport())
            return null;

        int importIndex = -index.Index - 1;
        if (importIndex < 0 || importIndex >= _asset.Imports.Count)
            return null;

        string? packagePath = OuterPackagePath(_asset.Imports[importIndex]);
        return packagePath is null ? null : new EffectReference(packagePath);
    }

    public FPackageIndex Resolve(EffectReference? effect)
    {
        if (effect is null)
            return FPackageIndex.FromRawIndex(0);

        if (_byPackagePath.TryGetValue(effect.PackagePath, out FPackageIndex? existing))
            return existing;

        // FromString splits a canonical trailing _N into FName.Number, which is how the game
        // stores these names: the map holds "..._slot" and the number carries the slot index.
        FPackageIndex packageImport = _asset.AddImport(new Import(
            FName.FromString(_asset, VoiceDataPaths.PackageClassPackage),
            FName.FromString(_asset, VoiceDataPaths.PackageClassName),
            FPackageIndex.FromRawIndex(0),
            FName.FromString(_asset, effect.PackagePath),
            false));

        FPackageIndex objectImport = _asset.AddImport(new Import(
            FName.FromString(_asset, VoiceDataPaths.EffectClassPackage),
            FName.FromString(_asset, VoiceDataPaths.EffectClassName),
            packageImport,
            FName.FromString(_asset, effect.ObjectName),
            false));

        AppendedImportCount += 2;
        _byPackagePath[effect.PackagePath] = objectImport;
        return objectImport;
    }

    private string? OuterPackagePath(Import import)
    {
        FPackageIndex? outer = import.OuterIndex;
        if (outer is null || !outer.IsImport())
            return null;

        int outerIndex = -outer.Index - 1;
        if (outerIndex < 0 || outerIndex >= _asset.Imports.Count)
            return null;

        Import package = _asset.Imports[outerIndex];
        return string.Equals(NameText(package.ClassName), VoiceDataPaths.PackageClassName, StringComparison.Ordinal)
            ? NameText(package.ObjectName)
            : null;
    }

    /// ToString re-attaches the _N suffix that UE folds into FName.Number; reading Value alone
    /// would collapse effect_vo_x_slot_0/_1/_2 onto one another.
    internal static string NameText(FName? name) => name?.ToString() ?? string.Empty;
}
