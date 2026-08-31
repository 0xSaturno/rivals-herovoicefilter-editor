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

        // A blank value is never a deliberate clear — that goes through the explicit "(None)"
        // entry — so treat it (and any other text that resolves to nothing) as noise from the
        // control rather than an edit. Only "(None)" or a real effect ever touches the model.
        if (string.IsNullOrEmpty(value))
            return;

        if (value == NoneLabel)
        {
            _owner.Entry.Slots[_index] = null;
        }
        else
        {
            EffectReference? effect = _owner.ResolveEffect(value);
            if (effect is null)
                return;

            _owner.Entry.Slots[_index] = effect;
        }

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
