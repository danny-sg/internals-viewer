using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.UI.App.vNext.ViewModels.Tabs;

namespace InternalsViewer.UI.App.vNext.Models;

public partial class DatabaseFile(DatabaseViewModel parent) : ObservableObject
{
    [ObservableProperty]
    private short fileId;

    [ObservableProperty]
    private int size;

    public DatabaseViewModel Parent { get; set; } = parent;
}