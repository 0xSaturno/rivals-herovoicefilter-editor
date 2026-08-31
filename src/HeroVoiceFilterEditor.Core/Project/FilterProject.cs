using System.Text.Json;
using System.Text.Json.Serialization;
using HeroVoiceFilterEditor.Core.Table;

namespace HeroVoiceFilterEditor.Core.Project;

[JsonConverter(typeof(JsonStringEnumConverter<FilterOp>))]
public enum FilterOp
{
    Upsert,
    Remove
}

public sealed class ProjectEntry
{
    public int SkinId { get; set; }

    public FilterOp Op { get; set; } = FilterOp.Upsert;

    /// Desired slot contents as effect package paths, null meaning None. Omitted for removals.
    public string?[]? Slots { get; set; }

    [JsonIgnore]
    public string?[] DesiredSlots => Slots ?? new string?[VoiceDataPaths.SlotCount];

    /// What vanilla held when this edit was authored, so a later replay can tell
    /// "already applied" apart from "the game changed underneath us".
    public string?[]? BaseSlots { get; set; }

    public void Normalize()
    {
        Slots = Op == FilterOp.Remove ? null : Resize(Slots) ?? new string?[VoiceDataPaths.SlotCount];
        BaseSlots = Resize(BaseSlots);
    }

    private static string?[]? Resize(string?[]? slots)
    {
        if (slots is null)
            return null;
        if (slots.Length == VoiceDataPaths.SlotCount)
            return slots;

        var sized = new string?[VoiceDataPaths.SlotCount];
        Array.Copy(slots, sized, Math.Min(slots.Length, VoiceDataPaths.SlotCount));
        return sized;
    }
}

/// A .rhvfp project: only the edits, keyed by effect package path so it survives the
/// import renumbering that every game patch causes.
public sealed class FilterProject
{
    public const string Extension = ".rhvfp";
    public const int CurrentSchema = 1;

    public int Schema { get; set; } = CurrentSchema;

    public string? AuthoredAgainstBuild { get; set; }

    public DateTimeOffset? SavedUtc { get; set; }

    public List<ProjectEntry> Entries { get; set; } = new();

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static FilterProject FromDocument(VoiceDataDocument document, string? build)
    {
        var project = new FilterProject { AuthoredAgainstBuild = build };

        foreach (SkinBusEntry entry in document.Entries.Where(document.IsModified))
        {
            document.Baseline.TryGetValue(entry.SkinId, out SkinBusEntry? original);
            project.Entries.Add(new ProjectEntry
            {
                SkinId = entry.SkinId,
                Op = FilterOp.Upsert,
                Slots = entry.Slots.Select(s => s?.PackagePath).ToArray(),
                BaseSlots = original?.Slots.Select(s => s?.PackagePath).ToArray()
            });
        }

        foreach (int removed in document.RemovedSkinIds)
        {
            project.Entries.Add(new ProjectEntry
            {
                SkinId = removed,
                Op = FilterOp.Remove,
                Slots = null,
                BaseSlots = document.Baseline[removed].Slots.Select(s => s?.PackagePath).ToArray()
            });
        }

        return project;
    }

    public void Save(string path)
    {
        SavedUtc = DateTimeOffset.UtcNow;

        // Enforce the Remove-has-no-Slots invariant here too, not just on Load, so it holds
        // regardless of how the entries were built rather than trusting every call site.
        foreach (ProjectEntry entry in Entries)
            entry.Normalize();

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(path, JsonSerializer.Serialize(this, SerializerOptions));
    }

    public static FilterProject Load(string path)
    {
        FilterProject project = JsonSerializer.Deserialize<FilterProject>(File.ReadAllText(path), SerializerOptions)
            ?? throw new InvalidDataException($"Not a readable {Extension} project: {path}");

        if (project.Schema > CurrentSchema)
            throw new InvalidDataException($"Project schema {project.Schema} is newer than this build supports ({CurrentSchema}).");

        foreach (ProjectEntry entry in project.Entries)
            entry.Normalize();

        return project;
    }
}
