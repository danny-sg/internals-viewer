using InternalsViewer.UI.App.Messages;
using CommunityToolkit.Mvvm.Messaging;
using System;
using Windows.Storage.Pickers;
using InternalsViewer.UI.App.ViewModels.Connections;
using Microsoft.UI.Xaml.Navigation;

namespace InternalsViewer.UI.App.Views.Connect;

public sealed partial class ConnectFilePage
{
    public ConnectFilePage()
    {
        InitializeComponent();
    }

    private ConnectFileViewModel ViewModel => (ConnectFileViewModel)DataContext;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is ConnectFileViewModel viewModel)
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

            openPicker.FileTypeFilter.Add(".mdf");

            var file = await openPicker.PickSingleFileAsync();

            if (file != null)
            {
                ViewModel.Filename = file.Path;
                ViewModel.IsValid = true;
            }
        }
        catch (Exception exception)
        {
            await WeakReferenceMessenger.Default.Send(new ExceptionMessage(exception));
        }
    }
#pragma warning restore VSTHRD100
}
