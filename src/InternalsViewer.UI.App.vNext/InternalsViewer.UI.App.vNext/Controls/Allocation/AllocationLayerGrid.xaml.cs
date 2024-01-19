using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.WinUI.UI.Controls;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.UI.App.vNext.Helpers;
using InternalsViewer.UI.App.vNext.Models;
using InternalsViewer.UI.App.vNext.ViewModels.Allocation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace InternalsViewer.UI.App.vNext.Controls.Allocation;

public sealed partial class AllocationLayerGrid
{
    public AllocationLayerGridViewModel ViewModel { get; set; } = new();

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

    public event EventHandler<PageClickedEventArgs>? PageClicked;

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

        if(e.Property == SelectedLayerProperty)
        {
            control.ViewModel.SelectedLayer = (AllocationLayer)e.NewValue;
        }
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

    private void HyperlinkButton_Click(object sender, RoutedEventArgs e)
    {
        var pageAddress = (PageAddress)((HyperlinkButton)sender).Tag;

        PageClicked?.Invoke(this, new PageClickedEventArgs(pageAddress.FileId, pageAddress.PageId));
    }
}
