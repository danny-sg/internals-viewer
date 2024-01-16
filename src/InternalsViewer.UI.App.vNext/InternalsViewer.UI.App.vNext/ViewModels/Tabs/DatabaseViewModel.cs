using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.UI.App.vNext.Models;

namespace InternalsViewer.UI.App.vNext.ViewModels.Tabs;

public partial class DatabaseViewModel(IServiceProvider serviceProvider, DatabaseSource database)
    : TabViewModel(serviceProvider, TabType.Database)
{
    [ObservableProperty]
    private DatabaseSource database = database;

    [ObservableProperty]
    private ObservableCollection<AllocationLayer> allocationLayers = new();

    [ObservableProperty]
    private AllocationLayer? selectedLayer;

    [ObservableProperty]
    private int size;

    [ObservableProperty]
    private Allocation.AllocationOverViewModel allocationOver = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GridAllocationLayers))]
    private string filter = string.Empty;

    [ObservableProperty]
    private bool isLayerDetailVisible;

    public List<AllocationLayer> GridAllocationLayers
        => allocationLayers.Where(w => string.IsNullOrEmpty(Filter) || w.Name.ToLower().Contains(filter.ToLower())).ToList();
}
