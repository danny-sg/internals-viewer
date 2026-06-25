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
    internal AppLogViewModel ViewModel { get; } = App.GetService<AppLogViewModel>();

    public SettingsView()
    {
        InitializeComponent();
    }

    private async void OpenLogButton_Click(object sender, RoutedEventArgs e)
    {
        await WeakReferenceMessenger.Default.Send(new OpenLogMessage());
    }

    private void ClearLogButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearLogCommand.Execute(null);
    }
}