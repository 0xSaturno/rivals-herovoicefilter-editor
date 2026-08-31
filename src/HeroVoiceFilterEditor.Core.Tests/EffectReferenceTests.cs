using HeroVoiceFilterEditor.Core.Effects;
using Xunit;

namespace HeroVoiceFilterEditor.Core.Tests;

public class EffectReferenceTests
{
    [Theory]
    [InlineData("effect_vo_tech_mask_01_slot_0", "effect_vo_tech_mask_01_slot", 0)]
    [InlineData("effect_vo_tech_mask_default_02_slot_2", "effect_vo_tech_mask_default_02_slot", 2)]
    [InlineData("effect_vo_symbiote_1041_0", "effect_vo_symbiote_1041", 0)]
    [InlineData("effect_vo_adam_god_04", "effect_vo_adam_god", 4)]
    public void FamilyAndOrdinal_SplitOnTheTrailingNumber(string objectName, string family, int ordinal)
    {
        var effect = new EffectReference($"/Game/Marvel/Wwise/Assets/Effects/effect_vo/x/{objectName}");
        Assert.Equal(family, effect.Family);
        Assert.Equal(ordinal, effect.Ordinal);
    }

    [Fact]
    public void ObjectName_IsTheLastPackagePathSegment()
    {
        var effect = new EffectReference("/Game/Marvel/Wwise/Assets/Effects/effect_vo/effect_vo_zombie/effect_vo_zombie_slot_0");
        Assert.Equal("effect_vo_zombie_slot_0", effect.ObjectName);
    }

    [Fact]
    public void FamilyAndOrdinal_FallBackToTheWholeName_WhenThereIsNoTrailingNumber()
    {
        var effect = new EffectReference("/Game/Marvel/Wwise/Assets/Effects/effect_vo/x/effect_vo_no_suffix");
        Assert.Equal("effect_vo_no_suffix", effect.Family);
        Assert.Equal(0, effect.Ordinal);
    }
}
