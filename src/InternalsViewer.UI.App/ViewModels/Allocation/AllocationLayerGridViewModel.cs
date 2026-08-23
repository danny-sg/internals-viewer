using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.UI.App.Controls.Allocation;
using InternalsViewer.UI.App.Models;

namespace InternalsViewer.UI.App.ViewModels.Allocation;

public sealed partial class AllocationLayerGridViewModel : ObservableObject
{
    private readonly string[] _refreshProperties = [nameof(Filter), nameof(DataSource)];

    private readonly Dictionary<string, bool> _expansionOverrides = [];

    private List<AllocationLayer> Layers { get; set; } = [];

    private string SortProperty { get; set; } = string.Empty;

    private bool SortAscending { get; set; } = true;

    [ObservableProperty]
    private string _filter = string.Empty;

    [ObservableProperty]
    private ObservableCollection<AllocationLayer> _selectedLayers = [];

    [ObservableProperty]
    private ObservableCollection<AllocationLayerRow> _dataSource = [];

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

        var rows = new ObservableCollection<AllocationLayerRow>();

        foreach (var layer in filteredLayers)
        {
            var row = AllocationLayerRow.ForLayer(layer, _expansionOverrides);

            rows.Add(row);

            foreach (var descendant in VisibleDescendants(row))
            {
                rows.Add(descendant);
            }
        }

        DataSource = rows;
    }

    private static IEnumerable<AllocationLayerRow> VisibleDescendants(AllocationLayerRow row)
    {
        if (!row.IsExpanded)
        {
            yield break;
        }

        foreach (var child in row.Children)
        {
            yield return child;

            foreach (var descendant in VisibleDescendants(child))
            {
                yield return descendant;
            }
        }
    }

    public void ToggleExpanded(AllocationLayerRow row)
    {
        if (!row.HasChildren)
        {
            return;
        }

        var index = DataSource.IndexOf(row);

        if (index < 0)
        {
            return;
        }

        row.IsExpanded = !row.IsExpanded;

        _expansionOverrides[row.Key] = row.IsExpanded;

        if (row.IsExpanded)
        {
            var insertAt = index + 1;

            foreach (var descendant in VisibleDescendants(row))
            {
                DataSource.Insert(insertAt++, descendant);
            }
        }
        else
        {
            while (index + 1 < DataSource.Count && DataSource[index + 1].Depth > row.Depth)
            {
                DataSource.RemoveAt(index + 1);
            }
        }
    }

    public AllocationLayerRow? FindRow(AllocationLayerRow row)
    {
        return DataSource.FirstOrDefault(r => r.Key == row.Key);
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
