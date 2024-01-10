﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.UI.App.vNext.Helpers;
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
    private Allocation.AllocationOverViewModel allocationOver = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GridAllocationLayers))]
    private string filter = string.Empty;

    public List<AllocationLayer> GridAllocationLayers 
        => allocationLayers.Where(w => string.IsNullOrEmpty(Filter) || w.Name.ToLower().Contains(filter.ToLower())).ToList();
}