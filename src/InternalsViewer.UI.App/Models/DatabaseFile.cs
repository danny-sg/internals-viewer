using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.UI.App.ViewModels.Database;

namespace InternalsViewer.UI.App.Models;

public partial class DatabaseFile(DatabaseTabViewModel parent) : ObservableObject
{
    [ObservableProperty]
    private short fileId;

    [ObservableProperty]
    private int size;

    public DatabaseTabViewModel Parent { get; } = parent;
}