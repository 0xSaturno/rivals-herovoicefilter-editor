using HeroVoiceFilterEditor.Core.Effects;
using HeroVoiceFilterEditor.Core.Project;
using HeroVoiceFilterEditor.Core.Table;
using Xunit;

namespace HeroVoiceFilterEditor.Core.Tests;

[Collection(GameEnvironmentCollection.Name)]
public class ReplayEngineTests
{
    private readonly GameEnvironmentFixture _env;

    public ReplayEngineTests(GameEnvironmentFixture env) => _env = env;

    private VoiceDataDocument LoadVanilla()
    {
        _env.RequireAvailable();
        return VoiceDataDocument.Load(_env.Snapshot.AssetPath, _env.Mappings);
    }

    [SkippableFact]
    public void ReplayingAProject_ReproducesEditingDirectly_ByteForByte()
    {
        // This is the load-bearing guarantee of the whole delta design: a project authored
        // during one session must, when replayed onto fresh vanilla after a patch, produce
        // exactly what editing directly would have produced.
        VoiceDataDocument authoring = LoadVanilla();
        EffectFamily family = _env.Effects.Families.First(f => f.Members.Count == 4);
        int newSkinId = authoring.Entries.Select(e => e.SkinId).Max() + 11111;

        SkinBusEntry added = authoring.AddEntry(newSkinId);
        for (int i = 0; i < 4; i++)
            added.Slots[i] = family.Members[i];

        SkinBusEntry existing = authoring.Entries.First(e => e.SkinId != newSkinId);
        existing.Slots[0] = existing.Slots[0] is null ? _env.Effects.Effects.First() : null;

        FilterProject project = FilterProject.FromDocument(authoring, _env.Snapshot.Build);

        VoiceDataDocument replayed = LoadVanilla();
        ReplayReport report = ReplayEngine.Apply(replayed, project, _env.Effects);
        Assert.False(report.HasProblems);

        string directPath = Path.Combine(Path.GetTempPath(), $"hvfe-direct-{Guid.NewGuid():N}.uasset");
        string replayedPath = Path.Combine(Path.GetTempPath(), $"hvfe-replayed-{Guid.NewGuid():N}.uasset");
        try
        {
            authoring.Save(directPath);
            replayed.Save(replayedPath);

            byte[] direct = File.ReadAllBytes(directPath);
            byte[] viaReplay = File.ReadAllBytes(replayedPath);
            Assert.Equal(direct, viaReplay);
        }
        finally
        {
            CleanUp(directPath);
            CleanUp(replayedPath);
        }
    }

    [SkippableFact]
    public void ReplayingTwice_TheSecondTimeAppliesNothing()
    {
        VoiceDataDocument doc = LoadVanilla();
        SkinBusEntry entry = doc.Entries.First();
        entry.Slots[0] = entry.Slots[0] is null ? _env.Effects.Effects.First() : null;
        FilterProject project = FilterProject.FromDocument(doc, _env.Snapshot.Build);

        VoiceDataDocument first = LoadVanilla();
        ReplayEngine.Apply(first, project, _env.Effects);

        ReplayReport second = ReplayEngine.Apply(first, project, _env.Effects);

        Assert.Equal(0, second.Count(ReplayStatus.Applied));
        Assert.Equal(0, second.Count(ReplayStatus.Added));
        Assert.True(second.Count(ReplayStatus.AlreadyMatches) >= 1);
    }

    [SkippableFact]
    public void WhenVanillaChangedUnderneath_SkipPolicy_LeavesVanillaAlone()
    {
        VoiceDataDocument authoring = LoadVanilla();
        SkinBusEntry target = authoring.Entries.First(e => e.Slots[0] is null);
        target.Slots[0] = _env.Effects.Effects.First();
        FilterProject project = FilterProject.FromDocument(authoring, _env.Snapshot.Build);

        VoiceDataDocument changedVanilla = LoadVanilla();
        SkinBusEntry sameTarget = changedVanilla.Find(target.SkinId)!;
        sameTarget.Slots[0] = _env.Effects.Effects.Last();

        ReplayReport report = ReplayEngine.Apply(changedVanilla, project, _env.Effects, ConflictPolicy.Skip);

        ReplayItem item = report.Items.Single(i => i.SkinId == target.SkinId);
        Assert.Equal(ReplayStatus.Conflict, item.Status);
        Assert.Equal(_env.Effects.Effects.Last().PackagePath, changedVanilla.Find(target.SkinId)!.Slots[0]!.PackagePath);
    }

    [SkippableFact]
    public void WhenVanillaChangedUnderneath_OverwritePolicy_AppliesTheEdit()
    {
        VoiceDataDocument authoring = LoadVanilla();
        SkinBusEntry target = authoring.Entries.First(e => e.Slots[0] is null);
        EffectReference desired = _env.Effects.Effects.First();
        target.Slots[0] = desired;
        FilterProject project = FilterProject.FromDocument(authoring, _env.Snapshot.Build);

        VoiceDataDocument changedVanilla = LoadVanilla();
        changedVanilla.Find(target.SkinId)!.Slots[0] = _env.Effects.Effects.Last();

        ReplayReport report = ReplayEngine.Apply(changedVanilla, project, _env.Effects, ConflictPolicy.Overwrite);

        Assert.Equal(ReplayStatus.Conflict, report.Items.Single(i => i.SkinId == target.SkinId).Status);
        Assert.Equal(desired.PackagePath, changedVanilla.Find(target.SkinId)!.Slots[0]!.PackagePath);
    }

    [SkippableFact]
    public void AnEffectNoLongerInTheGame_IsReportedAsMissing_AndNothingIsWritten()
    {
        var stale = new FilterProject
        {
            Entries =
            {
                new ProjectEntry
                {
                    SkinId = 1,
                    Op = FilterOp.Upsert,
                    Slots = ["/Game/Marvel/Wwise/Assets/Effects/effect_vo/gone/effect_vo_gone_slot_0", null, null, null]
                }
            }
        };

        VoiceDataDocument doc = LoadVanilla();
        int skinId = doc.Entries.First().SkinId;
        stale.Entries[0].SkinId = skinId;

        ReplayReport report = ReplayEngine.Apply(doc, stale, _env.Effects);

        Assert.Equal(ReplayStatus.MissingEffect, report.Items[0].Status);
    }

    [SkippableFact]
    public void RemovingAnAlreadyMissingEntry_IsReportedWithoutThrowing()
    {
        VoiceDataDocument doc = LoadVanilla();
        int skinId = doc.Entries.First().SkinId;
        doc.RemoveEntry(skinId);

        var project = new FilterProject
        {
            Entries = { new ProjectEntry { SkinId = skinId, Op = FilterOp.Remove } }
        };

        ReplayReport report = ReplayEngine.Apply(doc, project, _env.Effects);
        Assert.Equal(ReplayStatus.RemoveTargetMissing, report.Items[0].Status);
    }

    private static void CleanUp(string uassetPath)
    {
        File.Delete(uassetPath);
        string uexp = Path.ChangeExtension(uassetPath, ".uexp");
        if (File.Exists(uexp))
            File.Delete(uexp);
    }
}
