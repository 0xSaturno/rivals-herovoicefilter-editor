using HeroVoiceFilterEditor.Core.Effects;
using HeroVoiceFilterEditor.Core.Table;

namespace HeroVoiceFilterEditor.Core.Project;

public enum ReplayStatus
{
    /// Entry updated to the project's slots.
    Applied,

    /// Entry did not exist in vanilla and was created.
    Added,

    /// Vanilla already holds exactly what the project asks for.
    AlreadyMatches,

    /// Vanilla no longer matches what this edit was authored against.
    Conflict,

    /// An effect the project references is not in the game any more.
    MissingEffect,

    /// Entry removed as the project asks.
    Removed,

    /// A removal targets an entry vanilla no longer has.
    RemoveTargetMissing
}

public enum ConflictPolicy
{
    /// Leave conflicting entries as vanilla has them and report.
    Skip,

    /// Overwrite conflicting entries with the project's slots.
    Overwrite
}

public sealed record ReplayItem(int SkinId, ReplayStatus Status, string Detail)
{
    public bool NeedsAttention => Status is ReplayStatus.Conflict or ReplayStatus.MissingEffect or ReplayStatus.RemoveTargetMissing;
}

public sealed record ReplayReport(IReadOnlyList<ReplayItem> Items)
{
    public int Count(ReplayStatus status) => Items.Count(i => i.Status == status);

    public IEnumerable<ReplayItem> Attention => Items.Where(i => i.NeedsAttention);

    public bool HasProblems => Items.Any(i => i.NeedsAttention);

    public string Summary =>
        $"{Count(ReplayStatus.Applied)} applied, {Count(ReplayStatus.Added)} added, " +
        $"{Count(ReplayStatus.Removed)} removed, {Count(ReplayStatus.AlreadyMatches)} already current, " +
        $"{Count(ReplayStatus.Conflict)} conflicts, {Count(ReplayStatus.MissingEffect)} missing effects";
}

/// Replays a project's edits onto a freshly extracted vanilla table.
public static class ReplayEngine
{
    public static ReplayReport Apply(
        VoiceDataDocument document,
        FilterProject project,
        EffectCatalog catalog,
        ConflictPolicy policy = ConflictPolicy.Skip)
    {
        var items = new List<ReplayItem>();

        foreach (ProjectEntry entry in project.Entries)
        {
            items.Add(entry.Op == FilterOp.Remove
                ? ApplyRemove(document, entry)
                : ApplyUpsert(document, entry, catalog, policy));
        }

        return new ReplayReport(items);
    }

    private static ReplayItem ApplyRemove(VoiceDataDocument document, ProjectEntry entry)
    {
        if (document.Find(entry.SkinId) is null)
            return new ReplayItem(entry.SkinId, ReplayStatus.RemoveTargetMissing, "vanilla no longer has this entry");

        document.RemoveEntry(entry.SkinId);
        return new ReplayItem(entry.SkinId, ReplayStatus.Removed, "entry removed");
    }

    private static ReplayItem ApplyUpsert(
        VoiceDataDocument document,
        ProjectEntry entry,
        EffectCatalog catalog,
        ConflictPolicy policy)
    {
        string?[] desired = entry.DesiredSlots;

        string[] missing = desired
            .Where(s => !string.IsNullOrEmpty(s) && !catalog.Contains(s!))
            .Select(s => s!)
            .ToArray();

        if (missing.Length > 0)
            return new ReplayItem(entry.SkinId, ReplayStatus.MissingEffect, $"not in the game: {string.Join(", ", missing.Select(LeafOf))}");

        SkinBusEntry? current = document.Find(entry.SkinId);

        if (current is not null && SlotsMatch(current, desired))
            return new ReplayItem(entry.SkinId, ReplayStatus.AlreadyMatches, "vanilla already matches");

        if (current is not null && entry.BaseSlots is not null && !SlotsMatch(current, entry.BaseSlots))
        {
            string detail = $"vanilla now has [{Describe(current)}], edit was authored against [{Describe(entry.BaseSlots)}]";

            if (policy == ConflictPolicy.Skip)
                return new ReplayItem(entry.SkinId, ReplayStatus.Conflict, detail + " — left as vanilla");

            Write(document, current, entry, catalog);
            return new ReplayItem(entry.SkinId, ReplayStatus.Conflict, detail + " — overwritten");
        }

        bool added = current is null;
        current ??= document.AddEntry(entry.SkinId);
        Write(document, current, entry, catalog);

        return added
            ? new ReplayItem(entry.SkinId, ReplayStatus.Added, $"created with [{Describe(desired)}]")
            : new ReplayItem(entry.SkinId, ReplayStatus.Applied, $"set to [{Describe(desired)}]");
    }

    private static void Write(VoiceDataDocument document, SkinBusEntry target, ProjectEntry entry, EffectCatalog catalog)
    {
        string?[] desired = entry.DesiredSlots;

        for (int i = 0; i < VoiceDataPaths.SlotCount; i++)
        {
            string? path = desired[i];
            target.Slots[i] = string.IsNullOrEmpty(path) ? null : catalog.Find(path) ?? new EffectReference(path);
        }
    }

    private static bool SlotsMatch(SkinBusEntry entry, string?[] slots)
    {
        for (int i = 0; i < VoiceDataPaths.SlotCount; i++)
        {
            string? expected = string.IsNullOrEmpty(slots[i]) ? null : slots[i];
            if (entry.Slots[i]?.PackagePath != expected)
                return false;
        }

        return true;
    }

    private static string Describe(SkinBusEntry entry) =>
        string.Join(", ", entry.Slots.Select(s => s?.ObjectName ?? "None"));

    private static string Describe(string?[] slots) =>
        string.Join(", ", slots.Select(s => string.IsNullOrEmpty(s) ? "None" : LeafOf(s)));

    private static string LeafOf(string packagePath) => packagePath[(packagePath.LastIndexOf('/') + 1)..];
}
