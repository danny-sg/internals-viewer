using CommunityToolkit.Mvvm.ComponentModel;

namespace InternalsViewer.UI.App.ViewModels.Connections;

public partial class BackupFileConnectionViewModel : ObservableObject
{
    [ObservableProperty]
    private string _filename = string.Empty;
}