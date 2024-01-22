using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.UI.App.Controls.Connections;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.ViewModels.Tabs;
using System;
using Windows.Storage.Pickers;

namespace InternalsViewer.UI.App.Views.Connect;

public sealed partial class ConnectStartPage
{
    public ConnectStartPage()
    {
        InitializeComponent();
    }

    public ConnectViewModel ViewModel => (ConnectViewModel)DataContext;

    private void ConnectFileTile_OnClick(object? sender, RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.Send(new NavigateMessage("ConnectFilePage"));
    }

    private void ConnectSqlServerHeaderTile_Click(object sender, RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.Send(new NavigateMessage("ConnectServerPage"));
    }
}
