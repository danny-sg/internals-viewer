using System;
using Windows.System;
using Windows.UI.Core;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Events;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;
using InternalsViewer.UI.App.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs;

/// <summary>Dock document hosting the engine-events grid for the active query</summary>
public sealed partial class QueryEventsTabView : UserControl, IDisposable
{
    private QueryViewModel? _subscribed;

    public QueryEventsTabView()
    {
        InitializeComponent();

        // Keep-alive: this view is reused across re-layout, so subscriptions follow the load lifecycle
        // rather than DataContext changes (reparenting fires Unloaded/Loaded without a DataContext change).
        Loaded += (_, _) => Subscribe();
        Unloaded += (_, _) => Unsubscribe();
        DataContextChanged += OnDataContextChanged;
    }

    public QueryViewModel? ViewModel => DataContext as QueryViewModel;

    /// <summary>Disposed by <see cref="DocumentViewModel.DisposeView"/> when the query tab closes</summary>
    public void Dispose()
    {
        // x:Bind listens to the view model, which outlives the view, so the view stays rooted until
        // tracking stops
        Bindings.StopTracking();

        Unsubscribe();

        EventGrid.Dispose();
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

        var state = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);

        var isShiftPressed = state.HasFlag(CoreVirtualKeyStates.Down);

        // Shift opens the page as a separate top level tab; a plain click opens it as a document inside the
        // query view's dock layout
        if (isShiftPressed)
        {
            WeakReferenceMessenger.Default
                                  .Send(new OpenPageMessage(new OpenPageRequest(viewModel.Database, pageAddress)
                                  {
                                      LogRecords = viewModel.GetPageLogRecords(pageAddress)
                                  }));
        }
        else
        {
            viewModel.OpenPage(pageAddress);
        }
    }
}
