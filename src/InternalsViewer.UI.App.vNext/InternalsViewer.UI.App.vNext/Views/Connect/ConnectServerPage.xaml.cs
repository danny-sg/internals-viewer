using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.UI.App.vNext.Messages;
using InternalsViewer.UI.App.vNext.ViewModels.Connections;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Navigation;

namespace InternalsViewer.UI.App.vNext.Views.Connect;

public sealed partial class ConnectServerPage
{
    public ConnectServerPage()
    {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Enabled;
    }

    public ServerConnectionViewModel ViewModel => (ServerConnectionViewModel)DataContext;

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is ServerConnectionViewModel viewModel)
        {
            DataContext = viewModel;
        }
    }

    private IEnumerable<string> AuthenticationTypes
    {
        get
        {
            foreach (var value in Enum.GetNames(typeof(SqlAuthenticationMethod)))
            {
                yield return value.SplitCamelCase();
            }
        }
    }

    private void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = ViewModel.GetSettings();
        var connectionString = ViewModel.GetConnectionString();

        WeakReferenceMessenger.Default.Send(new ConnectServerMessage((connectionString, settings)));
    }

    private async void DatabaseComboBox_DropDownOpened(object? sender, object e)
    {
        await ViewModel.RefreshDatabases();
    }
}
