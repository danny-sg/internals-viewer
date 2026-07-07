using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views;

/// <summary>
/// Settings page.
/// </summary>
public sealed partial class SettingsView : Page
{
    internal AppLogViewModel AppLogViewModel { get; } = App.GetService<AppLogViewModel>();

    internal SettingsViewModel ViewModel { get; } = App.GetService<SettingsViewModel>();

    public SettingsView()
    {
        InitializeComponent();

        _ = ViewModel.LoadAsync();
    }

    private async void OpenLogButton_Click(object sender, RoutedEventArgs e)
    {
        await WeakReferenceMessenger.Default.Send(new OpenLogMessage());
    }

    private void ClearLogButton_Click(object sender, RoutedEventArgs e)
    {
        AppLogViewModel.ClearLogCommand.Execute(null);
    }
}