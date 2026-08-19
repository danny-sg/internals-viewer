using System;
using InternalsViewer.UI.App.ViewModels.Columnstore;
using Microsoft.UI.Xaml;

namespace InternalsViewer.UI.App.Views.Columnstore;

public sealed partial class ColumnstoreView : IDisposable
{
    public ColumnstoreView()
    {
        InitializeComponent();

        Loaded += OnLoaded;

        // x:Bind resolves against the view, and the dock sets DataContext after the view is built
        DataContextChanged += (_, _) => Bindings.Update();
    }

    public ColumnstoreTabViewModel ViewModel => (ColumnstoreTabViewModel)DataContext;

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        await ViewModel.Load();
    }

    public void Dispose() => ViewModel.Dispose();
}
