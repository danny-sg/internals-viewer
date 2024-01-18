using System;
using System.Collections.ObjectModel;
using CommunityToolkit.WinUI.UI.Controls;
using InternalsViewer.UI.App.vNext.Helpers;
using InternalsViewer.UI.App.vNext.Models;
using Microsoft.UI.Xaml.Input;

namespace InternalsViewer.UI.App.vNext.Controls.Allocation;

public sealed partial class AllocationLayerGrid
{
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

    public AllocationLayer? SelectedLayer {
        get => (AllocationLayer?)GetValue(SelectedLayerProperty);
        set => SetValue(SelectedLayerProperty, value);
    }

    public static readonly DependencyProperty SelectedLayerProperty
        = DependencyProperty.Register(nameof(SelectedLayer),
            typeof(AllocationLayer),
            typeof(AllocationLayerGrid),
            new PropertyMetadata(default, OnPropertyChanged));

    public string Filter { get; set; } = string.Empty;

    public AllocationLayerGrid()
    {
        InitializeComponent();
    }

    private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {

    }

    private void DataGrid_OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var row = LayoutHelpers.FindParent<DataGridRow>(e.OriginalSource as DependencyObject);

        if (row != null)
        {
            var layer = (AllocationLayer)row.DataContext;

            if (SelectedLayer == layer)
            {
                SelectedLayer = null;
                DataGrid.SelectedItem = null;
            }
            else
            {
                SelectedLayer = layer;
                DataGrid.SelectedItem = layer;
            }

            e.Handled = true;
        }
    }
}
