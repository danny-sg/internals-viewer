using System;
using System.Collections.Generic;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.UI.App.vNext.Models.Connections;
using InternalsViewer.UI.App.vNext.ViewModels.Connections;
using Microsoft.Data.SqlClient;

namespace InternalsViewer.UI.App.vNext.Controls.Connections;

public sealed partial class ServerConnectionControl
{
    public ServerConnectionViewModel ViewModel => (ServerConnectionViewModel)DataContext;

    public event EventHandler<ServerConnectEventArgs>? ConnectRequested;

    public ServerConnectionSettings? InitialSettings
    {
        get => (ServerConnectionSettings?)GetValue(InitialSettingsProperty);
        set => SetValue(InitialSettingsProperty, value);
    }

    public static readonly DependencyProperty InitialSettingsProperty
        = DependencyProperty.Register(nameof(InitialSettings),
            typeof(ServerConnectionSettings),
            typeof(ServerConnectionControl),
            new PropertyMetadata(default, OnInitialSettingsChanged));

    private static void OnInitialSettingsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is ServerConnectionSettings value)
        {
            (d as ServerConnectionControl)?.ViewModel.SetInitialSettings(value);
        }
    }

    public ServerConnectionControl()
    {
        InitializeComponent();

        DataContext = new ServerConnectionViewModel();
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

        ConnectRequested?.Invoke(sender, new ServerConnectEventArgs(connectionString, settings));
    }

    private async void DatabaseComboBox_DropDownOpened(object? sender, object e)
    {
        await ViewModel.RefreshDatabases();
    }
}