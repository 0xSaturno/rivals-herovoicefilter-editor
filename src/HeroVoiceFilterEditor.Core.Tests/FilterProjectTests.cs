using HeroVoiceFilterEditor.Core.Project;
using Xunit;

namespace HeroVoiceFilterEditor.Core.Tests;

public class FilterProjectTests
{
    [Fact]
    public void SaveThenLoad_RoundTripsUpsertAndRemoveEntries()
    {
        string path = Path.GetTempFileName();
        try
        {
            var project = new FilterProject
            {
                AuthoredAgainstBuild = "3805839",
                Entries =
                {
                    new ProjectEntry
                    {
                        SkinId = 1015503,
                        Op = FilterOp.Upsert,
                        Slots = ["/Game/x/effect_a", null, null, null],
                        BaseSlots = [null, null, null, null]
                    },
                    new ProjectEntry
                    {
                        SkinId = 1016501,
                        Op = FilterOp.Remove,
                        BaseSlots = ["/Game/x/effect_b", null, null, null]
                    }
                }
            };

            project.Save(path);
            FilterProject loaded = FilterProject.Load(path);

            Assert.Equal(2, loaded.Entries.Count);
            Assert.Equal("3805839", loaded.AuthoredAgainstBuild);

            ProjectEntry upsert = loaded.Entries.Single(e => e.SkinId == 1015503);
            Assert.Equal("/Game/x/effect_a", upsert.DesiredSlots[0]);
            Assert.Null(upsert.DesiredSlots[1]);

            ProjectEntry remove = loaded.Entries.Single(e => e.SkinId == 1016501);
            Assert.Equal(FilterOp.Remove, remove.Op);
            Assert.Equal("/Game/x/effect_b", remove.BaseSlots?[0]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Save_OmitsSlotsForARemoveEntry()
    {
        // A removal has nothing to apply, so the file should not carry a pointless
        // all-null Slots array — it is easy for a hand-edited .rhvfp to look wrong otherwise.
        string path = Path.GetTempFileName();
        try
        {
            var project = new FilterProject
            {
                Entries = { new ProjectEntry { SkinId = 1, Op = FilterOp.Remove, Slots = [null, null, null, null] } }
            };
            project.Save(path);

            string json = File.ReadAllText(path);
            Assert.DoesNotContain("\"Slots\"", json);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DesiredSlots_IsAllNone_WhenSlotsWasNeverSet()
    {
        var entry = new ProjectEntry { SkinId = 1, Op = FilterOp.Remove };
        Assert.All(entry.DesiredSlots, Assert.Null);
    }

    [Fact]
    public void Load_RejectsASchemaNewerThanThisBuildSupports()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, """{"Schema":99,"Entries":[]}""");
            Assert.Throws<InvalidDataException>(() => FilterProject.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_PadsAShortSlotsArray_ToTheFixedSlotCount()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, """
                { "Schema": 1, "Entries": [ { "SkinId": 1, "Op": "Upsert", "Slots": ["/Game/x/a"] } ] }
                """);

            FilterProject loaded = FilterProject.Load(path);
            Assert.Equal(4, loaded.Entries[0].DesiredSlots.Length);
            Assert.Equal("/Game/x/a", loaded.Entries[0].DesiredSlots[0]);
            Assert.Null(loaded.Entries[0].DesiredSlots[3]);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
