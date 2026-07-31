using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.WinUI.UI.Controls;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Interfaces.Events;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Controls.EventGrid;

public sealed partial class EventGridControl : UserControl, IDisposable
{
    private static readonly SolidColorBrush InScopeBrush =
        new(Windows.UI.Color.FromArgb(20, 121, 251, 155));

    public event EventHandler<PageAddressEventArgs>? PageClicked;

    private readonly TappedEventHandler _tappedHandler;

    public EngineEvent? SelectedItem
    {
        get => (EngineEvent?)GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(nameof(SelectedItem), typeof(EngineEvent), typeof(EventGridControl),
            new PropertyMetadata(null, OnSelectedItemChanged));

    private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (EventGridControl)d;

        if (!ReferenceEquals((control.DataGrid.SelectedItem as EventGridRow)?.Event, e.NewValue))
        {
            control.SelectRow(e.NewValue as EngineEvent);
        }
    }

    private void SelectRow(EngineEvent? engineEvent)
    {
        if (engineEvent is null)
        {
            DataGrid.SelectedItem = null;

            return;
        }

        if (_parentOf.TryGetValue(engineEvent, out var parent) && _expanded.Add(parent))
        {
            ApplyFilter();
        }

        DataGrid.SelectedItem = FindRow(engineEvent);
    }

    private EventGridRow? FindRow(EngineEvent engineEvent) =>
        (DataGrid.ItemsSource as IEnumerable<EventGridRow>)?.FirstOrDefault(r => ReferenceEquals(r.Event, engineEvent));

    public List<EngineEvent> Events
    {
        get => (List<EngineEvent>)GetValue(EventsProperty);
        set => SetValue(EventsProperty, value);
    }

    public static readonly DependencyProperty EventsProperty =
        DependencyProperty.Register(nameof(Events), typeof(List<EngineEvent>), typeof(EventGridControl),
            new PropertyMetadata(null, OnEventsChanged));

    private static void OnEventsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (EventGridControl)d;

        control.RebuildParentMap();
        control.ApplyFilter();
    }

    private void RebuildParentMap()
    {
        _parentOf.Clear();
        _expanded.Clear();

        foreach (var group in (Events).OfType<IEventGroup>())
        {
            foreach (var child in group.Events)
            {
                _parentOf[child] = (EngineEvent)group;
            }
        }
    }

    public long SequenceFrom
    {
        get => (long)GetValue(SequenceFromProperty);
        set => SetValue(SequenceFromProperty, value);
    }

    public static readonly DependencyProperty SequenceFromProperty =
        DependencyProperty.Register(nameof(SequenceFrom), typeof(long), typeof(EventGridControl),
            new PropertyMetadata(0L, OnSequenceRangeChanged));

    public long SequenceTo
    {
        get => (long)GetValue(SequenceToProperty);
        set => SetValue(SequenceToProperty, value);
    }

    public static readonly DependencyProperty SequenceToProperty =
        DependencyProperty.Register(nameof(SequenceTo), typeof(long), typeof(EventGridControl),
            new PropertyMetadata(0L, OnSequenceRangeChanged));

    private static void OnSequenceRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((EventGridControl)d).RefreshRowHighlights();
    }

    private readonly Dictionary<DataGridRow, EngineEvent> _visibleRows = new();

    // Read groups the user has expanded, and the parent group of each child event (for expanding on selection).
    private readonly HashSet<EngineEvent> _expanded = new(ReferenceEqualityComparer.Instance);

    private readonly Dictionary<EngineEvent, EngineEvent> _parentOf = new(ReferenceEqualityComparer.Instance);

    // The grid's bound rows. Expand/collapse mutates this in place so the DataGrid keeps its scroll offset; only a
    // filter/sort/new-events change replaces it.
    private ObservableCollection<EventGridRow> _rows = [];

    private string? _sortTag;
    private bool _sortAscending = true;

    // Whether the in-progress click changed the selection: a click on the already-selected row raises no
    // SelectionChanged, so the tap handler uses this to tell "selected a new row" from "clicked the selected row".
    private bool _selectionChanged;

    public EventGridControl()
    {
        InitializeComponent();

        DataGrid.LoadingRow += OnDataGridLoadingRow;
        DataGrid.UnloadingRow += OnDataGridUnloadingRow;
        DataGrid.SelectionChanged += OnDataGridSelectionChanged;

        _tappedHandler = OnDataGridTapped;

        // handledEventsToo so the tap still reaches us after the DataGrid has handled the pointer for selection.
        DataGrid.AddHandler(UIElement.TappedEvent, _tappedHandler, handledEventsToo: true);
    }

    /// <summary>
    /// Releases the grid's row/event references and detaches the DataGrid handlers, so a closed query tab's events
    /// aren't held through this control
    /// </summary>
    public void Dispose()
    {
        DataGrid.LoadingRow -= OnDataGridLoadingRow;
        DataGrid.UnloadingRow -= OnDataGridUnloadingRow;
        DataGrid.SelectionChanged -= OnDataGridSelectionChanged;
        DataGrid.RemoveHandler(UIElement.TappedEvent, _tappedHandler);

        DataGrid.ItemsSource = null;

        _rows.Clear();
        _visibleRows.Clear();
        _expanded.Clear();
        _parentOf.Clear();
    }

    private void OnDataGridSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectionChanged = true;

        var selected = (DataGrid.SelectedItem as EventGridRow)?.Event;

        if (!ReferenceEquals(SelectedItem, selected))
        {
            SelectedItem = selected;
        }
    }

    // Click-to-deselect: clicking the already-selected row clears the selection. SelectionChanged fires on pointer
    // press (before this tap on release), so if it did NOT fire this click, the tapped row was already selected.
    private void OnDataGridTapped(object sender, TappedRoutedEventArgs e)
    {
        var changedThisClick = _selectionChanged;

        _selectionChanged = false;

        if (changedThisClick)
        {
            return;
        }

        if (RowFromSource(e.OriginalSource) is { } row && ReferenceEquals(DataGrid.SelectedItem, row))
        {
            DataGrid.SelectedItem = null;
        }
    }

    private static EventGridRow? RowFromSource(object? source)
    {
        var node = source as DependencyObject;

        while (node is not null and not DataGridRow)
        {
            // A tap on the expander chevron or a page-address link isn't a row deselect — those have their own action.
            if (node is ButtonBase)
            {
                return null;
            }

            node = VisualTreeHelper.GetParent(node);
        }

        return (node as DataGridRow)?.DataContext as EventGridRow;
    }

    private void OnDataGridLoadingRow(object? sender, DataGridRowEventArgs e)
    {
        if (e.Row.DataContext is EventGridRow row)
        {
            _visibleRows[e.Row] = row.Event;
            ApplyHighlight(e.Row, row.Event);
        }
    }

    private void OnExpanderClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not EventGridRow { HasChildren: true } row)
        {
            return;
        }

        var index = _rows.IndexOf(row);

        if (index < 0)
        {
            return;
        }

        if (_expanded.Remove(row.Event))
        {
            while (index + 1 < _rows.Count && _rows[index + 1].Depth > row.Depth)
            {
                _rows.RemoveAt(index + 1);
            }
        }
        else
        {
            _expanded.Add(row.Event);

            var children = row.Event is IEventGroup group ? group.Events : [];

            var insertAt = index + 1;

            foreach (var child in children)
            {
                _rows.Insert(insertAt++, new EventGridRow(child, row.Depth + 1, hasChildren: false, isExpanded: false));
            }
        }

        row.IsExpanded = _expanded.Contains(row.Event);
    }

    private void OnDataGridUnloadingRow(object? sender, DataGridRowEventArgs e)
    {
        _visibleRows.Remove(e.Row);
    }

    private void RefreshRowHighlights()
    {
        foreach (var (row, ev) in _visibleRows)
        {
            ApplyHighlight(row, ev);
        }
    }

    private void ApplyHighlight(DataGridRow row, EngineEvent ev)
    {
        var from = SequenceFrom;
        var to = SequenceTo;

        var inScope = (from == 0 && to == 0)
                   || (ev.SequenceId >= from && ev.SequenceId <= to);

        row.Background = inScope ? InScopeBrush : null;
    }

    /// <summary>
    /// Selects and scrolls the grid to the given event, clearing the search filter if it hides it
    /// </summary>
    public void NavigateToEvent(EngineEvent ev)
    {
        if (SearchBox is { Text.Length: > 0 } box && FindRow(ev) is null)
        {
            box.Text = string.Empty;
        }

        // A child event's group must be expanded before its row exists
        SelectRow(ev);

        var row = FindRow(ev);

        // Defer the scroll so it runs after any tab switch / filter change has laid the grid out.
        DispatcherQueue.TryEnqueue(() => { if (row is not null) { DataGrid.ScrollIntoView(row, null); } });
    }

    private void HyperlinkButton_Click(object sender, RoutedEventArgs e)
    {
        if (((HyperlinkButton)sender).Tag is PageAddress pageAddress)
        {
            PageClicked?.Invoke(this, new PageAddressEventArgs(pageAddress.FileId, pageAddress.PageId));
        }
    }

    private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        ApplyFilter();
    }

    /// <summary>
    /// Sets the grid's source to the events matching the search box (all fields), in the current sort order.
    /// </summary>
    private void ApplyFilter()
    {
        var events = Events.Where(e => e.IsVisible);

        IEnumerable<EngineEvent> result = events;

        var query = SearchBox?.Text?.Trim();

        if (!string.IsNullOrEmpty(query))
        {
            result = result.Where(ev => Matches(ev, query));
        }

        var filtered = ApplySort(result.ToList());

        _rows = new ObservableCollection<EventGridRow>(BuildRows(filtered));

        DataGrid.ItemsSource = _rows;

        UpdateStatusBar(filtered);
    }

    // Flattens the top-level events into grid rows, following each expanded read group with its child events indented.
    private List<EventGridRow> BuildRows(List<EngineEvent> topLevel)
    {
        var rows = new List<EventGridRow>(topLevel.Count);

        foreach (var engineEvent in topLevel)
        {
            var children = engineEvent is IEventGroup group ? group.Events : [];

            var hasChildren = children.Count > 0;

            var expanded = hasChildren && _expanded.Contains(engineEvent);

            rows.Add(new EventGridRow(engineEvent, depth: 0, hasChildren, expanded));

            if (!expanded)
            {
                continue;
            }

            foreach (var child in children)
            {
                rows.Add(new EventGridRow(child, depth: 1, hasChildren: false, isExpanded: false));
            }
        }

        return rows;
    }

    /// <summary>Shows a per-event-type count of the currently filtered events, e.g. "wait_info: 100   latch_acquired: 20".</summary>
    private void UpdateStatusBar(IReadOnlyCollection<EngineEvent> filtered)
    {
        var counts = filtered
            .GroupBy(ev => ev.Name)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => $"{g.Key}: {g.Count()}");

        StatusBarText.Text = filtered.Count == 0
            ? "No events"
            : string.Join("   ", counts);
    }

    private static bool Matches(EngineEvent ev, string query) =>
        BuildSearchText(ev).Contains(query, StringComparison.OrdinalIgnoreCase);

    // The same fields shown as columns, flattened to one string so a query matches any of them.
    private static string BuildSearchText(EngineEvent ev) => string.Join(" ",
        ev.Name,
        ev.Description,
        ev.TimeUs,
        ev.DurationUs,
        PageOf(ev),
        ev.ObjectName,
        ev.SequenceId,
        ev.PlanNodeIdentifier);

    // The representative page of an event: its single page, or the first page of a multi-page read group.
    private static PageAddress? PageOf(EngineEvent ev) => ev switch
    {
        PageEngineEvent { PageAddress: { } page } => page,
        ReadEventGroup { PageAddress: { } page } => page,
        _ => null,
    };

    private void OnSorting(object? sender, DataGridColumnEventArgs e)
    {
        if (e.Column.Tag is not string tag || tag.Length == 0)
        {
            return;
        }

        // First click (or a different column) sorts ascending; clicking the active column flips it.
        var ascending = e.Column.SortDirection != DataGridSortDirection.Ascending;

        _sortTag = tag;
        _sortAscending = ascending;

        // Show the sort glyph on the active column and clear it from the rest.
        foreach (var column in DataGrid.Columns)
        {
            column.SortDirection = column == e.Column
                ? (ascending ? DataGridSortDirection.Ascending : DataGridSortDirection.Descending)
                : null;
        }

        ApplyFilter();
    }

    private List<EngineEvent> ApplySort(IEnumerable<EngineEvent> events)
    {
        if (string.IsNullOrEmpty(_sortTag))
        {
            return events.ToList();
        }

        IOrderedEnumerable<EngineEvent> ordered = _sortTag switch
        {
            "Event" => Order(events, e => e.Name),
            "Type" => Order(events, e => e.Description),
            "TimeUs" => Order(events, e => e.TimeUs),
            "DurationUs" => Order(events, e => e.DurationUs),
            "PageAddress" => Order(events, PageSortKey),
            "Object" => Order(events, e => e.ObjectName),
            "SequenceId" => Order(events, e => e.SequenceId),
            "NodeId" => Order(events, e => e.PlanNodeIdentifier?.NodeId),
            _ => Order(events, e => e.SequenceId),
        };

        return ordered.ToList();
    }

    private IOrderedEnumerable<EngineEvent> Order<TKey>(IEnumerable<EngineEvent> events, Func<EngineEvent, TKey> key)
        => _sortAscending ? events.OrderBy(key) : events.OrderByDescending(key);

    // Sort pages numerically by (file, page) rather than by their textual form.
    private static long PageSortKey(EngineEvent ev) =>
        PageOf(ev) is { } page ? ((long)page.FileId << 32) | (uint)page.PageId : long.MinValue;
}

/// <summary>Formatting helpers called directly from the grid's x:Bind cell templates.</summary>
public static class EventGridFormat
{
    /// <summary>Converts a <see cref="EngineEvent.TimeUs"/> value to milliseconds, e.g. "1234.500".</summary>
    public static string TimeMs(long timeUs) => (timeUs / 1000.0).ToString("0.000");
}
