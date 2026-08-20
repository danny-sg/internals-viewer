using System;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query;

public sealed partial class QueryView : Page, IDisposable
{
    public QueryViewModel ViewModel => (QueryViewModel)DataContext;

    public QueryView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;

        Unloaded += OnUnloaded;
    }

    public void Dispose()
    {
        // x:Bind listens to the view model, which outlives the view, so the view stays rooted until
        // tracking stops
        Bindings.StopTracking();

        (DataContext as QueryViewModel)?.Dispose();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not QueryViewModel viewModel)
        {
            return;
        }

        DockHostControl.CaptureSizes();

        _ = viewModel.SaveLayoutAsync();
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        Bindings.Update();
    }
}
