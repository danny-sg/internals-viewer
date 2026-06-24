using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Replay.Events.EventTypes;
using InternalsViewer.UI.App.Controls.Allocation;
using InternalsViewer.UI.App.Controls.QueryReplay;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.ViewModels.Docking;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Controls.Query;

/// <summary>Dock document hosting the engine-events grid for the active query.</summary>
public sealed partial class EventsDocumentView : UserControl
{
    public QueryViewModel? ViewModel => (DataContext as DocumentViewModel)?.Query ?? DataContext as QueryViewModel;

    private QueryViewModel? _subscribed;

    public EventsDocumentView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
        Unloaded += (_, _) => Unsubscribe();
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        Bindings.Update();

        Unsubscribe();

        _subscribed = ViewModel;

        if (_subscribed is not null)
        {
            _subscribed.EventNavigationRequested += OnEventNavigationRequested;
        }
    }

    private void Unsubscribe()
    {
        if (_subscribed is not null)
        {
            _subscribed.EventNavigationRequested -= OnEventNavigationRequested;
            _subscribed = null;
        }
    }

    private void OnEventNavigationRequested(EngineEvent engineEvent) => EventGrid.NavigateToEvent(engineEvent);

    private void OnPageSelected(object? sender, PageAddressEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        var pageAddress = new PageAddress(e.FileId, e.PageId);

        WeakReferenceMessenger.Default
                              .Send(new OpenPageMessage(new OpenPageRequest(viewModel.Database, pageAddress)));
    }
}
