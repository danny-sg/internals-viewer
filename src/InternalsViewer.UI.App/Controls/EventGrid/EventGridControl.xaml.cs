using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Interfaces.Events;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using WinUI.TableView;

namespace InternalsViewer.UI.App.Controls.EventGrid;

public sealed partial class EventGridControl : UserControl, IDisposable
{
    private static readonly SolidColorBrush InScopeBrush =
        new(Windows.UI.Color.FromArgb(20, 121, 251, 155));

    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(nameof(SelectedItem), typeof(EngineEvent), typeof(EventGridControl),
            new PropertyMetadata(null, OnSelectedItemChanged));

    public EngineEvent? SelectedItem
    {
        get => (EngineEvent?)GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public static readonly DependencyProperty EventsProperty =
        DependencyProperty.Register(nameof(Events), typeof(List<EngineEvent>), typeof(EventGridControl),
            new PropertyMetadata(null, OnEventsChanged));

    public List<EngineEvent> Events
    {
        get => (List<EngineEvent>)GetValue(EventsProperty);
        set => SetValue(EventsProperty, value);
    }

    public static readonly DependencyProperty SequenceFromProperty =
        DependencyProperty.Register(nameof(SequenceFrom), typeof(long), typeof(EventGridControl),
            new PropertyMetadata(0L, OnSequenceRangeChanged));

    public long SequenceFrom
    {
        get => (long)GetValue(SequenceFromProperty);
        set => SetValue(SequenceFromProperty, value);
    }

    public static readonly DependencyProperty SequenceToProperty =
        DependencyProperty.Register(nameof(SequenceTo), typeof(long), typeof(EventGridControl),
            new PropertyMetadata(0L, OnSequenceRangeChanged));

    public long SequenceTo
    {
        get => (long)GetValue(SequenceToProperty);
        set => SetValue(SequenceToProperty, value);
    }

    private readonly TappedEventHandler _tappedHandler;

    private readonly Dictionary<TableViewRow, EngineEvent> _visibleRows = new();

    private readonly HashSet<EngineEvent> _expanded = new(ReferenceEqualityComparer.Instance);

    private readonly Dictionary<EngineEvent, EngineEvent> _parentOf = new(ReferenceEqualityComparer.Instance);

    private ObservableCollection<EventGridRow> _rows = [];

    private string? _sortTag;
    private bool _sortAscending = true;

    private bool _selectionChanged;

    public EventGridControl()
    {
        InitializeComponent();

        EventTable.ContainerContentChanging += OnContainerContentChanging;
        EventTable.SelectionChanged += OnTableSelectionChanged;

        _tappedHandler = OnTableTapped;

        EventTable.AddHandler(UIElement.TappedEvent, _tappedHandler, handledEventsToo: true);
    }

    public event EventHandler<PageAddressEventArgs>? PageClicked;

    public void Dispose()
    {
        EventTable.ContainerContentChanging -= OnContainerContentChanging;
        EventTable.SelectionChanged -= OnTableSelectionChanged;
        EventTable.RemoveHandler(UIElement.TappedEvent, _tappedHandler);

        EventTable.ItemsSource = null;

        _rows.Clear();
        _visibleRows.Clear();
        _expanded.Clear();
        _parentOf.Clear();
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
        DispatcherQueue.TryEnqueue(() => { if (row is not null) { EventTable.ScrollIntoView(row); } });
    }

    private void SelectRow(EngineEvent? engineEvent)
    {
        if (engineEvent is null)
        {
            EventTable.SelectedItem = null;

            return;
        }

        if (_parentOf.TryGetValue(engineEvent, out var parent) && _expanded.Add(parent))
        {
            ApplyFilter();
        }

        EventTable.SelectedItem = FindRow(engineEvent);
    }

    private EventGridRow? FindRow(EngineEvent engineEvent) =>
        (EventTable.ItemsSource as IEnumerable<EventGridRow>)?.FirstOrDefault(r => ReferenceEquals(r.Event, engineEvent));

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

    private void OnTableSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectionChanged = true;

        var selected = (EventTable.SelectedItem as EventGridRow)?.Event;

        if (!ReferenceEquals(SelectedItem, selected))
        {
            SelectedItem = selected;
        }
    }

    private void OnTableTapped(object sender, TappedRoutedEventArgs e)
    {
        var changedThisClick = _selectionChanged;

        _selectionChanged = false;

        if (changedThisClick)
        {
            return;
        }

        if (RowFromSource(e.OriginalSource) is { } row && ReferenceEquals(EventTable.SelectedItem, row))
        {
            EventTable.SelectedItem = null;
        }
    }

    private static EventGridRow? RowFromSource(object? source)
    {
        var node = source as DependencyObject;

        while (node is not null and not TableViewRow)
        {
            // A tap on the expander chevron or a page-address link isn't a row deselect — those have their own action.
            if (node is ButtonBase)
            {
                return null;
            }

            node = VisualTreeHelper.GetParent(node);
        }

        return (node as TableViewRow)?.Content as EventGridRow;
    }

    private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.ItemContainer is not TableViewRow container)
        {
            return;
        }

        if (args.InRecycleQueue)
        {
            _visibleRows.Remove(container);

            return;
        }

        if (args.Item is EventGridRow row)
        {
            _visibleRows[container] = row.Event;

            ApplyHighlight(container, row.Event);
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
                _rows.Insert(insertAt++, Row(child, row.Depth + 1, hasChildren: false, isExpanded: false));
            }
        }

        row.IsExpanded = _expanded.Contains(row.Event);
    }

    private void RefreshRowHighlights()
    {
        foreach (var (row, ev) in _visibleRows)
        {
            ApplyHighlight(row, ev);
        }
    }

    private void ApplyHighlight(TableViewRow row, EngineEvent ev)
    {
        var from = SequenceFrom;
        var to = SequenceTo;

        var inScope = (from == 0 && to == 0)
                   || (ev.SequenceId >= from && ev.SequenceId <= to);

        row.Background = inScope ? InScopeBrush : null;
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
    /// Sets the grid's source to the events matching the search box (all fields), in the current sort order
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

        EventTable.ItemsSource = _rows;

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

            rows.Add(Row(engineEvent, depth: 0, hasChildren, expanded));

            if (!expanded)
            {
                continue;
            }

            foreach (var child in children)
            {
                rows.Add(Row(child, depth: 1, hasChildren: false, isExpanded: false));
            }
        }

        return rows;
    }

    /// <summary>Shows a per-event-type count of the currently filtered events, e.g. "wait_info: 100   latch_acquired: 20"</summary>
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

    public Func<PageAddress, string?>? ResolveStructure { get; set; }

    private EventGridRow Row(EngineEvent engineEvent, int depth, bool hasChildren, bool isExpanded)
    {
        var row = new EventGridRow(engineEvent, depth, hasChildren, isExpanded);

        if (ResolveStructure is { } resolve && PageOf(engineEvent) is { } page)
        {
            row.Structure = resolve(page) ?? string.Empty;
        }

        return row;
    }

    private static string BuildSearchText(EngineEvent ev) => string.Join(" ",
       ev.Name,
       ev.Description,
       ev.TimeUs,
       ev.DurationUs,
       PageOf(ev),
       ev.ObjectName,
       ev.SequenceId,
       ev.PlanNodeIdentifier);

    private static PageAddress? PageOf(EngineEvent ev) => ev switch
    {
        PageEngineEvent { PageAddress: { } page } => page,
        ReadEventGroup { PageAddress: { } page } => page,
        _ => null,
    };

    private void OnSorting(object? sender, TableViewSortingEventArgs e)
    {
        if (e.Column.Tag is not string tag || tag.Length == 0)
        {
            return;
        }

        e.Handled = true;

        // First click (or a different column) sorts ascending; clicking the active column flips it.
        var ascending = e.Column.SortDirection != SortDirection.Ascending;

        _sortTag = tag;
        _sortAscending = ascending;

        // Show the sort glyph on the active column and clear it from the rest.
        foreach (var column in EventTable.Columns)
        {
            column.SortDirection = column == e.Column
                ? (ascending ? SortDirection.Ascending : SortDirection.Descending)
                : null;
        }

        ApplyFilter();
    }

    private List<EngineEvent> ApplySort(IEnumerable<EngineEvent> events)
    {
        if (string.IsNullOrEmpty(_sortTag))
        {
            return [.. events];
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

        return [.. ordered];
    }

    private IOrderedEnumerable<EngineEvent> Order<TKey>(IEnumerable<EngineEvent> events, Func<EngineEvent, TKey> key)
        => _sortAscending ? events.OrderBy(key) : events.OrderByDescending(key);

    // Sort pages numerically by (file, page) rather than by their textual form.
    private static long PageSortKey(EngineEvent ev) =>
        PageOf(ev) is { } page ? ((long)page.FileId << 32) | (uint)page.PageId : long.MinValue;

    private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (EventGridControl)d;

        if (!ReferenceEquals((control.EventTable.SelectedItem as EventGridRow)?.Event, e.NewValue))
        {
            control.SelectRow(e.NewValue as EngineEvent);
        }
    }

    private static void OnEventsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (EventGridControl)d;

        control.RebuildParentMap();
        control.ApplyFilter();
    }

    private static void OnSequenceRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((EventGridControl)d).RefreshRowHighlights();
    }
}

public static class EventGridFormat
{
    public static string TimeMs(long timeUs) => (timeUs / 1000.0).ToString("0.000");
}
