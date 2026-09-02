using Avalonia.Controls;
using Avalonia.Interactivity;
using HeroVoiceFilterEditor.ViewModels;

namespace HeroVoiceFilterEditor.Views;

public partial class AddEntryDialog : Window
{
    public AddEntryDialog() => InitializeComponent();

    private void OnAccept(object? sender, RoutedEventArgs e) =>
        Close((DataContext as AddEntryViewModel)?.ResolvedIds ?? Array.Empty<int>());

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
