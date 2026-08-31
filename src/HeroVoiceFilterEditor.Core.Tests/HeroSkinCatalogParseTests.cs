using HeroVoiceFilterEditor.Core.Metadata;
using Xunit;

namespace HeroVoiceFilterEditor.Core.Tests;

/// Every quirk here was observed in the live MarvelRivalsCharacterIDs.md file, not invented —
/// see PLAN.md Phase 4 for where each one came from.
public class HeroSkinCatalogParseTests
{
    private const string Sample = """
        # Marvel Rivals Character IDs

        |  ID  | NAME | SKIN IDs | SKIN NAMES
        | :--: | :--: | :--: | :--: |
        | 1014 | Punisher | 1014100 | Camo |
        | | | 1014501 | Punisher 2099 |
        | 1057 | Deadpool | 1057100 | X-FORCE? |
        | 4071 | God Of Stories | | Path To Doomsday Announcer | |
        | 4084 | UltronTrackedBomber | | |
        | ???? | Upcoming Characters | | |
        | ???? | Gorr The God Butcher |
        | 1069 | | | |
        | 1057 | Professor X (Old) | | |
        """;

    private readonly HeroSkinCatalog _catalog = HeroSkinCatalog.Parse(Sample);

    [Fact]
    public void Parse_ReadsOrdinarySkinRows()
    {
        HeroSkin? skin = _catalog.FindSkin(1014501);
        Assert.NotNull(skin);
        Assert.Equal("Punisher 2099", skin!.SkinName);
        Assert.Equal("Punisher", skin.HeroName);
    }

    [Fact]
    public void Parse_InheritsHeroFromBlankLeadingCells()
    {
        // "| | | 1014501 | Punisher 2099 |" carries no hero id of its own — it belongs to
        // whichever hero row came before it.
        HeroSkin skin = _catalog.FindSkin(1014501)!;
        Assert.Equal(1014, skin.HeroId);
        Assert.Equal("Punisher", skin.HeroName);
    }

    [Fact]
    public void Parse_SkipsPlaceholderHeroIds()
    {
        Assert.DoesNotContain(_catalog.Heroes, h => h.HeroName == "Upcoming Characters");
        Assert.DoesNotContain(_catalog.Heroes, h => h.HeroName == "Gorr The God Butcher");
    }

    [Fact]
    public void Parse_SkipsARowWithASkinNameButNoSkinId()
    {
        // The row itself is unusable, but the hero still gets its synthesized Default skin.
        Hero? hero = _catalog.FindHero(4071);
        Assert.NotNull(hero);
        HeroSkin skin = Assert.Single(hero!.Skins);
        Assert.Equal("Default", skin.SkinName);
    }

    [Fact]
    public void Parse_AllowsAHeroWithNoSkinsAtAll()
    {
        // "No skins listed" still means the base costume exists — only the alt skins are absent.
        Hero? hero = _catalog.FindHero(4084);
        Assert.NotNull(hero);
        HeroSkin skin = Assert.Single(hero!.Skins);
        Assert.Equal("Default", skin.SkinName);
        Assert.Equal(4084001, skin.SkinId);
    }

    [Fact]
    public void Parse_FallsBackToAPlaceholderName_ForABlankHeroName()
    {
        Hero? hero = _catalog.FindHero(1069);
        Assert.NotNull(hero);
        Assert.Equal("Hero 1069", hero!.HeroName);
    }

    [Fact]
    public void Parse_MergesReassignedHeroIds_KeepingTheNonEmptyName()
    {
        // The file reuses 1057 for a historical "(Old)" entry; the current owner (Deadpool,
        // which has skins) must win, not whichever row happened to parse last.
        Hero? hero = _catalog.FindHero(1057);
        Assert.NotNull(hero);
        Assert.Equal("Deadpool", hero!.HeroName);
        Assert.Equal(2, hero.Skins.Count); // synthesized Default + the listed 1057100
        Assert.Contains(hero.Skins, s => s.SkinId == 1057100);
    }

    [Fact]
    public void Parse_ProducesNoDuplicateHeroIds()
    {
        List<int> ids = _catalog.Heroes.Select(h => h.HeroId).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void Describe_FallsBackToTheHeroName_ForAnUnlistedSkin()
    {
        // 1014999 is not in the sample at all, but its leading digits name a known hero.
        Assert.StartsWith("Punisher", _catalog.Describe(1014999));
    }

    [Fact]
    public void Describe_FallsBackToABareNumber_ForACompletelyUnknownSkin()
    {
        Assert.Equal("skin 9999999", _catalog.Describe(9999999));
    }

    [Theory]
    [InlineData(1014501, 1014)]
    [InlineData(1057100, 1057)]
    [InlineData(999, 0)]
    public void HeroIdOf_ReadsTheLeadingFourDigits(int skinId, int expectedHeroId) =>
        Assert.Equal(expectedHeroId, HeroSkinCatalog.HeroIdOf(skinId));

    [Fact]
    public void Parse_SynthesizesADefaultSkin_ForEveryHero()
    {
        // The community markdown never lists the base costume (heroId + "001") at all —
        // confirmed against the live file, zero matches — so it must be added, not parsed.
        HeroSkin? punisherDefault = _catalog.FindSkin(1014001);
        Assert.NotNull(punisherDefault);
        Assert.Equal("Default", punisherDefault!.SkinName);
        Assert.Equal("Punisher", punisherDefault.HeroName);
    }

    [Fact]
    public void Parse_PutsTheDefaultSkinFirst_InAHerosSkinList()
    {
        Hero hero = _catalog.FindHero(1014)!;
        Assert.Equal("Default", hero.Skins[0].SkinName);
    }

    [Fact]
    public void Describe_RendersTheDefaultSkinIdAsDefault_NotABareNumber()
    {
        Assert.Equal("Punisher — Default", _catalog.Describe(1014001));
    }

    [Fact]
    public void DefaultSkinId_IsHeroIdFollowedBy001()
    {
        Assert.Equal(1014001, HeroSkinCatalog.DefaultSkinId(1014));
    }

    [Fact]
    public void Parse_OfEmptyMarkdown_ProducesAnEmptyUsableCatalog()
    {
        HeroSkinCatalog empty = HeroSkinCatalog.Parse(string.Empty);
        Assert.Empty(empty.Heroes);
        Assert.Equal(0, empty.SkinCount);
    }
}
