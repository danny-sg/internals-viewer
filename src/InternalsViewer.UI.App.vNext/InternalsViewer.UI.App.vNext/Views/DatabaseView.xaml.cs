using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.Internals.Providers;
using InternalsViewer.Internals.Services.Loaders.Engine;
using InternalsViewer.UI.App.vNext.Services;
using InternalsViewer.UI.App.vNext.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using InternalsViewer.UI.App.vNext.Models;
using CommunityToolkit.WinUI.UI.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace InternalsViewer.UI.App.vNext.Views;
public sealed partial class DatabaseView : UserControl
{
    public IDatabaseLoader DatabaseLoader { get; set; }

    public CurrentConnection Connection { get; set; }

    public DatabaseView()
    {
        this.InitializeComponent();
    }

    public DatabaseViewModel ViewModel { get; set; } = new() { Size = 0 };

    private async void AppBarButton_Click(object sender, RoutedEventArgs e)
    {
        Connection.ConnectionString = "Server=.;Database=master;Integrated Security=true;TrustServerCertificate=True";
        Connection.DatabaseName = "AdventureWorks2022";

        var database = await DatabaseLoader.Load("AdventureWorks2022");

        var layers = AllocationLayerBuilder.GenerateLayers(database, true);

        ViewModel.Size = database.GetFileSize(1);
        ViewModel.AllocationLayers = new ObservableCollection<AllocationLayer>(layers);
    }

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
}
