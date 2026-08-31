using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace InternalsViewer.UI.App.Views;

public sealed partial class SettingsView : Page
{
    private readonly DispatcherTimer _memoryTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    public SettingsView()
    {
        InitializeComponent();

        _ = ViewModel.LoadAsync();

        ViewModel.RefreshMemoryUsage();

        _memoryTimer.Tick += (_, _) => ViewModel.RefreshMemoryUsage();

        Loaded += (_, _) => _memoryTimer.Start();
        Unloaded += (_, _) => _memoryTimer.Stop();
    }

    internal AppLogViewModel AppLogViewModel { get; } = App.GetService<AppLogViewModel>();

    internal SettingsViewModel ViewModel { get; } = App.GetService<SettingsViewModel>();

#pragma warning disable VSTHRD100
    private async void OpenLogButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await WeakReferenceMessenger.Default.Send(new OpenLogMessage());
        }
        catch (Exception exception)
        {
            await WeakReferenceMessenger.Default.Send(new ExceptionMessage(exception));
        }
    }
#pragma warning restore VSTHRD100

    private void ClearLogButton_Click(object sender, RoutedEventArgs e)
    {
        AppLogViewModel.ClearLogCommand.Execute(null);
    }

#pragma warning disable VSTHRD100
    private async void BrowseTraceDirectory_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (await PickFolderAsync() is { } path)
            {
                ViewModel.TraceDirectory = path;
            }
        }
        catch (Exception exception)
        {
            await WeakReferenceMessenger.Default.Send(new ExceptionMessage(exception));
        }
    }
#pragma warning restore VSTHRD100

#pragma warning disable VSTHRD100
    private async void BrowseSymbolsPath_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (await PickFolderAsync() is { } path)
            {
                ViewModel.SymbolsPath = path;
            }
        }
        catch (Exception exception)
        {
            await WeakReferenceMessenger.Default.Send(new ExceptionMessage(exception));
        }
    }
#pragma warning restore VSTHRD100

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
