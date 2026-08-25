using System;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.Models.Columnstore;
using InternalsViewer.UI.App.ViewModels.Columnstore;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Columnstore.Tabs;

public sealed partial class ColumnstoreMetadataTabView : IDisposable
{
    public ColumnstoreMetadataTabView()
    {
        InitializeComponent();

        // x:Bind resolves against the view, and the dock sets DataContext after the view is built
        DataContextChanged += OnDataContextChanged;
    }

    public ColumnstoreTabViewModel ViewModel => (ColumnstoreTabViewModel)DataContext;

    private ColumnstoreTabViewModel? _tracked;

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        Bindings.Update();

        if (_tracked is not null)
        {
            _tracked.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _tracked = DataContext as ColumnstoreTabViewModel;

        if (_tracked is not null)
        {
            _tracked.PropertyChanged += OnViewModelPropertyChanged;
        }

        RealizePanels();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => RealizePanels();

    private void RealizePanels()
    {
        if (_tracked is { IsDictionariesTabLoaded: true } && DictionaryTable is null)
        {
            FindName(nameof(DictionaryTable));
        }
    }

    private Visibility GetTabContentVisibility(int selected, int index)
        => selected == index ? Visibility.Visible : Visibility.Collapsed;

    private void Dictionary_OnClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is DictionarySummary summary)
        {
            ViewModel.OpenDictionary(summary.Dictionary);
        }
    }

    public void Dispose()
    {
        DataContextChanged -= OnDataContextChanged;

        if (_tracked is not null)
        {
            _tracked.PropertyChanged -= OnViewModelPropertyChanged;

            _tracked = null;
        }

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

    private async void DictionaryDataPointerButton_Click(object sender, RoutedEventArgs e)
    {
        if (((HyperlinkButton)sender).Tag is not DictionarySummary { HasDataPointer: true } dictionary)
        {
            return;
        }

        var request = new OpenPageRequest(ViewModel.Database, dictionary.DataPage) { Slot = dictionary.DataSlot };

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
