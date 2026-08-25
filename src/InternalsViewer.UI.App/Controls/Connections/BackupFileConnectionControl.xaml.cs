using System;
using InternalsViewer.UI.App.ViewModels.Connections;

namespace InternalsViewer.UI.App.Controls.Connections;

public sealed partial class BackupFileConnectionControl
{
    public BackupFileConnectionControl()
    {
        InitializeComponent();

        DataContext = new BackupFileConnectionViewModel();
    }

    public BackupFileConnectionViewModel ViewModel => (BackupFileConnectionViewModel)DataContext;

    private void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        throw new NotImplementedException();
    }
}