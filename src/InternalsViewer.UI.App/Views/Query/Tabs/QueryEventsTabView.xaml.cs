using System;
using System.ComponentModel;
using Windows.System;
using Windows.UI.Core;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Properties;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;
using InternalsViewer.UI.App.Controls;
using InternalsViewer.UI.App.ViewModels.Docking;

namespace InternalsViewer.UI.App.Views.Query.Tabs;

/// <summary>
/// Dock document hosting the engine-events grid for the active query
/// </summary>
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

        Loaded += (_, _) => AttachStructureResolver();
    }

    public QueryViewModel? ViewModel => DataContext as QueryViewModel;

    public bool IsDetailPaneVisible
    {
        get => ViewModel?.IsEventDetailsVisible == true;
        set
        {
            if (ViewModel is { } viewModel)
            {
                viewModel.IsEventDetailsVisible = value;
            }

            RefreshDetailPane();
        }
    }

    public bool IsDetailPaneDockedBottom
    {
        get => ViewModel?.IsEventDetailsDockedBottom == true;
        set
        {
            if (ViewModel is { } viewModel)
            {
                viewModel.IsEventDetailsDockedBottom = value;
            }

            RefreshDetailPane();
        }
    }

    public GridLength BodyColumnWidth
        => IsDetailPaneVisible && !IsDetailPaneDockedBottom
            ? new GridLength(6, GridUnitType.Star)
            : new GridLength(1, GridUnitType.Star);

    public GridLength DetailColumnWidth
        => IsDetailPaneVisible && !IsDetailPaneDockedBottom ? new GridLength(4, GridUnitType.Star) : new GridLength(0);

    public GridLength BodyRowHeight
        => IsDetailPaneVisible && IsDetailPaneDockedBottom
            ? new GridLength(6, GridUnitType.Star)
            : new GridLength(1, GridUnitType.Star);

    public GridLength DetailRowHeight
        => IsDetailPaneVisible && IsDetailPaneDockedBottom ? new GridLength(4, GridUnitType.Star) : new GridLength(0);

    public Visibility DetailPaneVisibility
        => IsDetailPaneVisible ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ColumnSplitterVisibility
        => IsDetailPaneVisible && !IsDetailPaneDockedBottom ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RowSplitterVisibility
        => IsDetailPaneVisible && IsDetailPaneDockedBottom ? Visibility.Visible : Visibility.Collapsed;

    public Thickness DetailPaneBorderThickness
        => IsDetailPaneDockedBottom ? new Thickness(0, 1, 0, 0) : new Thickness(1, 0, 0, 0);

    public string DockToggleGlyph => IsDetailPaneDockedBottom ? "" : "";

    public string DockToggleTooltip => IsDetailPaneDockedBottom ? "Dock Right" : "Dock Bottom";

    public static Visibility LinkVisibility(EventPropertyType type)
        => type is EventPropertyType.PageAddress or EventPropertyType.RowIdentifier ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility TextVisibility(EventPropertyType type)
        => type is EventPropertyType.PageAddress or EventPropertyType.RowIdentifier ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>
    /// Disposed by <see cref="DocumentViewModel.DisposeView"/> when the query tab closes
    /// </summary>
    public void Dispose()
    {
        // x:Bind listens to the view model, which outlives the view, so the view stays rooted until tracking stops
        Bindings.StopTracking();

        Unsubscribe();

        EventGrid.Dispose();
    }

    private void AttachStructureResolver()
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        var cache = App.GetService<ColumnstoreCache>();

        EventGrid.ResolveStructure = page => ColumnstoreStructureText.Describe(cache.GetPageReads(viewModel.Database, page));
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        RefreshDetailPane();
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
            _subscribed.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void Unsubscribe()
    {
        if (_subscribed is not null)
        {
            _subscribed.EventNavigationRequested -= OnEventNavigationRequested;
            _subscribed.PropertyChanged -= OnViewModelPropertyChanged;
            _subscribed = null;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(QueryViewModel.IsEventDetailsVisible) or nameof(QueryViewModel.IsEventDetailsDockedBottom))
        {
            RefreshDetailPane();
        }
    }

    private void OnEventNavigationRequested(EngineEvent engineEvent) => EventGrid.NavigateToEvent(engineEvent);

    private void CloseDetailPane()
    {
        IsDetailPaneVisible = false;
    }

    private void ToggleDetailPaneDock()
    {
        IsDetailPaneDockedBottom = !IsDetailPaneDockedBottom;
    }

    private void RefreshDetailPane()
    {
        Grid.SetRow(DetailPane, IsDetailPaneDockedBottom ? 2 : 0);
        Grid.SetColumn(DetailPane, IsDetailPaneDockedBottom ? 0 : 2);

        Bindings.Update();
    }

    private void OnPageSelected(object? sender, PageAddressEventArgs e) => OpenPage(new PageAddress(e.FileId, e.PageId), e.Slot);

    private void OnPropertyLinkClick(object sender, RoutedEventArgs e)
    {
        switch (sender)
        {
            case HyperlinkButton { DataContext: EventProperty { Row: { } row } }:
                OpenPage(row.PageAddress, row.SlotId);

                break;

            case HyperlinkButton { DataContext: EventProperty { Page: { } page } }:
                OpenPage(page);

                break;
        }
    }

    private void OpenPage(PageAddress pageAddress, ushort? slot = null)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        var state = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);

        var isShiftPressed = state.HasFlag(CoreVirtualKeyStates.Down);

        // Shift opens the page as a separate top level tab; a plain click opens it as a document inside the query view's dock layout
        if (isShiftPressed)
        {
            WeakReferenceMessenger.Default
                                  .Send(new OpenPageMessage(new OpenPageRequest(viewModel.Database, pageAddress)
                                  {
                                      Slot = slot,
                                      LogRecords = viewModel.GetPageLogRecords(pageAddress)
                                  }));
        }
        else
        {
            viewModel.OpenPage(pageAddress, slot);
        }
    }
}
