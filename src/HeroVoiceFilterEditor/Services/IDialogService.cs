using HeroVoiceFilterEditor.Core.Metadata;

namespace HeroVoiceFilterEditor.Services;

public interface IDialogService
{
    Task<string?> PickSaveFileAsync(string title, string suggestedName, string extension);

    Task<string?> PickOpenFileAsync(string title, string extension);

    Task<string?> PickFolderAsync(string title);

    Task ShowMessageAsync(string title, string message);

    Task<bool> ConfirmAsync(string title, string message);

    /// Returns the chosen skin ids, or an empty list if cancelled.
    Task<IReadOnlyList<int>> PickSkinAsync(HeroSkinCatalog heroes, IReadOnlyCollection<int> alreadyPresent);

    /// Returns true when the user accepted changes.
    Task<bool> EditSettingsAsync(AppSettings settings);
}

/// Used by the design-time constructor, where no window exists.
public sealed class NullDialogService : IDialogService
{
    public Task<string?> PickSaveFileAsync(string title, string suggestedName, string extension) => Task.FromResult<string?>(null);

    public Task<string?> PickOpenFileAsync(string title, string extension) => Task.FromResult<string?>(null);

    public Task<string?> PickFolderAsync(string title) => Task.FromResult<string?>(null);

    public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;

    public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(false);

    public Task<IReadOnlyList<int>> PickSkinAsync(HeroSkinCatalog heroes, IReadOnlyCollection<int> alreadyPresent) =>
        Task.FromResult<IReadOnlyList<int>>(Array.Empty<int>());

    public Task<bool> EditSettingsAsync(AppSettings settings) => Task.FromResult(false);
}
