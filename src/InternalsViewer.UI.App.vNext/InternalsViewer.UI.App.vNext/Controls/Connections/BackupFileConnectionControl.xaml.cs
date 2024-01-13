using System;
using InternalsViewer.UI.App.vNext.ViewModels.Connections;

namespace InternalsViewer.UI.App.vNext.Controls.Connections;

public sealed partial class BackupFileConnectionControl
{
    public BackupFileConnectionViewModel ViewModel => (BackupFileConnectionViewModel)DataContext;

    public event EventHandler<ServerConnectEventArgs>? ConnectRequested;

    public BackupFileConnectionControl()
    {
        InitializeComponent();

        DataContext = new BackupFileConnectionViewModel();
    }

    private void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        throw new NotImplementedException();
    }
}