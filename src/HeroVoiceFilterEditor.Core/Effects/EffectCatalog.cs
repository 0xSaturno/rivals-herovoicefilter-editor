using System.Text.Json;
using HeroVoiceFilterEditor.Core.Game;

namespace HeroVoiceFilterEditor.Core.Effects;

/// Every effect_vo AkEffectShareSet the game ships, read from the container index only.
public sealed class EffectCatalog
{
    public const string FileName = "effects.json";

    private readonly Dictionary<string, EffectReference> _byPackagePath;

    private EffectCatalog(IReadOnlyList<EffectReference> effects)
    {
        Effects = effects;
        _byPackagePath = effects.ToDictionary(e => e.PackagePath, StringComparer.OrdinalIgnoreCase);
        Families = effects
            .GroupBy(e => e.Family, StringComparer.OrdinalIgnoreCase)
            .Select(g => new EffectFamily(g.Key, g.OrderBy(e => e.Ordinal).ToList()))
            .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<EffectReference> Effects { get; }

    public IReadOnlyList<EffectFamily> Families { get; }

    public EffectReference? Find(string packagePath) =>
        _byPackagePath.TryGetValue(packagePath, out EffectReference? effect) ? effect : null;

    public bool Contains(string packagePath) => _byPackagePath.ContainsKey(packagePath);

    public static EffectCatalog Build(GameContainerSet containers, IProgress<string>? log = null)
    {
        var effects = containers
            .PackagesUnder(VoiceDataPaths.EffectRootPackage)
            .Select(p => new EffectReference(p.PackagePath))
            .OrderBy(e => e.PackagePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var catalog = new EffectCatalog(effects);
        log?.Report($"Effect catalog: {catalog.Effects.Count} objects in {catalog.Families.Count} families");
        return catalog;
    }

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public void Save(string directory)
    {
        Directory.CreateDirectory(directory);
        string[] paths = Effects.Select(e => e.PackagePath).ToArray();
        File.WriteAllText(Path.Combine(directory, FileName), JsonSerializer.Serialize(paths, SerializerOptions));
    }

    public static EffectCatalog? Load(string directory)
    {
        string path = Path.Combine(directory, FileName);
        if (!File.Exists(path))
            return null;

        string[]? paths = JsonSerializer.Deserialize<string[]>(File.ReadAllText(path));
        return paths is null ? null : new EffectCatalog(paths.Select(p => new EffectReference(p)).ToList());
    }
}
