using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace InternalsViewer.UI.App.vNext.ViewModels.Tabs;

public partial class TabViewModel : ObservableObject
{
    public MainViewModel Parent { get; }

    public virtual TabType TabType { get; }

    [ObservableProperty]
    private string tabId = string.Empty;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private bool isLoading = true;

    public TabViewModel(MainViewModel parent)
    {
        Parent = parent;
        TabId = Guid.NewGuid().ToString();
    }
}