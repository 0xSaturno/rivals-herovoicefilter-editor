using CommunityToolkit.Mvvm.ComponentModel;
using HeroVoiceFilterEditor.Core;

namespace HeroVoiceFilterEditor.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _backendStatus = BackendInfo.Describe();

    [ObservableProperty]
    private string _stage = "Phase 0 — scaffolding. No asset loaded yet.";
}
