using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeroVoiceFilterEditor.Core.Metadata;

namespace HeroVoiceFilterEditor.ViewModels;

public sealed record SkinChoice(int SkinId, string Display, bool AlreadyPresent);

/// A row in the results list: either a hero's default skin (collapsible header, itself
/// checkable) or an alternate skin nested under it once expanded, or a flat search hit.
public sealed record ResultRow(SkinChoice Skin, int HeroId, bool IsHeader, bool HasAltSkins, bool IsExpanded, bool IsAlt, bool IsChecked);

public partial class AddEntryViewModel : ObservableObject
{
    private readonly HeroSkinCatalog _heroes;
    private readonly HashSet<int> _present;
    private readonly List<SkinChoice> _all;
    private readonly List<Hero> _heroesOrdered;
    private readonly Dictionary<int, List<SkinChoice>> _altSkinsByHero;
    private readonly HashSet<int> _expandedHeroIds = new();
    private readonly HashSet<int> _checkedSkinIds = new();

    public AddEntryViewModel(HeroSkinCatalog heroes, IReadOnlyCollection<int> alreadyPresent)
    {
        _heroes = heroes;
        _present = new HashSet<int>(alreadyPresent);

        _all = heroes.Heroes
            .SelectMany(h => h.Skins)
            .OrderBy(s => s.SkinId)
            .Select(ToChoice)
            .ToList();

        _heroesOrdered = heroes.Heroes.OrderBy(h => h.HeroId).ToList();
        _altSkinsByHero = _heroesOrdered.ToDictionary(
            h => h.HeroId,
            h => h.Skins
                .Where(s => s.SkinId != HeroSkinCatalog.DefaultSkinId(h.HeroId))
                .OrderBy(s => s.SkinId)
                .Select(ToChoice)
                .ToList());

        Results = new ObservableCollection<ResultRow>();
        RebuildRows();
    }

    private SkinChoice ToChoice(HeroSkin skin) => new(skin.SkinId, skin.Display, _present.Contains(skin.SkinId));

    public ObservableCollection<ResultRow> Results { get; }

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    private string _rawId = string.Empty;

    /// Every id to add: checked rows plus a manually typed raw id, deduplicated.
    public IReadOnlyList<int> ResolvedIds
    {
        get
        {
            var ids = new List<int>(_checkedSkinIds);

            if (int.TryParse(RawId.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int typed)
                && typed > 0 && !_present.Contains(typed) && !ids.Contains(typed))
            {
                ids.Add(typed);
            }

            return ids;
        }
    }

    public string Validation
    {
        get
        {
            IReadOnlyList<int> ids = ResolvedIds;
            if (ids.Count == 0)
                return "Check one or more skins, or type an id.";

            if (ids.Count == 1)
            {
                int id = ids[0];
                HeroSkin? known = _heroes.FindSkin(id);
                return known is not null
                    ? $"Will add {id} — {known.Display}"
                    : $"Will add {id} — {_heroes.Describe(id)} (not in the id list)";
            }

            string list = ids.Count <= 6 ? string.Join(", ", ids) : string.Join(", ", ids.Take(6)) + $", +{ids.Count - 6} more";
            return $"Will add {ids.Count} skins: {list}";
        }
    }

    public bool CanAccept => ResolvedIds.Count > 0;

    public string AddLabel => ResolvedIds.Count > 1 ? $"Add {ResolvedIds.Count} entries" : "Add entry";

    [RelayCommand]
    private void ToggleExpand(int heroId)
    {
        if (!_expandedHeroIds.Remove(heroId))
            _expandedHeroIds.Add(heroId);
        RebuildRows();
    }

    [RelayCommand]
    private void ToggleSelect(int skinId)
    {
        if (_present.Contains(skinId))
            return;

        if (!_checkedSkinIds.Remove(skinId))
            _checkedSkinIds.Add(skinId);

        RebuildRows();
        RaiseValidation();
    }

    partial void OnQueryChanged(string value) => RebuildRows();

    partial void OnRawIdChanged(string value) => RaiseValidation();

    private void RaiseValidation()
    {
        OnPropertyChanged(nameof(ResolvedIds));
        OnPropertyChanged(nameof(Validation));
        OnPropertyChanged(nameof(CanAccept));
        OnPropertyChanged(nameof(AddLabel));
    }

    private void RebuildRows()
    {
        Results.Clear();

        string query = Query.Trim();
        if (query.Length > 0)
        {
            foreach (SkinChoice choice in _all.Where(c => Matches(c, query)))
                Results.Add(ToRow(choice, HeroSkinCatalog.HeroIdOf(choice.SkinId), IsHeader: false, HasAltSkins: false, IsExpanded: false, IsAlt: false));
        }
        else
        {
            foreach (Hero hero in _heroesOrdered)
            {
                int defaultId = HeroSkinCatalog.DefaultSkinId(hero.HeroId);
                HeroSkin? defaultSkin = hero.Skins.FirstOrDefault(s => s.SkinId == defaultId);
                if (defaultSkin is null)
                    continue;

                List<SkinChoice> alts = _altSkinsByHero[hero.HeroId];
                bool expanded = _expandedHeroIds.Contains(hero.HeroId);
                Results.Add(ToRow(ToChoice(defaultSkin), hero.HeroId, IsHeader: true, HasAltSkins: alts.Count > 0, IsExpanded: expanded, IsAlt: false));

                if (expanded)
                {
                    foreach (SkinChoice alt in alts)
                        Results.Add(ToRow(alt, hero.HeroId, IsHeader: false, HasAltSkins: false, IsExpanded: false, IsAlt: true));
                }
            }
        }
    }

    private ResultRow ToRow(SkinChoice skin, int heroId, bool IsHeader, bool HasAltSkins, bool IsExpanded, bool IsAlt) =>
        new(skin, heroId, IsHeader, HasAltSkins, IsExpanded, IsAlt, _checkedSkinIds.Contains(skin.SkinId));

    private static bool Matches(SkinChoice choice, string query) =>
        choice.Display.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        choice.SkinId.ToString(CultureInfo.InvariantCulture).Contains(query, StringComparison.OrdinalIgnoreCase);
}
