using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.WinUI.UI.Controls;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Replay.Events;
using InternalsViewer.UI.App.Controls.Allocation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Controls.QueryReplay;

public sealed partial class EventGridControl : UserControl
{
    private static readonly SolidColorBrush InScopeBrush =
        new(Windows.UI.Color.FromArgb(60, 255, 200, 0));

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
        ((EventGridControl)d).RefreshRowHighlights();
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

    private void HyperlinkButton_Click(object sender, RoutedEventArgs e)
    {
        if (((HyperlinkButton)sender).Tag is PageAddress pageAddress)
        {
            PageClicked?.Invoke(this, new PageAddressEventArgs(pageAddress.FileId, pageAddress.PageId));
        }
    }
}
