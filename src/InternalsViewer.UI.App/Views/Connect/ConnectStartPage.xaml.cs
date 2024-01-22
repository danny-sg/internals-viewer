using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.ViewModels;

namespace InternalsViewer.UI.App.Views.Connect;

public sealed partial class ConnectStartPage
{
    public ConnectStartPage()
    {
        InitializeComponent();
    }

    public MainViewModel ViewModel => (MainViewModel)DataContext;

    private void ConnectFileTile_OnClick(object? sender, RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.Send(new NavigateMessage("ConnectFilePage"));
    }

    private void ConnectSqlServerHeaderTile_Click(object sender, RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.Send(new NavigateMessage("ConnectServerPage"));
    }
}
