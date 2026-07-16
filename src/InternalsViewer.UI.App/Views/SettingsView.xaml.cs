using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

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

    private async void BrowseTraceDirectory_Click(object sender, RoutedEventArgs e)
    {
        if (await PickFolderAsync() is { } path)
        {
            ViewModel.TraceDirectory = path;
        }
    }

    private async void BrowseSymbolsPath_Click(object sender, RoutedEventArgs e)
    {
        if (await PickFolderAsync() is { } path)
        {
            ViewModel.SymbolsPath = path;
        }
    }

    private static async Task<string?> PickFolderAsync()
    {
        if (App.MainWindow is null)
        {
            return null;
        }

        var picker = new FolderPicker();

        // WinUI 3 pickers must be tied to the window's handle, and FolderPicker requires at least one file-type filter.
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);

        WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

        picker.FileTypeFilter.Add("*");

        var folder = await picker.PickSingleFolderAsync();

        return folder?.Path;
    }
}