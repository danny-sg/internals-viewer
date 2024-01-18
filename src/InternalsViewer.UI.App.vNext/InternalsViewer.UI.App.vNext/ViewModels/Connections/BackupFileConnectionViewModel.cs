using CommunityToolkit.Mvvm.ComponentModel;

namespace InternalsViewer.UI.App.vNext.ViewModels.Connections;

[ObservableObject]
public partial class BackupFileConnectionViewModel
{
    [ObservableProperty]
    private string filename = string.Empty;
}