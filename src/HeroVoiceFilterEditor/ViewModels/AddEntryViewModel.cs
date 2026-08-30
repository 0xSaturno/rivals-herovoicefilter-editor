using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using HeroVoiceFilterEditor.Core.Metadata;

namespace HeroVoiceFilterEditor.ViewModels;

public sealed record SkinChoice(int SkinId, string Display, bool AlreadyPresent);

public partial class AddEntryViewModel : ObservableObject
{
    private readonly HeroSkinCatalog _heroes;
    private readonly HashSet<int> _present;
    private readonly List<SkinChoice> _all;

    public AddEntryViewModel(HeroSkinCatalog heroes, IReadOnlyCollection<int> alreadyPresent)
    {
        _heroes = heroes;
        _present = new HashSet<int>(alreadyPresent);

        _all = heroes.Heroes
            .SelectMany(h => h.Skins)
            .OrderBy(s => s.SkinId)
            .Select(s => new SkinChoice(s.SkinId, s.Display, _present.Contains(s.SkinId)))
            .ToList();

        Results = new ObservableCollection<SkinChoice>(_all.Take(200));
    }

    public ObservableCollection<SkinChoice> Results { get; }

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    private SkinChoice? _selected;

    [ObservableProperty]
    private string _rawId = string.Empty;

    /// A skin id the markdown does not list is still valid; the table is keyed by id, not name.
    public int? ResolvedId
    {
        get
        {
            if (int.TryParse(RawId.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int typed) && typed > 0)
                return typed;

            return Selected?.SkinId;
        }
    }

    public string Validation
    {
        get
        {
            int? id = ResolvedId;
            if (id is null)
                return "Pick a skin or type an id.";
            if (_present.Contains(id.Value))
                return $"{id} already has an entry.";

            HeroSkin? known = _heroes.FindSkin(id.Value);
            return known is not null
                ? $"Will add {id} — {known.Display}"
                : $"Will add {id} — {_heroes.Describe(id.Value)} (not in the id list)";
        }
    }

    public bool CanAccept => ResolvedId is { } id && !_present.Contains(id);

    partial void OnQueryChanged(string value)
    {
        string query = value.Trim();

        Results.Clear();
        foreach (SkinChoice choice in _all
                     .Where(c => query.Length == 0
                                 || c.Display.Contains(query, StringComparison.OrdinalIgnoreCase)
                                 || c.SkinId.ToString().Contains(query, StringComparison.OrdinalIgnoreCase))
                     .Take(200))
        {
            Results.Add(choice);
        }
    }

    partial void OnSelectedChanged(SkinChoice? value)
    {
        if (value is not null)
            RawId = string.Empty;

        RaiseValidation();
    }

    partial void OnRawIdChanged(string value) => RaiseValidation();

    private void RaiseValidation()
    {
        OnPropertyChanged(nameof(ResolvedId));
        OnPropertyChanged(nameof(Validation));
        OnPropertyChanged(nameof(CanAccept));
    }
}
