using InternalsViewer.UI.App.Messages;
using CommunityToolkit.Mvvm.Messaging;
using System;
using InternalsViewer.UI.App.ViewModels.Connections;
using Microsoft.UI.Xaml.Navigation;

namespace InternalsViewer.UI.App.Views.Connect;

public sealed partial class ConnectServerPage
{
    public ConnectServerPage()
    {
        InitializeComponent();

        NavigationCacheMode = NavigationCacheMode.Enabled;
    }

    private ConnectServerViewModel ViewModel => (ConnectServerViewModel)DataContext;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is ConnectServerViewModel viewModel)
        {
            DataContext = viewModel;
        }
    }

#pragma warning disable VSTHRD100
    private async void DatabaseComboBox_DropDownOpened(object? sender, object e)
    {
        try
        {
            await ViewModel.RefreshDatabases();
        }
        catch (Exception exception)
        {
            await WeakReferenceMessenger.Default.Send(new ExceptionMessage(exception));
        }
    }
#pragma warning restore VSTHRD100
}
