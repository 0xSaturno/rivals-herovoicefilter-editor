using HeroVoiceFilterEditor.Core.Effects;
using HeroVoiceFilterEditor.Core.Table;
using Xunit;

namespace HeroVoiceFilterEditor.Core.Tests;

/// Exercises VoiceDataDocument against the real, live-extracted MarvelHeroVoiceData table.
/// Skips (does not fail) when the game is not installed on the machine running the suite.
[Collection(GameEnvironmentCollection.Name)]
public class VoiceDataDocumentTests
{
    private readonly GameEnvironmentFixture _env;

    public VoiceDataDocumentTests(GameEnvironmentFixture env) => _env = env;

    private VoiceDataDocument LoadVanilla()
    {
        _env.RequireAvailable();
        return VoiceDataDocument.Load(_env.Snapshot.AssetPath, _env.Mappings);
    }

    [SkippableFact]
    public void Load_ParsesEverySkinBusEffectsEntry()
    {
        VoiceDataDocument doc = LoadVanilla();
        Assert.NotEmpty(doc.Entries);
        Assert.All(doc.Entries, e => Assert.InRange(e.SkinId, 1, int.MaxValue));
    }

    [SkippableFact]
    public void Load_ReadsFourSlotNameSuffixesDistinctly()
    {
        // Regression: reading FName.Value instead of FName.ToString() collapsed
        // effect_vo_x_slot_0/_1/_2 onto the same key, since UE folds a canonical trailing
        // _N into FName.Number rather than storing it in the string.
        VoiceDataDocument doc = LoadVanilla();

        SkinBusEntry? withThreeDistinctSlots = doc.Entries.FirstOrDefault(e =>
            e.Slots.Count(s => s is not null) >= 3 &&
            e.Slots.Where(s => s is not null).Select(s => s!.ObjectName).Distinct().Count() >= 3);

        Assert.NotNull(withThreeDistinctSlots);
        List<string> names = withThreeDistinctSlots!.Slots.Where(s => s is not null).Select(s => s!.ObjectName).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [SkippableFact]
    public void Save_WithoutEdits_ReproducesTheSourceBytesExactly()
    {
        VoiceDataDocument doc = LoadVanilla();
        string outPath = Path.Combine(Path.GetTempPath(), $"hvfe-roundtrip-{Guid.NewGuid():N}.uasset");
        try
        {
            doc.Save(outPath);

            AssertSameBytes(_env.Snapshot.AssetPath, outPath);
            AssertSameBytes(Path.ChangeExtension(_env.Snapshot.AssetPath, ".uexp"), Path.ChangeExtension(outPath, ".uexp"));
            Assert.Equal(0, doc.Imports.AppendedImportCount);
        }
        finally
        {
            CleanUp(outPath);
        }
    }

    [SkippableFact]
    public void EditingASlot_ThenReloading_ReadsBackTheSameEffect()
    {
        VoiceDataDocument doc = LoadVanilla();
        SkinBusEntry target = doc.Entries.First();
        EffectReference replacement = _env.Effects.Effects.First(e => e != target.Slots[0]);

        target.Slots[0] = replacement;

        string outPath = Path.Combine(Path.GetTempPath(), $"hvfe-edit-{Guid.NewGuid():N}.uasset");
        try
        {
            doc.Save(outPath);
            VoiceDataDocument reloaded = VoiceDataDocument.Load(outPath, _env.Mappings);

            Assert.Equal(replacement.PackagePath, reloaded.Find(target.SkinId)!.Slots[0]!.PackagePath);
            Assert.Equal(doc.Entries.Count, reloaded.Entries.Count);
        }
        finally
        {
            CleanUp(outPath);
        }
    }

    [SkippableFact]
    public void AddingAnEntry_ThenReloading_HasAllFourSlotsAndDoesNotDisturbOthers()
    {
        VoiceDataDocument doc = LoadVanilla();
        int newSkinId = doc.Entries.Select(e => e.SkinId).Max() + 12345;
        EffectFamily family = _env.Effects.Families.First(f => f.Members.Count == 4);

        SkinBusEntry added = doc.AddEntry(newSkinId);
        for (int i = 0; i < 4; i++)
            added.Slots[i] = family.Members[i];

        SkinBusEntry untouchedBefore = doc.Entries.First(e => e.SkinId != newSkinId);
        var untouchedSnapshot = untouchedBefore.Slots.Select(s => s?.PackagePath).ToArray();

        string outPath = Path.Combine(Path.GetTempPath(), $"hvfe-add-{Guid.NewGuid():N}.uasset");
        try
        {
            doc.Save(outPath);
            VoiceDataDocument reloaded = VoiceDataDocument.Load(outPath, _env.Mappings);

            SkinBusEntry? newEntry = reloaded.Find(newSkinId);
            Assert.NotNull(newEntry);
            Assert.Equal(4, newEntry!.FilledSlotCount);

            SkinBusEntry stillThere = reloaded.Find(untouchedBefore.SkinId)!;
            Assert.Equal(untouchedSnapshot, stillThere.Slots.Select(s => s?.PackagePath).ToArray());
        }
        finally
        {
            CleanUp(outPath);
        }
    }

    [SkippableFact]
    public void ClearingASlot_ThenReloading_ReadsBackAsNone()
    {
        VoiceDataDocument doc = LoadVanilla();
        SkinBusEntry target = doc.Entries.First(e => e.Slots[0] is not null);
        target.Slots[0] = null;

        string outPath = Path.Combine(Path.GetTempPath(), $"hvfe-clear-{Guid.NewGuid():N}.uasset");
        try
        {
            doc.Save(outPath);
            VoiceDataDocument reloaded = VoiceDataDocument.Load(outPath, _env.Mappings);
            Assert.Null(reloaded.Find(target.SkinId)!.Slots[0]);
        }
        finally
        {
            CleanUp(outPath);
        }
    }

    [SkippableFact]
    public void RemovingAnEntry_ThenReloading_DropsExactlyThatEntry()
    {
        VoiceDataDocument doc = LoadVanilla();
        int originalCount = doc.Entries.Count;
        int removedId = doc.Entries.First().SkinId;

        Assert.True(doc.RemoveEntry(removedId));

        string outPath = Path.Combine(Path.GetTempPath(), $"hvfe-remove-{Guid.NewGuid():N}.uasset");
        try
        {
            doc.Save(outPath);
            VoiceDataDocument reloaded = VoiceDataDocument.Load(outPath, _env.Mappings);

            Assert.Equal(originalCount - 1, reloaded.Entries.Count);
            Assert.Null(reloaded.Find(removedId));
        }
        finally
        {
            CleanUp(outPath);
        }
    }

    [SkippableFact]
    public void AnEffectNotYetImported_AppendsExactlyTwoImports_AndResolvesBackCorrectly()
    {
        VoiceDataDocument doc = LoadVanilla();
        var novel = new EffectReference("/Game/Marvel/Wwise/Assets/Effects/effect_vo/effect_vo_test_probe/effect_vo_test_probe_slot_0");
        doc.Entries.First().Slots[1] = novel;

        string outPath = Path.Combine(Path.GetTempPath(), $"hvfe-novel-{Guid.NewGuid():N}.uasset");
        try
        {
            doc.Save(outPath);
            Assert.Equal(2, doc.Imports.AppendedImportCount);

            VoiceDataDocument reloaded = VoiceDataDocument.Load(outPath, _env.Mappings);
            Assert.Equal(novel.PackagePath, reloaded.Entries.First().Slots[1]!.PackagePath);
        }
        finally
        {
            CleanUp(outPath);
        }
    }

    [SkippableFact]
    public void Baseline_TracksAddedAndModifiedEntries()
    {
        VoiceDataDocument doc = LoadVanilla();
        SkinBusEntry existing = doc.Entries.First();
        SkinBusEntry untouched = doc.Entries.Skip(1).First();

        existing.Slots[0] = existing.Slots[0] is null
            ? _env.Effects.Effects.First()
            : null;

        SkinBusEntry added = doc.AddEntry(doc.Entries.Select(e => e.SkinId).Max() + 54321);

        Assert.True(doc.IsModified(existing));
        Assert.False(doc.IsModified(untouched));
        Assert.True(doc.IsAdded(added));
        Assert.False(doc.IsAdded(existing));
    }

    [SkippableFact]
    public void ReferenceComparison_MatchesTheKnownGoodExtraction()
    {
        const string reference = @"B:\MRivalsMods\Coding\UAssetToolTUI\extracted\Marvel\Content\Marvel\Audio\Voice\MarvelHeroVoiceData.uasset";
        Skip.IfNot(File.Exists(reference), "Reference extraction not present on this machine.");

        VoiceDataDocument fromReference = VoiceDataDocument.Load(reference, _env.Mappings);
        VoiceDataDocument fromLiveExtraction = LoadVanilla();

        // The reference copy was pulled at a different moment than whatever build is
        // currently live, so this compares structure, not raw bytes: every entry the
        // known-good copy has must still resolve to the same slot contents today.
        foreach (SkinBusEntry expected in fromReference.Entries)
        {
            SkinBusEntry? actual = fromLiveExtraction.Find(expected.SkinId);
            if (actual is null)
                continue; // the game may have dropped or renumbered a skin since the reference was taken

            Assert.Equal(
                expected.Slots.Select(s => s?.ObjectName).ToArray(),
                actual.Slots.Select(s => s?.ObjectName).ToArray());
        }
    }

    private static void AssertSameBytes(string expectedPath, string actualPath)
    {
        byte[] expected = File.ReadAllBytes(expectedPath);
        byte[] actual = File.ReadAllBytes(actualPath);
        Assert.True(expected.AsSpan().SequenceEqual(actual),
            $"{Path.GetFileName(actualPath)}: expected {expected.Length} bytes, got {actual.Length}, first difference at " +
            $"{FirstDifference(expected, actual)}");
    }

    private static int FirstDifference(byte[] a, byte[] b)
    {
        int limit = Math.Min(a.Length, b.Length);
        for (int i = 0; i < limit; i++)
        {
            if (a[i] != b[i])
                return i;
        }
        return limit;
    }

    private static void CleanUp(string uassetPath)
    {
        File.Delete(uassetPath);
        string uexp = Path.ChangeExtension(uassetPath, ".uexp");
        if (File.Exists(uexp))
            File.Delete(uexp);
    }
}
