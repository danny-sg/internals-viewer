using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.UI.App.vNext.Models;

namespace InternalsViewer.UI.App.vNext.ViewModels.Tabs;

public partial class DatabaseViewModel(MainViewModel parent, DatabaseDetail database) : TabViewModel(parent)
{
    public override TabType TabType => TabType.Database;

    [ObservableProperty]
    private DatabaseDetail database = database;

    [ObservableProperty]
    private ObservableCollection<AllocationLayer> allocationLayers = new();

    [ObservableProperty]
    private AllocationLayer? selectedLayer;

    [ObservableProperty]
    private int size;

    [ObservableProperty]
    private AllocationOverViewModel allocationOver = new();
}
