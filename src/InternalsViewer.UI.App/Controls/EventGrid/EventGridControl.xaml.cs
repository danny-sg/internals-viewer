using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.WinUI.UI.Controls;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.UI.App.Controls.Allocation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Controls.QueryReplay;

public sealed partial class EventGridControl : UserControl
{
    private static readonly SolidColorBrush InScopeBrush =
        new(Windows.UI.Color.FromArgb(20, 121, 251, 155));

    public event EventHandler<PageAddressEventArgs>? PageClicked;

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
        ((EventGridControl)d).ApplyFilter();
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

    private string? _sortTag;
    private bool _sortAscending = true;

    public EventGridControl()
    {
        InitializeComponent();

        DataGrid.LoadingRow   += OnDataGridLoadingRow;
        DataGrid.UnloadingRow += OnDataGridUnloadingRow;
    }

    private void OnDataGridLoadingRow(object? sender, DataGridRowEventArgs e)
    {
        if (e.Row.DataContext is EngineEvent ev)
        {
            _visibleRows[e.Row] = ev;
            ApplyHighlight(e.Row, ev);
        }
    }

    private void OnDataGridUnloadingRow(object? sender, DataGridRowEventArgs e)
    {
        _visibleRows.Remove(e.Row);
    }

    private void RefreshRowHighlights()
    {
        foreach (var (row, ev) in _visibleRows)
            ApplyHighlight(row, ev);

        ScrollToSequenceTo();
    }

    private void ApplyHighlight(DataGridRow row, EngineEvent ev)
    {
        var from = SequenceFrom;
        var to   = SequenceTo;

        var inScope = (from == 0 && to == 0)
                   || (ev.SequenceId >= from && ev.SequenceId <= to);

        row.Background = inScope ? InScopeBrush : null;
    }

    private void ScrollToSequenceTo()
    {
        if (SequenceTo == 0 || Events is not { Count: > 0 })
        {
            return;
        }

        var target = Events.LastOrDefault(e => e.SequenceId <= SequenceTo);

        if (target != null)
        {
            DataGrid.ScrollIntoView(target, null);
        }
    }

    /// <summary>Selects and scrolls the grid to the given event, clearing the search filter if it hides it.</summary>
    public void NavigateToEvent(EngineEvent ev)
    {
        if (ev is null)
        {
            return;
        }

        if (SearchBox is { Text.Length: > 0 } box &&
            DataGrid.ItemsSource is IEnumerable<EngineEvent> source && !source.Contains(ev))
        {
            box.Text = string.Empty;   // clears the filter (triggers ApplyFilter via OnSearchTextChanged)
        }

        DataGrid.SelectedItem = ev;

        // Defer the scroll so it runs after any tab switch / filter change has laid the grid out.
        DispatcherQueue.TryEnqueue(() => DataGrid.ScrollIntoView(ev, null));
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

    /// <summary>Sets the grid's source to the events matching the search box (all fields), in the current sort order.</summary>
    private void ApplyFilter()
    {
        var events = Events;

        if (events is null)
        {
            DataGrid.ItemsSource = null;
            return;
        }

        IEnumerable<EngineEvent> result = events;

        var query = SearchBox?.Text?.Trim();

        if (!string.IsNullOrEmpty(query))
        {
            result = result.Where(ev => Matches(ev, query));
        }

        DataGrid.ItemsSource = ApplySort(result);

        RefreshRowHighlights();
    }

    private static bool Matches(EngineEvent ev, string query) =>
        BuildSearchText(ev).Contains(query, StringComparison.OrdinalIgnoreCase);

    // The same fields shown as columns, flattened to one string so a query matches any of them.
    private static string BuildSearchText(EngineEvent ev) => string.Join(" ",
        ev.Name,
        ev.Description,
        ev.TimeUs,
        ev.DurationUs,
        ev.PageAddress,
        ev.ObjectName,
        ev.SequenceId,
        ev.PlanNodeIdentifier);

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
            "Event"       => Order(events, e => e.Name),
            "Type"        => Order(events, e => e.Description),
            "TimeUs"      => Order(events, e => e.TimeUs),
            "DurationUs"  => Order(events, e => e.DurationUs),
            "PageAddress" => Order(events, PageSortKey),
            "Object"      => Order(events, e => e.ObjectName),
            "SequenceId"  => Order(events, e => e.SequenceId),
            "NodeId"      => Order(events, e => e.PlanNodeIdentifier?.NodeId),
            _             => Order(events, e => e.SequenceId),
        };

        return ordered.ToList();
    }

    private IOrderedEnumerable<EngineEvent> Order<TKey>(IEnumerable<EngineEvent> events, Func<EngineEvent, TKey> key)
        => _sortAscending ? events.OrderBy(key) : events.OrderByDescending(key);

    // Sort pages numerically by (file, page) rather than by their textual form.
    private static long PageSortKey(EngineEvent ev) =>
        ev.PageAddress is { } page ? ((long)page.FileId << 32) | (uint)page.PageId : long.MinValue;
}
