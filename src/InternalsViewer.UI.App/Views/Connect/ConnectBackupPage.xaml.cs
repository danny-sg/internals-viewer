using InternalsViewer.UI.App.Messages;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Linq;
using Windows.Storage.Pickers;
using InternalsViewer.UI.App.ViewModels.Connections;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace InternalsViewer.UI.App.Views.Connect;

public sealed partial class ConnectBackupPage
{
    public ConnectBackupPage()
    {
        InitializeComponent();
    }

    private ConnectBackupViewModel ViewModel => (ConnectBackupViewModel)DataContext;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is ConnectBackupViewModel viewModel)
        {
            DataContext = viewModel;
        }
    }

#pragma warning disable VSTHRD100
    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var openPicker = new FileOpenPicker();

            var window = App.MainWindow;

            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);

            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hWnd);

            openPicker.ViewMode = PickerViewMode.List;

            openPicker.FileTypeFilter.Add(".bak");

            var files = await openPicker.PickMultipleFilesAsync();

            if (files.Count > 0)
            {
                ViewModel.AddFiles(files.Select(f => f.Path));
            }
        }
        catch (Exception exception)
        {
            await WeakReferenceMessenger.Default.Send(new ExceptionMessage(exception));
        }
    }
#pragma warning restore VSTHRD100

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string path })
        {
            ViewModel.RemoveFileCommand.Execute(path);
        }
    }
}
