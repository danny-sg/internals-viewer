using System;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.Models.Columnstore;
using InternalsViewer.UI.App.ViewModels.Columnstore;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Columnstore.Tabs;

public sealed partial class ColumnstoreRowGroupsTabView : IDisposable
{
    public ColumnstoreRowGroupsTabView()
    {
        InitializeComponent();

        // x:Bind resolves against the view, and the dock sets DataContext after the view is built
        DataContextChanged += OnDataContextChanged;
    }

    public ColumnstoreTabViewModel ViewModel => (ColumnstoreTabViewModel)DataContext;

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args) => Bindings.Update();

    public void Dispose()
    {
        DataContextChanged -= OnDataContextChanged;

        // x:Bind listens to the view model, which outlives the view, so the view stays rooted until tracking stops
        Bindings.StopTracking();
    }

    private async void DataPointerButton_Click(object sender, RoutedEventArgs e)
    {
        if (((HyperlinkButton)sender).Tag is not SegmentSummary { HasDataPointer: true } segment)
        {
            return;
        }

        var request = new OpenPageRequest(ViewModel.Database, segment.DataPage) { Slot = segment.DataSlot };

        await WeakReferenceMessenger.Default.Send(new OpenPageMessage(request));
    }

    private void ViewDictionaryButton_Click(object sender, RoutedEventArgs e)
    {
        if (((HyperlinkButton)sender).Tag is SegmentSummary { HasDictionary: true } segment)
        {
            ViewModel.OpenDictionary(segment);
        }
    }

    private void ViewSegmentButton_Click(object sender, RoutedEventArgs e)
    {
        if (((HyperlinkButton)sender).Tag is SegmentSummary segment)
        {
            ViewModel.OpenSegment(segment);
        }
    }
}
