using System;
using InternalsViewer.UI.App.ViewModels.Connections;

namespace InternalsViewer.UI.App.Controls.Connections;

public sealed partial class BackupFileConnectionControl
{
    public BackupFileConnectionViewModel ViewModel => (BackupFileConnectionViewModel)DataContext;

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