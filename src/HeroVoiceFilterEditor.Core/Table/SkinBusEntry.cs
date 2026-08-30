using HeroVoiceFilterEditor.Core.Effects;

namespace HeroVoiceFilterEditor.Core.Table;

/// One SkinBusEffects row: a skin id mapped to four AkEffectShareSet slots, any of which may be None.
public sealed class SkinBusEntry
{
    public SkinBusEntry(int skinId, IEnumerable<EffectReference?>? slots = null)
    {
        SkinId = skinId;
        Slots = new EffectReference?[VoiceDataPaths.SlotCount];

        if (slots is null)
            return;

        int index = 0;
        foreach (EffectReference? slot in slots)
        {
            if (index >= Slots.Length)
                break;
            Slots[index++] = slot;
        }
    }

    public int SkinId { get; set; }

    public EffectReference?[] Slots { get; }

    public bool IsEmpty => Slots.All(s => s is null);

    public int FilledSlotCount => Slots.Count(s => s is not null);

    public SkinBusEntry Clone()
    {
        var copy = new SkinBusEntry(SkinId);
        Array.Copy(Slots, copy.Slots, Slots.Length);
        return copy;
    }

    public bool SlotsEqual(SkinBusEntry other)
    {
        for (int i = 0; i < Slots.Length; i++)
        {
            if (Slots[i]?.PackagePath != other.Slots[i]?.PackagePath)
                return false;
        }

        return true;
    }

    public override string ToString() =>
        $"{SkinId} [{string.Join(", ", Slots.Select(s => s?.ObjectName ?? "None"))}]";
}
