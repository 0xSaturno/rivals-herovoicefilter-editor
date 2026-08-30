using Avalonia.Controls;
using Avalonia.Interactivity;

namespace HeroVoiceFilterEditor.Views;

public partial class MessageDialog : Window
{
    public MessageDialog() => InitializeComponent();

    public static MessageDialog Create(string title, string message, bool confirm)
    {
        var dialog = new MessageDialog { Title = title };
        dialog.MessageText.Text = message;
        dialog.CancelButton.IsVisible = confirm;
        dialog.OkButton.Content = confirm ? "Continue" : "OK";
        return dialog;
    }

    private void OnAccept(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
