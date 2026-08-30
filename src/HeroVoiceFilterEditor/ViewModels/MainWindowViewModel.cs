using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeroVoiceFilterEditor.Core.Effects;
using HeroVoiceFilterEditor.Core.Metadata;
using HeroVoiceFilterEditor.Core.Project;
using HeroVoiceFilterEditor.Core.Session;
using HeroVoiceFilterEditor.Core.Table;
using HeroVoiceFilterEditor.Services;

namespace HeroVoiceFilterEditor.ViewModels;

public enum EntryFilter
{
    All,
    HasFilters,
    Empty,
    Modified
}

public partial class MainWindowViewModel : ObservableObject
{
    private readonly List<EntryViewModel> _all = new();
    private readonly Dictionary<string, EffectReference> _effectsByName = new(StringComparer.OrdinalIgnoreCase);

    public MainWindowViewModel() : this(new NullDialogService())
    {
    }

    public MainWindowViewModel(IDialogService dialogs)
    {
        Dialogs = dialogs;
        EffectNames = new List<string> { SlotViewModel.NoneLabel };
    }

    public IDialogService Dialogs { get; }

    public EditorSession Session { get; } = new();

    public IReadOnlyList<string> EffectNames { get; private set; }

    public ObservableCollection<string> FamilyNames { get; } = new();

    public ObservableCollection<EntryViewModel> VisibleEntries { get; } = new();

    public ObservableCollection<string> Log { get; } = new();

    public IReadOnlyList<EntryFilter> Filters { get; } = Enum.GetValues<EntryFilter>();

    [ObservableProperty]
    private EntryViewModel? _selectedEntry;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private EntryFilter _filter = EntryFilter.All;

    [ObservableProperty]
    private string? _selectedFamily;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _status = "Starting…";

    [ObservableProperty]
    private string _provenance = string.Empty;

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    [ObservableProperty]
    private string? _projectPath;

    public bool IsReady => Session.Readiness == SessionReadiness.Ready;

    public string EntryCountText => _all.Count == 0
        ? string.Empty
        : $"{VisibleEntries.Count} of {_all.Count} entries";

    public string BackendVersion => Core.BackendInfo.Describe();

    public string UsmapLabel => Session.UsmapName is null ? "no usmap" : $"usmap {Session.UsmapName}";

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnFilterChanged(EntryFilter value) => ApplyFilter();

    public void Report(string message)
    {
        Log.Add($"{DateTime.Now:HH:mm:ss}  {message}");
        while (Log.Count > 400)
            Log.RemoveAt(0);
    }

    public void MarkDirty()
    {
        HasUnsavedChanges = true;
        OnPropertyChanged(nameof(EntryCountText));
    }

    public EffectReference? ResolveEffect(string objectName) => _effectsByName.GetValueOrDefault(objectName);

    [RelayCommand]
    public async Task InitializeAsync()
    {
        await RunBusy("Loading", async () =>
        {
            await Session.InitializeAsync(new Progress<string>(Report));
            RebuildFromSession();
        });

        if (Session.Readiness == SessionReadiness.NeedsExtraction)
            Status = "No workspace copy for this build yet — use Refresh from game.";
    }

    [RelayCommand]
    private async Task RefreshFromGameAsync()
    {
        if (HasUnsavedChanges && !await Dialogs.ConfirmAsync("Discard changes?",
                "Refreshing re-reads the table from the game and discards unsaved edits. Continue?"))
            return;

        await RunBusy("Reading game containers (this takes a few seconds)", async () =>
        {
            await Session.RefreshFromGameAsync(new Progress<string>(Report));
            RebuildFromSession();
            HasUnsavedChanges = false;
        });
    }

    [RelayCommand]
    private async Task ExportAssetAsync()
    {
        if (Session.Document is null)
            return;

        string suggested = Path.GetFileName(Session.DefaultExportPath());
        string? path = await Dialogs.PickSaveFileAsync("Export MarvelHeroVoiceData", suggested, "uasset");
        if (path is null)
            return;

        await RunBusy("Exporting", () =>
        {
            Session.SaveAsset(path);
            Report($"Exported {Path.GetFileName(path)} and its .uexp to {Path.GetDirectoryName(path)}");
            return Task.CompletedTask;
        });
    }

    [RelayCommand]
    private async Task SaveProjectAsync()
    {
        if (Session.Document is null)
            return;

        string? path = ProjectPath ?? await Dialogs.PickSaveFileAsync(
            "Save project", "HeroVoiceFilters" + FilterProject.Extension, FilterProject.Extension.TrimStart('.'));
        if (path is null)
            return;

        FilterProject project = Session.CreateProject();
        project.Save(path);
        ProjectPath = path;
        HasUnsavedChanges = false;
        Report($"Saved {project.Entries.Count} change(s) to {Path.GetFileName(path)}");
        Status = $"Project saved — {project.Entries.Count} change(s)";
    }

    [RelayCommand]
    private async Task SaveProjectAsAsync()
    {
        ProjectPath = null;
        await SaveProjectAsync();
    }

    [RelayCommand]
    private async Task OpenProjectAsync()
    {
        string? path = await Dialogs.PickOpenFileAsync("Open project", FilterProject.Extension.TrimStart('.'));
        if (path is null || Session.Document is null)
            return;

        await RunBusy("Applying project", () =>
        {
            FilterProject project = FilterProject.Load(path);
            ReplayReport report = Session.ReloadAndApply(project);

            foreach (ReplayItem item in report.Items)
                Report($"{item.SkinId}  {item.Status}  {item.Detail}");

            RebuildEntries();
            ProjectPath = path;
            HasUnsavedChanges = report.Count(ReplayStatus.Applied) + report.Count(ReplayStatus.Added) + report.Count(ReplayStatus.Removed) > 0;
            Status = report.Summary;

            return report.HasProblems
                ? Dialogs.ShowMessageAsync("Project applied with problems",
                    report.Summary + "\n\n" + string.Join("\n", report.Attention.Select(i => $"{i.SkinId}: {i.Status} — {i.Detail}")))
                : Task.CompletedTask;
        });
    }

    [RelayCommand]
    private async Task AddEntryAsync()
    {
        if (Session.Document is null)
            return;

        int[] existing = _all.Select(e => e.SkinId).ToArray();
        int? skinId = await Dialogs.PickSkinAsync(Session.Heroes, existing);
        if (skinId is null)
            return;

        SkinBusEntry entry = Session.Document.AddEntry(skinId.Value);
        var vm = new EntryViewModel(this, entry);
        _all.Add(vm);
        ApplyFilter();
        SelectedEntry = vm;
        MarkDirty();
        Report($"Added entry {skinId} ({Session.Heroes.Describe(skinId.Value)})");
    }

    [RelayCommand]
    private void RemoveEntry()
    {
        if (SelectedEntry is null || Session.Document is null)
            return;

        int skinId = SelectedEntry.SkinId;
        Session.Document.RemoveEntry(skinId);
        _all.Remove(SelectedEntry);
        SelectedEntry = null;
        ApplyFilter();
        MarkDirty();
        Report($"Removed entry {skinId}");
    }

    [RelayCommand]
    private void ApplyFamily()
    {
        if (SelectedEntry is null || SelectedFamily is null || Session.Effects is null)
            return;

        EffectFamily? family = Session.Effects.Families.FirstOrDefault(f => f.Name == SelectedFamily);
        if (family is null)
            return;

        SelectedEntry.ApplyFamily(family);
        Report($"{SelectedEntry.SkinId}: filled from {family.Name} ({family.Members.Count} slot(s))");
    }

    [RelayCommand]
    private void ClearSlots()
    {
        if (SelectedEntry is null)
            return;

        SelectedEntry.ClearAllSlots();
        Report($"{SelectedEntry.SkinId}: cleared all slots");
    }

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        bool changed = await Dialogs.EditSettingsAsync(Session.Settings);
        if (!changed)
            return;

        SettingsService.Save(Session.Settings);
        Report("Settings saved");
        await RunBusy("Reloading", async () =>
        {
            await Session.LoadMetadataAsync(new Progress<string>(Report));
            Session.TryLoadCachedVanilla(new Progress<string>(Report));
            RebuildFromSession();
        });
    }

    private void RebuildFromSession()
    {
        if (Session.Effects is not null)
        {
            _effectsByName.Clear();
            foreach (EffectReference effect in Session.Effects.Effects)
                _effectsByName[effect.ObjectName] = effect;

            EffectNames = new[] { SlotViewModel.NoneLabel }
                .Concat(Session.Effects.Effects.Select(e => e.ObjectName).OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
                .ToList();

            FamilyNames.Clear();
            foreach (EffectFamily family in Session.Effects.Families)
                FamilyNames.Add(family.Name);
        }

        RebuildEntries();

        Provenance = Session.Snapshot is null
            ? "no table loaded"
            : $"build {Session.Snapshot.Build}  ·  from {Session.Snapshot.SourceContainer}";
        OnPropertyChanged(nameof(UsmapLabel));

        Status = Session.Readiness switch
        {
            SessionReadiness.Ready => $"{_all.Count} entries loaded",
            SessionReadiness.NeedsExtraction => "Use Refresh from game to read the table",
            SessionReadiness.NeedsGameDirectory => "Set the Marvel Rivals Paks directory in Settings",
            SessionReadiness.NeedsUsmap => "No usmap available — check your connection or pin one in Settings",
            _ => string.Empty
        };

        OnPropertyChanged(nameof(IsReady));
    }

    private void RebuildEntries()
    {
        _all.Clear();
        if (Session.Document is not null)
        {
            foreach (SkinBusEntry entry in Session.Document.Entries)
                _all.Add(new EntryViewModel(this, entry));
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        string query = SearchText.Trim();

        IEnumerable<EntryViewModel> matches = _all.Where(e => e.Matches(query));
        matches = Filter switch
        {
            EntryFilter.HasFilters => matches.Where(e => e.HasFilters),
            EntryFilter.Empty => matches.Where(e => !e.HasFilters),
            EntryFilter.Modified => matches.Where(e => e.IsModified || e.IsAdded),
            _ => matches
        };

        EntryViewModel? previous = SelectedEntry;
        VisibleEntries.Clear();
        foreach (EntryViewModel entry in matches.OrderBy(e => e.SkinId))
            VisibleEntries.Add(entry);

        if (previous is not null && VisibleEntries.Contains(previous))
            SelectedEntry = previous;

        OnPropertyChanged(nameof(EntryCountText));
    }

    private async Task RunBusy(string what, Func<Task> action)
    {
        IsBusy = true;
        Status = what + "…";
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Report($"ERROR: {ex.Message}");
            Status = $"{what} failed: {ex.Message}";
            await Dialogs.ShowMessageAsync($"{what} failed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
