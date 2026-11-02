using InternalsViewer.UI.App.Messages;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.ComponentModel;
using InternalsViewer.UI.App.ViewModels.Connections;
using Microsoft.UI.Xaml.Navigation;

namespace InternalsViewer.UI.App.Views.Connect;

public sealed partial class ConnectServerPage
{
    public ConnectServerPage()
    {
        InitializeComponent();
    }

    private ConnectServerViewModel ViewModel => (ConnectServerViewModel)DataContext;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is ConnectServerViewModel viewModel)
        {
            DataContext = viewModel;

            viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        if (DataContext is ConnectServerViewModel viewModel)
        {
            viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        base.OnNavigatedFrom(e);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ConnectServerViewModel.IsDatabaseListRefreshing))
        {
            return;
        }

        if (ViewModel.IsDatabaseListRefreshing)
        {
            RefreshSpinStoryboard.Begin();
        }
        else
        {
            RefreshSpinStoryboard.Stop();
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
