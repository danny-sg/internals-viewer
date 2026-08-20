using System;
using InternalsViewer.UI.App.ViewModels.Columnstore;

namespace InternalsViewer.UI.App.Views.Columnstore;

public sealed partial class ColumnstoreView : IDisposable
{
    public ColumnstoreView()
    {
        InitializeComponent();

        Loaded += OnLoaded;

        // x:Bind resolves against the view, and the dock sets DataContext after the view is built
        DataContextChanged += OnDataContextChanged;
    }

    public ColumnstoreTabViewModel ViewModel => (ColumnstoreTabViewModel)DataContext;

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args) => Bindings.Update();

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        await ViewModel.Load();
    }

    public void Dispose()
    {
        Loaded -= OnLoaded;

        DataContextChanged -= OnDataContextChanged;

        // x:Bind listens to the view model, which outlives the view, so the view stays rooted until tracking stops
        Bindings.StopTracking();

        ViewModel.Dispose();
    }
}
