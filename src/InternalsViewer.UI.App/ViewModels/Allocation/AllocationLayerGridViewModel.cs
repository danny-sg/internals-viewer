using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using InternalsViewer.UI.App.Models;

namespace InternalsViewer.UI.App.ViewModels.Allocation;

[ObservableObject]
public partial class AllocationLayerGridViewModel
{
    private readonly string[] refreshProperties = { nameof(Filter), nameof(DataSource) };

    private List<AllocationLayer> Layers { get; set; } = [];

    [ObservableProperty]
    private string filter = string.Empty;

    [ObservableProperty]
    private ObservableCollection<AllocationLayer> selectedLayers = [];

    [ObservableProperty]
    private ObservableCollection<AllocationLayer> dataSource = [];

    partial void OnFilterChanged(string? oldValue, string newValue)
    {
        RefreshDataSource();
    }

    private void RefreshDataSource()
    {
        var filteredLayers = Layers.Where(l => l.Name.Contains(Filter, StringComparison.OrdinalIgnoreCase) 
                                               || string.IsNullOrEmpty(Filter));

        DataSource = new ObservableCollection<AllocationLayer>(filteredLayers);
    }

    private void AllocationLayerGridViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if(refreshProperties.Contains(e.PropertyName))
        {
            RefreshDataSource();
        }
    }

    public void SetLayers(List<AllocationLayer> value)
    {
        Layers = value;
        RefreshDataSource();
    }
}