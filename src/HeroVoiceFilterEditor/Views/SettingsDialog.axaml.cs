using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HeroVoiceFilterEditor.ViewModels;

namespace HeroVoiceFilterEditor.Views;

public partial class SettingsDialog : Window
{
    public SettingsDialog() => InitializeComponent();

    private async void OnBrowse(object? sender, RoutedEventArgs e)
    {
        IReadOnlyList<IStorageFolder> picked = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select the Marvel Rivals Paks folder",
            AllowMultiple = false
        });

        if (picked.Count > 0 && DataContext is SettingsViewModel vm)
            vm.PaksDirectory = picked[0].Path.LocalPath;
    }

    private void OnAccept(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
