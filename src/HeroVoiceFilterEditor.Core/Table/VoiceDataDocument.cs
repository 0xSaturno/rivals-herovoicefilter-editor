using HeroVoiceFilterEditor.Core.Effects;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;

namespace HeroVoiceFilterEditor.Core.Table;

/// The SkinBusEffects map of a MarvelHeroVoiceData asset, exposed as a flat editable list.
/// Everything else in the asset is carried through untouched.
public sealed class VoiceDataDocument
{
    private readonly UAsset _asset;
    private readonly MapPropertyData _skinBusEffects;
    private readonly Dictionary<int, (IntPropertyData Key, StructPropertyData Value)> _originalPairs = new();
    private readonly IntPropertyData _keyTemplate;
    private readonly StructPropertyData _valueTemplate;
    private readonly List<SkinBusEntry> _entries = new();

    private VoiceDataDocument(UAsset asset, MapPropertyData skinBusEffects, string assetPath)
    {
        _asset = asset;
        _skinBusEffects = skinBusEffects;
        AssetPath = assetPath;
        Imports = new ImportResolver(asset);

        foreach (KeyValuePair<PropertyData, PropertyData> pair in skinBusEffects.Value)
        {
            if (pair.Key is not IntPropertyData key || pair.Value is not StructPropertyData value)
                continue;

            _originalPairs[key.Value] = (key, value);
            _entries.Add(new SkinBusEntry(key.Value, ReadSlots(value)));
        }

        if (_originalPairs.Count == 0)
            throw new InvalidDataException($"{VoiceDataPaths.SkinBusEffectsProperty} has no usable entries.");

        (_keyTemplate, _valueTemplate) = _originalPairs.Values.First();
        Baseline = _entries.ToDictionary(e => e.SkinId, e => e.Clone());
    }

    /// State as loaded, kept so edits can be diffed into a project and shown as modified.
    public IReadOnlyDictionary<int, SkinBusEntry> Baseline { get; }

    public bool IsAdded(SkinBusEntry entry) => !Baseline.ContainsKey(entry.SkinId);

    public bool IsModified(SkinBusEntry entry) =>
        !Baseline.TryGetValue(entry.SkinId, out SkinBusEntry? original) || !entry.SlotsEqual(original);

    public IEnumerable<SkinBusEntry> ModifiedEntries => _entries.Where(IsModified);

    public IEnumerable<int> RemovedSkinIds =>
        Baseline.Keys.Where(id => _entries.All(e => e.SkinId != id));

    public string AssetPath { get; }

    public ImportResolver Imports { get; }

    public IReadOnlyList<SkinBusEntry> Entries => _entries;

    public static VoiceDataDocument Load(string assetPath, Usmap mappings)
    {
        var asset = new UAsset(assetPath, EngineVersion.VER_UE5_3, mappings);

        NormalExport export = asset.Exports.OfType<NormalExport>().FirstOrDefault(e =>
            string.Equals(ImportResolver.NameText(e.ObjectName), VoiceDataPaths.VoiceDataExportName, StringComparison.Ordinal))
            ?? throw new InvalidDataException($"Export {VoiceDataPaths.VoiceDataExportName} not found in {assetPath}");

        MapPropertyData map = export.Data.OfType<MapPropertyData>().FirstOrDefault(p =>
            string.Equals(ImportResolver.NameText(p.Name), VoiceDataPaths.SkinBusEffectsProperty, StringComparison.Ordinal))
            ?? throw new InvalidDataException($"Property {VoiceDataPaths.SkinBusEffectsProperty} not found in {assetPath}");

        return new VoiceDataDocument(asset, map, assetPath);
    }

    public SkinBusEntry? Find(int skinId) => _entries.FirstOrDefault(e => e.SkinId == skinId);

    public SkinBusEntry AddEntry(int skinId)
    {
        if (Find(skinId) is not null)
            throw new InvalidOperationException($"Skin {skinId} already has an entry.");

        var entry = new SkinBusEntry(skinId);
        _entries.Add(entry);
        return entry;
    }

    public bool RemoveEntry(int skinId)
    {
        SkinBusEntry? entry = Find(skinId);
        return entry is not null && _entries.Remove(entry);
    }

    /// Rebuilds the map from Entries, reusing the original property objects wherever a skin id
    /// already existed, so saving without edits reproduces the source bytes exactly.
    public void Save(string outputPath)
    {
        var rebuilt = new TMap<PropertyData, PropertyData>();

        foreach (SkinBusEntry entry in _entries)
        {
            bool isNew = !_originalPairs.ContainsKey(entry.SkinId);
            (IntPropertyData key, StructPropertyData value) = MaterializePair(entry);
            WriteSlots(entry, value, forceRegenerateHeader: isNew);
            rebuilt.Add(key, value);
        }

        _skinBusEffects.Value = rebuilt;

        string? directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        _asset.Write(outputPath);
    }

    private (IntPropertyData Key, StructPropertyData Value) MaterializePair(SkinBusEntry entry)
    {
        if (_originalPairs.TryGetValue(entry.SkinId, out (IntPropertyData Key, StructPropertyData Value) existing))
            return existing;

        var key = (IntPropertyData)_keyTemplate.Clone();
        key.Value = entry.SkinId;
        key.IsZero = false;

        return (key, (StructPropertyData)_valueTemplate.Clone());
    }

    private IEnumerable<EffectReference?> ReadSlots(StructPropertyData slots)
    {
        for (int i = 0; i < VoiceDataPaths.SlotCount; i++)
            yield return Imports.Describe(FindSlot(slots, i)?.Value);
    }

    private void WriteSlots(SkinBusEntry entry, StructPropertyData slots, bool forceRegenerateHeader = false)
    {
        bool changed = forceRegenerateHeader;

        for (int i = 0; i < VoiceDataPaths.SlotCount; i++)
        {
            ObjectPropertyData? slot = FindSlot(slots, i);
            if (slot is null)
                continue;

            EffectReference? effect = entry.Slots[i];
            FPackageIndex resolved = Imports.Resolve(effect);
            bool nowZero = effect is null;

            if (slot.IsZero != nowZero || (slot.Value?.Index ?? 0) != resolved.Index)
                changed = true;

            slot.Value = resolved;

            // A zero property is omitted from the body and flagged in the unversioned header
            // instead, so this must track the value or the write silently drops it.
            slot.IsZero = nowZero;
        }

        // UAssetAPI replays the header captured at read time, which still encodes the old
        // zero-mask. Dropping it on touched structs only keeps untouched ones byte-exact.
        if (changed)
            slots._originalStructHeader = null;
    }

    private static ObjectPropertyData? FindSlot(StructPropertyData slots, int index)
    {
        string name = VoiceDataPaths.SlotPropertyName(index);
        return slots.Value?.OfType<ObjectPropertyData>().FirstOrDefault(p =>
            string.Equals(ImportResolver.NameText(p.Name), name, StringComparison.Ordinal));
    }
}
