using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.UI.App.vNext.Messages;
using InternalsViewer.UI.App.vNext.Models;
using DatabaseFile = InternalsViewer.UI.App.vNext.Models.DatabaseFile;

namespace InternalsViewer.UI.App.vNext.ViewModels.Tabs;

public partial class DatabaseViewModel(IServiceProvider serviceProvider, DatabaseSource database)
    : TabViewModel(serviceProvider, TabType.Database)
{
    [ObservableProperty]
    private DatabaseSource database = database;

    [ObservableProperty]
    private DatabaseFile[] databaseFiles = Array.Empty<DatabaseFile>();

    [ObservableProperty]
    private ObservableCollection<AllocationLayer> allocationLayers = new();

    [ObservableProperty]
    private AllocationLayer? selectedLayer;

    [ObservableProperty]
    private int extentCount;

    [ObservableProperty]
    private Allocation.AllocationOverViewModel allocationOver = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GridAllocationLayers))]
    private string filter = string.Empty;

    [ObservableProperty]
    private bool isDetailVisible = true;

    [ObservableProperty]
    private bool isTooltipEnabled;

    [ObservableProperty]
    private short fileId = 1;

    [ObservableProperty]
    private double allocationMapHeight = 200;

    [RelayCommand]
    private void OpenPage(PageAddress pageAddress)
    {
        WeakReferenceMessenger.Default.Send(new OpenPageMessage(new OpenPageRequest(database, pageAddress)));
    }

    public List<AllocationLayer> GridAllocationLayers
        => allocationLayers.Where(w => string.IsNullOrEmpty(Filter) || w.Name.ToLower().Contains(filter.ToLower())).ToList();
}

public enum ViewLocation
{
    Bottom,
    Left,
    Hidden
}