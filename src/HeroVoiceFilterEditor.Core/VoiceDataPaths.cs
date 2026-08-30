namespace HeroVoiceFilterEditor.Core;

public static class VoiceDataPaths
{
    public const string VoiceDataPackage = "/Game/Marvel/Audio/Voice/MarvelHeroVoiceData";
    public const string VoiceDataContainerPath = "Marvel/Content/Marvel/Audio/Voice/MarvelHeroVoiceData.uasset";
    public const string VoiceDataExportName = "MarvelHeroVoiceData";

    public const string EffectRootPackage = "/Game/Marvel/Wwise/Assets/Effects/effect_vo";
    public const string EffectRootContainerPath = "Marvel/Content/Marvel/Wwise/Assets/Effects/effect_vo";

    public const string SkinBusEffectsProperty = "SkinBusEffects";
    public const string BusEffectSlotsStruct = "MarvelAudioBusEffectSlots";

    public const string EffectClassPackage = "/Script/AkAudio";
    public const string EffectClassName = "AkEffectShareSet";
    public const string PackageClassPackage = "/Script/CoreUObject";
    public const string PackageClassName = "Package";

    public const int SlotCount = 4;

    public static string SlotPropertyName(int slot) => $"Effect{slot}";
}
