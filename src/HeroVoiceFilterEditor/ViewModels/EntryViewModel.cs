using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using HeroVoiceFilterEditor.Core;
using HeroVoiceFilterEditor.Core.Effects;
using HeroVoiceFilterEditor.Core.Metadata;
using HeroVoiceFilterEditor.Core.Table;

namespace HeroVoiceFilterEditor.ViewModels;

public partial class EntryViewModel : ObservableObject
{
    private readonly MainWindowViewModel _main;

    public EntryViewModel(MainWindowViewModel main, SkinBusEntry entry)
    {
        _main = main;
        Entry = entry;

        Slots = new ObservableCollection<SlotViewModel>(
            Enumerable.Range(0, VoiceDataPaths.SlotCount).Select(i => new SlotViewModel(this, i, main.EffectNames)));
    }

    public SkinBusEntry Entry { get; }

    public int SkinId => Entry.SkinId;

    public ObservableCollection<SlotViewModel> Slots { get; }

    public string DisplayName => _main.Session.Heroes.Describe(SkinId);

    public string HeroName => _main.Session.Heroes.FindHero(HeroSkinCatalog.HeroIdOf(SkinId))?.HeroName
        ?? $"Hero {HeroSkinCatalog.HeroIdOf(SkinId)}";

    public bool IsNamed => _main.Session.Heroes.FindSkin(SkinId) is not null;

    public string SlotSummary => Entry.IsEmpty
        ? "no filters"
        : string.Join("  ·  ", Entry.Slots.Where(s => s is not null).Select(s => s!.ObjectName));

    /// Filter names for the row's pill chips. Trims the "effect_vo_" prefix every filter
    /// shares, since spelling it out on every chip would make each row needlessly tall.
    public IReadOnlyList<string> FilterChips => Entry.Slots
        .Where(s => s is not null)
        .Select(s => ShortEffectName(s!.ObjectName))
        .ToList();

    private static string ShortEffectName(string objectName) =>
        objectName.StartsWith("effect_vo_", StringComparison.OrdinalIgnoreCase)
            ? objectName["effect_vo_".Length..]
            : objectName;

    public bool IsAdded => _main.Session.Document?.IsAdded(Entry) ?? false;

    public bool IsModified => _main.Session.Document?.IsModified(Entry) ?? false;

    public string Badge => IsAdded ? "NEW" : IsModified ? "EDITED" : string.Empty;

    public bool HasBadge => Badge.Length > 0;

    public bool HasFilters => !Entry.IsEmpty;

    public EffectReference? ResolveEffect(string objectName) => _main.ResolveEffect(objectName);

    public void NotifySlotsChanged()
    {
        OnPropertyChanged(nameof(SlotSummary));
        OnPropertyChanged(nameof(FilterChips));
        OnPropertyChanged(nameof(HasFilters));
        RefreshBadges();
        _main.MarkDirty();
    }

    public void RefreshBadges()
    {
        OnPropertyChanged(nameof(IsAdded));
        OnPropertyChanged(nameof(IsModified));
        OnPropertyChanged(nameof(Badge));
        OnPropertyChanged(nameof(HasBadge));
    }

    /// Fills consecutive slots from a family, clearing any the family does not reach.
    public void ApplyFamily(EffectFamily family)
    {
        for (int i = 0; i < VoiceDataPaths.SlotCount; i++)
            Entry.Slots[i] = i < family.Members.Count ? family.Members[i] : null;

        foreach (SlotViewModel slot in Slots)
            slot.Refresh();

        NotifySlotsChanged();
    }

    public void ClearAllSlots()
    {
        for (int i = 0; i < VoiceDataPaths.SlotCount; i++)
            Entry.Slots[i] = null;

        foreach (SlotViewModel slot in Slots)
            slot.Refresh();

        NotifySlotsChanged();
    }

    public bool Matches(string query) =>
        query.Length == 0
        || SkinId.ToString().Contains(query, StringComparison.OrdinalIgnoreCase)
        || DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
        || SlotSummary.Contains(query, StringComparison.OrdinalIgnoreCase);
}
