using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using InternalsViewer.UI.App.vNext.Models;
using CommunityToolkit.WinUI.UI.Controls;
using InternalsViewer.UI.App.vNext.Controls;
using DatabaseViewModel = InternalsViewer.UI.App.vNext.ViewModels.Tabs.DatabaseViewModel;

namespace InternalsViewer.UI.App.vNext.Views;

public sealed partial class DatabaseView
{
    public DatabaseView()
    {
        InitializeComponent();
    }

    public DatabaseViewModel ViewModel => (DatabaseViewModel)DataContext;

    private void DataGrid_OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var row = FindParent<DataGridRow>(e.OriginalSource as DependencyObject);

        if(row != null)
        {
            var layer = (AllocationLayer)row.DataContext;

            if (ViewModel.SelectedLayer == layer)
            {
                ViewModel.SelectedLayer = null;
                DataGrid.SelectedItem = null;
            }
            else
            {
                ViewModel.SelectedLayer = layer;
                DataGrid.SelectedItem = layer;  
            }

            e.Handled = true;
        }   
    }

    private T? FindParent<T>(DependencyObject? source) where T : DependencyObject
    {
        var target = source;

        while (target != null && target is not T)
        {
            target = VisualTreeHelper.GetParent(target);
        }

        return target as T;
    }

    private async void Allocations_OnPageClicked(object? sender, PageClickedEventArgs e)
    {
       await ViewModel.Parent.OpenPage(ViewModel.Database, new Internals.Engine.Address.PageAddress(1, e.PageId));
    }
}
