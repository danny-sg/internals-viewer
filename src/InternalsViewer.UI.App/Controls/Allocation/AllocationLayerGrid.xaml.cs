using System;
using System.Collections.ObjectModel;
using System.Linq;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.ViewModels.Allocation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using WinUI.TableView;

namespace InternalsViewer.UI.App.Controls.Allocation;

public sealed partial class AllocationLayerGrid
{
    public static readonly DependencyProperty LayersProperty
        = DependencyProperty.Register(nameof(Layers),
            typeof(ObservableCollection<AllocationLayer>),
            typeof(AllocationLayerGrid),
            new PropertyMetadata(null, OnPropertyChanged));

    public ObservableCollection<AllocationLayer> Layers
    {
        get => (ObservableCollection<AllocationLayer>)GetValue(LayersProperty);
        set => SetValue(LayersProperty, value);
    }

    public static readonly DependencyProperty SelectedLayersProperty
        = DependencyProperty.Register(nameof(SelectedLayers),
            typeof(ObservableCollection<AllocationLayer>),
            typeof(AllocationLayerGrid),
            new PropertyMetadata(new ObservableCollection<AllocationLayer>(), OnPropertyChanged));

    public ObservableCollection<AllocationLayer> SelectedLayers
    {
        get => (ObservableCollection<AllocationLayer>)GetValue(SelectedLayersProperty);
        set => SetValue(SelectedLayersProperty, value);
    }

    public AllocationLayerGrid()
    {
        InitializeComponent();

        // handledEventsToo so the press still reaches us after the row has handled it for its own selection.
        LayerTable.AddHandler(UIElement.PointerPressedEvent,
                              new PointerEventHandler(LayerTable_OnPointerPressed),
                              handledEventsToo: true);
    }

    public event EventHandler<PageAddressEventArgs>? PageClicked;

    public event EventHandler<PageAddressEventArgs>? ViewIndexClicked;

    public event EventHandler<long>? ViewColumnstoreClicked;

    public AllocationLayerGridViewModel ViewModel { get; } = new();

    private void LayerTable_OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var layers = SelectedLayers.ToList();

        var source = e.OriginalSource as DependencyObject;

        if (LayoutHelpers.FindParent<ButtonBase>(source) != null)
        {
            e.Handled = true;
            return;
        }

        var row = LayoutHelpers.FindParent<TableViewRow>(source);

        if (row == null)
        {
            return;
        }

        var layerRow = (AllocationLayerRow)row.Content;

        var layer = layerRow.Layer;

        // Snapshot selection state before the table's own handler changes anything
        var wasSelected = layers.Contains(layer);

        var isShiftHeld = (e.KeyModifiers & Windows.System.VirtualKeyModifiers.Shift) != 0;

        if (isShiftHeld)
        {
            if (wasSelected)
            {
                layers.Remove(layer);
            }
            else
            {
                layers.Add(layer);
            }
        }
        else
        {
            layers.Clear();

            if (!wasSelected)
            {
                layers.Add(layer);
            }
        }
        SelectedLayers = new ObservableCollection<AllocationLayer>(layers);

        LayerTable.SelectedItem = SelectedLayers.Count == 1 && SelectedLayers[0] == layer ? ViewModel.FindRow(layerRow) : null;

        e.Handled = true;
    }

    private void ExpanderButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is AllocationLayerRow row)
        {
            ViewModel.ToggleExpanded(row);
        }
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

    private void ViewColumnstoreButton_Click(object sender, RoutedEventArgs e)
    {
        var allocationUnitId = (long)((HyperlinkButton)sender).Tag;

        ViewColumnstoreClicked?.Invoke(this, allocationUnitId);
    }

    private void LayerTable_OnSorting(object sender, TableViewSortingEventArgs e)
    {
        var tag = e.Column.Tag as string;

        if (string.IsNullOrEmpty(tag))
        {
            return;
        }

        e.Handled = true;

        var ascending = e.Column.SortDirection != SortDirection.Ascending;

        foreach (var column in LayerTable.Columns)
        {
            column.SortDirection = null;
        }

        e.Column.SortDirection = ascending ? SortDirection.Ascending : SortDirection.Descending;

        ViewModel.Sort(tag, ascending);
    }

    private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (AllocationLayerGrid)d;

        if (e.Property == LayersProperty)
        {
            var layers = (ObservableCollection<AllocationLayer>)e.NewValue;

            control.ViewModel.SetLayers([.. layers.Where(l => !l.IsAllocationLayer)]);
        }

        if (e.Property == SelectedLayersProperty)
        {
            control.ViewModel.SelectedLayers = (ObservableCollection<AllocationLayer>)e.NewValue;
        }
    }
}
