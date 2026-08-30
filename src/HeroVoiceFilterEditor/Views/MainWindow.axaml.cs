using Avalonia.Controls;
using HeroVoiceFilterEditor.Services;
using HeroVoiceFilterEditor.ViewModels;

namespace HeroVoiceFilterEditor.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var viewModel = new MainWindowViewModel(new DialogService(this));
        DataContext = viewModel;

        Opened += async (_, _) => await viewModel.InitializeAsync();
    }
}
