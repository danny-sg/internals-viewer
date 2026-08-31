using System;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.Models.Columnstore;
using InternalsViewer.UI.App.ViewModels.Columnstore;
using InternalsViewer.UI.App.Controls;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.UI.App.Views.Columnstore.Tabs;

public sealed partial class ColumnstoreMetadataTabView : ICellLinkNavigator, IDisposable
{
    private ColumnstoreTabViewModel? _tracked;

    public ColumnstoreMetadataTabView()
    {
        InitializeComponent();

        // x:Bind resolves against the view, and the dock sets DataContext after the view is built
        DataContextChanged += OnDataContextChanged;
    }

    public ColumnstoreTabViewModel ViewModel => (ColumnstoreTabViewModel)DataContext;

    public void OnLinkInvoked(string kind, object? parameter)
    {
        switch (kind)
        {
            case "Segment" when parameter is SegmentSummary segment:
                ViewModel.OpenSegment(segment);

                break;

            case "SegmentDictionary" when parameter is SegmentSummary { HasDictionary: true } withDictionary:
                ViewModel.OpenDictionary(withDictionary);

                break;

            case "Dictionary" when parameter is DictionarySummary summary:
                ViewModel.OpenDictionary(summary.Dictionary);

                break;

            case "SegmentDataPointer" when parameter is SegmentSummary { HasDataPointer: true } source:
                OpenPage(source.DataPage, source.DataSlot);

                break;

            case "DictionaryDataPointer" when parameter is DictionarySummary { HasDataPointer: true } dictionary:
                OpenPage(dictionary.DataPage, dictionary.DataSlot);

                break;
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

    private async void OpenPage(PageAddress address, ushort? slot)
    {
        var request = new OpenPageRequest(ViewModel.Database, address) { Slot = slot };

        await WeakReferenceMessenger.Default.Send(new OpenPageMessage(request));
    }

}
