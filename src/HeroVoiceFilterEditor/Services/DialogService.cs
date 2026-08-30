using Avalonia.Controls;
using Avalonia.Platform.Storage;
using HeroVoiceFilterEditor.Core.Metadata;
using HeroVoiceFilterEditor.ViewModels;
using HeroVoiceFilterEditor.Views;

namespace HeroVoiceFilterEditor.Services;

public sealed class DialogService : IDialogService
{
    private readonly Window _owner;

    public DialogService(Window owner) => _owner = owner;

    public async Task<string?> PickSaveFileAsync(string title, string suggestedName, string extension)
    {
        IStorageFile? file = await _owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = extension,
            FileTypeChoices = [FileType(extension)]
        });

        return file?.Path.LocalPath;
    }

    public async Task<string?> PickOpenFileAsync(string title, string extension)
    {
        IReadOnlyList<IStorageFile> files = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [FileType(extension)]
        });

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    public async Task<string?> PickFolderAsync(string title)
    {
        IReadOnlyList<IStorageFolder> folders = await _owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }

    public async Task ShowMessageAsync(string title, string message) =>
        await MessageDialog.Create(title, message, confirm: false).ShowDialog(_owner);

    public async Task<bool> ConfirmAsync(string title, string message) =>
        await MessageDialog.Create(title, message, confirm: true).ShowDialog<bool>(_owner);

    public async Task<int?> PickSkinAsync(HeroSkinCatalog heroes, IReadOnlyCollection<int> alreadyPresent)
    {
        var dialog = new AddEntryDialog { DataContext = new AddEntryViewModel(heroes, alreadyPresent) };
        return await dialog.ShowDialog<int?>(_owner);
    }

    public async Task<bool> EditSettingsAsync(AppSettings settings)
    {
        var viewModel = new SettingsViewModel(settings);
        var dialog = new SettingsDialog { DataContext = viewModel };

        if (!await dialog.ShowDialog<bool>(_owner))
            return false;

        viewModel.WriteTo(settings);
        return true;
    }

    private static FilePickerFileType FileType(string extension) => new($".{extension} files")
    {
        Patterns = [$"*.{extension}"]
    };
}
