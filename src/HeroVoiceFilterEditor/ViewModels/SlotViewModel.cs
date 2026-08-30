using CommunityToolkit.Mvvm.ComponentModel;
using HeroVoiceFilterEditor.Core.Effects;

namespace HeroVoiceFilterEditor.ViewModels;

/// One Effect0..Effect3 slot. Bound as plain strings so the combo's type-ahead filters
/// on the effect name directly.
public partial class SlotViewModel : ObservableObject
{
    public const string NoneLabel = "(None)";

    private readonly EntryViewModel _owner;
    private readonly int _index;
    private bool _suppress;

    public SlotViewModel(EntryViewModel owner, int index, IReadOnlyList<string> available)
    {
        _owner = owner;
        _index = index;
        Available = available;
        _selectedName = owner.Entry.Slots[index]?.ObjectName ?? NoneLabel;
    }

    public string Label => $"Slot {_index}";

    public IReadOnlyList<string> Available { get; }

    [ObservableProperty]
    private string _selectedName;

    public bool IsEmpty => SelectedName is NoneLabel or null;

    partial void OnSelectedNameChanged(string value)
    {
        if (_suppress)
            return;

        EffectReference? effect = value is null or NoneLabel ? null : _owner.ResolveEffect(value);
        _owner.Entry.Slots[_index] = effect;
        _owner.NotifySlotsChanged();
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// Push the model back into the control without re-entering the change handler.
    public void Refresh()
    {
        _suppress = true;
        SelectedName = _owner.Entry.Slots[_index]?.ObjectName ?? NoneLabel;
        _suppress = false;
        OnPropertyChanged(nameof(IsEmpty));
    }

    public void Clear() => SelectedName = NoneLabel;
}
