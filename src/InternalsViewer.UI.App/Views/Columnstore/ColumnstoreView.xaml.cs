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
    }

    public ColumnstoreTabViewModel ViewModel => (ColumnstoreTabViewModel)DataContext;

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        await ViewModel.Load();
    }

    public void Dispose() => ViewModel.Dispose();
}
