using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.UI.App.Controls.Allocation;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs;

/// <summary>Dock document hosting the engine-events grid for the active query.</summary>
public sealed partial class EventsDocumentView : UserControl
{
    public QueryViewModel? ViewModel => DataContext as QueryViewModel;

    private QueryViewModel? _subscribed;

    public EventsDocumentView()
    {
        InitializeComponent();

        // Keep-alive: this view is reused across re-layout, so subscriptions follow the load lifecycle
        // rather than DataContext changes (reparenting fires Unloaded/Loaded without a DataContext change).
        Loaded += (_, _) => Subscribe();
        Unloaded += (_, _) => Unsubscribe();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        Bindings.Update();
        Subscribe();
    }

    private void Subscribe()
    {
        if (ReferenceEquals(_subscribed, ViewModel))
        {
            return;
        }

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
