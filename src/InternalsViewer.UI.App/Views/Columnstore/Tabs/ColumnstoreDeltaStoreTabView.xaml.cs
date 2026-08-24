using System;
using System.Threading;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.Models.Columnstore.Segment;
using InternalsViewer.UI.App.ViewModels.Columnstore;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Columnstore.Tabs;

public sealed partial class ColumnstoreDeltaStoreTabView : UserControl, IDisposable
{
    private readonly CancellationTokenSource _cts = new();

    public ColumnstoreDeltaStoreTabView()
    {
        InitializeComponent();

        Loaded += OnLoaded;

        // x:Bind resolves against the view, and the dock sets DataContext after the view is built
        DataContextChanged += OnDataContextChanged;
    }

    public DeltaStoreTabViewModel ViewModel => (DeltaStoreTabViewModel)DataContext;

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args) => Bindings.Update();

    private void Page_OnClick(object sender, RoutedEventArgs e)
    {
        if (((HyperlinkButton)sender).Tag is DeltaStorePageSummary page)
        {
            OpenPage(page.PageAddress);
        }
    }

    private void FirstPage_OnClick(object sender, RoutedEventArgs e) => OpenPage(ViewModel.FirstPage);

    private void FirstIamPage_OnClick(object sender, RoutedEventArgs e) => OpenPage(ViewModel.FirstIamPage);

    private async void OpenPage(PageAddress pageAddress)
    {
        if (pageAddress == PageAddress.Empty)
        {
            return;
        }

        await WeakReferenceMessenger.Default.Send(
            new OpenPageMessage(new OpenPageRequest(ViewModel.Database, pageAddress)));
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        await ViewModel.Load(_cts.Token);
    }

    public void Dispose()
    {
        Loaded -= OnLoaded;

        DataContextChanged -= OnDataContextChanged;

        // x:Bind listens to the view model, which outlives the view, so the view stays rooted until tracking stops
        Bindings.StopTracking();

        _cts.Cancel();
        _cts.Dispose();
    }
}
