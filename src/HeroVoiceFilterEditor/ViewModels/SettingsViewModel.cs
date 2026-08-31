using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeroVoiceFilterEditor.Core.Game;
using HeroVoiceFilterEditor.Core.Metadata;
using HeroVoiceFilterEditor.Services;

namespace HeroVoiceFilterEditor.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    public SettingsViewModel(AppSettings settings)
    {
        _paksDirectory = settings.PaksDirectory ?? string.Empty;
        _aesKey = settings.AesKey;
        _usmapOverridePath = settings.UsmapOverridePath ?? string.Empty;
        _workspaceDirectory = settings.WorkspaceDirectory ?? string.Empty;
        _checkForUpdatesOnLaunch = settings.CheckForUpdatesOnLaunch;
        _showLogPane = settings.ShowLogPane;
    }

    [ObservableProperty]
    private string _paksDirectory;

    [ObservableProperty]
    private string _aesKey;

    [ObservableProperty]
    private string _usmapOverridePath;

    [ObservableProperty]
    private string _workspaceDirectory;

    [ObservableProperty]
    private bool _checkForUpdatesOnLaunch;

    [ObservableProperty]
    private bool _showLogPane;

    public string PaksValidation => GameLocator.IsPaksDirectory(PaksDirectory)
        ? "Looks good — global.utoc found."
        : "No global.utoc here; the editor cannot read the game from this folder.";

    partial void OnPaksDirectoryChanged(string value) => OnPropertyChanged(nameof(PaksValidation));

    [RelayCommand]
    private void Autodetect()
    {
        string? found = GameLocator.FindCandidates().FirstOrDefault();
        if (found is not null)
            PaksDirectory = found;
    }

    [RelayCommand]
    private void ClearUsmap() => UsmapOverridePath = string.Empty;

    [RelayCommand]
    private void OpenWorkspace() => FileExplorer.OpenFolder(
        string.IsNullOrWhiteSpace(WorkspaceDirectory) ? AppPaths.DefaultWorkspaceDirectory : WorkspaceDirectory.Trim());

    public void WriteTo(AppSettings settings)
    {
        settings.PaksDirectory = Blank(PaksDirectory);
        settings.AesKey = string.IsNullOrWhiteSpace(AesKey) ? GameDefaults.AesKey : AesKey.Trim();
        settings.UsmapOverridePath = Blank(UsmapOverridePath);
        settings.WorkspaceDirectory = Blank(WorkspaceDirectory);
        settings.CheckForUpdatesOnLaunch = CheckForUpdatesOnLaunch;
        settings.ShowLogPane = ShowLogPane;
    }

    private static string? Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
