using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.WinUI.UI.Controls;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.ViewModels.Allocation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace InternalsViewer.UI.App.Controls.Allocation;

public sealed partial class AllocationLayerGrid
{
    public event EventHandler<PageAddressEventArgs>? PageClicked;

    public event EventHandler<PageAddressEventArgs>? ViewIndexClicked;

    public AllocationLayerGridViewModel ViewModel { get; } = new();

    public ObservableCollection<AllocationLayer> Layers
    {
        get => (ObservableCollection<AllocationLayer>)GetValue(LayersProperty);
        set => SetValue(LayersProperty, value);
    }

    public static readonly DependencyProperty LayersProperty
        = DependencyProperty.Register(nameof(Layers),
            typeof(ObservableCollection<AllocationLayer>),
            typeof(AllocationLayerGrid),
            new PropertyMetadata(default, OnPropertyChanged));

    public ObservableCollection<AllocationLayer> SelectedLayers
    {
        get => (ObservableCollection<AllocationLayer>)GetValue(SelectedLayersProperty);
        set => SetValue(SelectedLayersProperty, value);
    }

    public static readonly DependencyProperty SelectedLayersProperty
        = DependencyProperty.Register(nameof(SelectedLayers),
            typeof(ObservableCollection<AllocationLayer>),
            typeof(AllocationLayerGrid),
            new PropertyMetadata(new ObservableCollection<AllocationLayer>(), OnPropertyChanged));

    public AllocationLayerGrid()
    {
        InitializeComponent();
    }

    private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (AllocationLayerGrid)d;

        if(e.Property == LayersProperty)
        {
            var layers = (ObservableCollection<AllocationLayer>)e.NewValue;

            control.ViewModel.SetLayers(layers.ToList());
        }

        if(e.Property == SelectedLayersProperty)
        {
            control.ViewModel.SelectedLayers = (ObservableCollection<AllocationLayer>)e.NewValue;
        }
    }

    private void DataGrid_OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;

        // Clicks on the page-address / view-index links navigate; they must not toggle row selection.
        // Marking the event handled stops the DataGrid from selecting the row underneath the link.
        if (LayoutHelpers.FindParent<ButtonBase>(source) != null)
        {
            e.Handled = true;
            return;
        }

        var row = LayoutHelpers.FindParent<DataGridRow>(source);

        if (row == null)
        {
            return;
        }

        var layer = (AllocationLayer)row.DataContext;

        // Snapshot selection state before the DataGrid's own handler changes anything
        var wasSelected = SelectedLayers.Contains(layer);

        var isShiftHeld = (e.KeyModifiers & Windows.System.VirtualKeyModifiers.Shift) != 0;

        if (isShiftHeld)
        {
            if (wasSelected)
            {
                SelectedLayers.Remove(layer);
            }
            else
            {
                SelectedLayers.Add(layer);
            }
        }
        else
        {
            SelectedLayers.Clear();

            if (!wasSelected)
            {
                SelectedLayers.Add(layer);
            }
        }

        DataGrid.SelectedItem = SelectedLayers.Count == 1 ? (object)SelectedLayers[0] : null;

        e.Handled = true;
    }

    private void HyperlinkButton_Click(object sender, RoutedEventArgs e)
    {
        var pageAddress = (PageAddress)((HyperlinkButton)sender).Tag;

        PageClicked?.Invoke(this, new PageAddressEventArgs(pageAddress.FileId, pageAddress.PageId));
    }

    private void ViewIndexButton_Click(object sender, RoutedEventArgs e)
    {
        var pageAddress = (PageAddress)((HyperlinkButton)sender).Tag;

        ViewIndexClicked?.Invoke(this, new PageAddressEventArgs(pageAddress.FileId, pageAddress.PageId));
    }

    private void DataGrid_OnSorting(object sender, DataGridColumnEventArgs e)
    {
        var tag = e.Column.Tag as string;

        if (string.IsNullOrEmpty(tag))
        {
            return;
        }

        var ascending = e.Column.SortDirection != DataGridSortDirection.Ascending;

        foreach (var column in DataGrid.Columns)
        {
            column.SortDirection = null;
        }

        e.Column.SortDirection = ascending ? DataGridSortDirection.Ascending : DataGridSortDirection.Descending;

        ViewModel.Sort(tag, ascending);
    }
}
