using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.UI.App.Models;

namespace InternalsViewer.UI.App.ViewModels.Allocation;

public sealed partial class AllocationLayerGridViewModel : ObservableObject
{
    private readonly string[] _refreshProperties = [nameof(Filter), nameof(DataSource)];

    private List<AllocationLayer> Layers { get; set; } = [];

    private string SortProperty { get; set; } = string.Empty;

    private bool SortAscending { get; set; } = true;

    [ObservableProperty]
    private string _filter = string.Empty;

    [ObservableProperty]
    private ObservableCollection<AllocationLayer> _selectedLayers = [];

    [ObservableProperty]
    private ObservableCollection<AllocationLayer> _dataSource = [];

    partial void OnFilterChanged(string? oldValue, string newValue)
    {
        RefreshDataSource();
    }

    private void RefreshDataSource()
    {
        var filteredLayers = Layers.Where(l => l.Name.Contains(Filter, StringComparison.OrdinalIgnoreCase)
                                               || string.IsNullOrEmpty(Filter));

        if (!string.IsNullOrEmpty(SortProperty))
        {
            filteredLayers = SortAscending
                ? filteredLayers.OrderBy(l => GetSortValue(l, SortProperty))
                : filteredLayers.OrderByDescending(l => GetSortValue(l, SortProperty));
        }

        DataSource = new ObservableCollection<AllocationLayer>(filteredLayers);
    }

    private static IComparable? GetSortValue(AllocationLayer layer, string property) => property switch
    {
        nameof(AllocationLayer.ObjectName) => layer.ObjectName,
        nameof(AllocationLayer.IndexName) => layer.IndexName,
        nameof(AllocationLayer.IndexTypeDescription) => layer.IndexTypeDescription,
        nameof(AllocationLayer.TotalPages) => layer.TotalPages,
        _ => null
    };

    private void AllocationLayerGridViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_refreshProperties.Contains(e.PropertyName))
        {
            RefreshDataSource();
        }
    }

    public void Sort(string property, bool ascending)
    {
        SortProperty = property;
        SortAscending = ascending;
        RefreshDataSource();
    }

    public void SetLayers(List<AllocationLayer> value)
    {
        Layers = value;
        RefreshDataSource();
    }
}