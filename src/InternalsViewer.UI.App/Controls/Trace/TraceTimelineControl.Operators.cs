using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.FullTrace.Timeline.Tracks;
using InternalsViewer.Query.Helpers;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SkiaSharp;
using Windows.Foundation;

namespace InternalsViewer.UI.App.Controls.Trace;

public sealed partial class TraceTimelineControl
{
    private const float SplitterHeight = 6f;

    private const float SplitterGripWidth = 32f;

    private const float SplitterHitMargin = 2f;

    private const float OperatorsShare = 1f / 3;

    private const float MinimumOperatorsHeight = 20f;

    private const float MinimumLanesHeight = 60f;

    private const float MemoryShare = 0.25f;

    private const float MinimumMemoryHeight = 24f;

    private const double MemoryProgressSize = 28;

    public static readonly DependencyProperty MemoryBlocksProperty =
        DependencyProperty.Register(nameof(MemoryBlocks),
                                    typeof(IReadOnlyList<TraceMemoryBlock>),
                                    typeof(TraceTimelineControl),
                                    new PropertyMetadata(null, OnMemoryBlocksChanged));

    public IReadOnlyList<TraceMemoryBlock>? MemoryBlocks
    {
        get => (IReadOnlyList<TraceMemoryBlock>?)GetValue(MemoryBlocksProperty);
        set => SetValue(MemoryBlocksProperty, value);
    }

    public static readonly DependencyProperty ShowOperatorsProperty =
        DependencyProperty.Register(nameof(ShowOperators),
                                    typeof(bool),
                                    typeof(TraceTimelineControl),
                                    new PropertyMetadata(true, OnPaneVisibilityChanged));

    public bool ShowOperators
    {
        get => (bool)GetValue(ShowOperatorsProperty);
        set => SetValue(ShowOperatorsProperty, value);
    }

    public static readonly DependencyProperty ShowFlameChartProperty =
        DependencyProperty.Register(nameof(ShowFlameChart),
                                    typeof(bool),
                                    typeof(TraceTimelineControl),
                                    new PropertyMetadata(true, OnPaneVisibilityChanged));

    public bool ShowFlameChart
    {
        get => (bool)GetValue(ShowFlameChartProperty);
        set => SetValue(ShowFlameChartProperty, value);
    }

    public static readonly DependencyProperty RowReturnMarksProperty =
        DependencyProperty.Register(nameof(RowReturnMarks),
                                    typeof(TraceRowReturnMarks),
                                    typeof(TraceTimelineControl),
                                    new PropertyMetadata(null, OnRowReturnsChanged));

    public TraceRowReturnMarks? RowReturnMarks
    {
        get => (TraceRowReturnMarks?)GetValue(RowReturnMarksProperty);
        set => SetValue(RowReturnMarksProperty, value);
    }

    public static readonly DependencyProperty TickTracksProperty =
        DependencyProperty.Register(nameof(TickTracks),
                                    typeof(IReadOnlyList<TraceTickTrack>),
                                    typeof(TraceTimelineControl),
                                    new PropertyMetadata(null, OnTickTracksChanged));

    public IReadOnlyList<TraceTickTrack>? TickTracks
    {
        get => (IReadOnlyList<TraceTickTrack>?)GetValue(TickTracksProperty);
        set => SetValue(TickTracksProperty, value);
    }

    public static readonly DependencyProperty HiddenTickTracksProperty =
        DependencyProperty.Register(nameof(HiddenTickTracks),
                                    typeof(IReadOnlySet<string>),
                                    typeof(TraceTimelineControl),
                                    new PropertyMetadata(null, OnTickTracksChanged));

    public IReadOnlySet<string>? HiddenTickTracks
    {
        get => (IReadOnlySet<string>?)GetValue(HiddenTickTracksProperty);
        set => SetValue(HiddenTickTracksProperty, value);
    }

    public static readonly DependencyProperty ColourProviderProperty =
        DependencyProperty.Register(nameof(ColourProvider),
                                    typeof(EventColourProvider),
                                    typeof(TraceTimelineControl),
                                    new PropertyMetadata(null, OnColourProviderChanged));

    public EventColourProvider? ColourProvider
    {
        get => (EventColourProvider?)GetValue(ColourProviderProperty);
        set => SetValue(ColourProviderProperty, value);
    }

    public static readonly DependencyProperty StatementNameProperty =
        DependencyProperty.Register(nameof(StatementName),
                                    typeof(string),
                                    typeof(TraceTimelineControl),
                                    new PropertyMetadata(null, OnStatementNameChanged));

    public string? StatementName
    {
        get => (string?)GetValue(StatementNameProperty);
        set => SetValue(StatementNameProperty, value);
    }

    private readonly Dictionary<int, SelectedLane> _selectedLanes = [];

    private float? _operatorsHeight;

    private bool _isResizingOperators;

    private double _resizePressY;

    private float _resizeStartHeight;

    private float? _memoryHeight;

    private bool _isResizingMemory;

    private double _memoryResizePressY;

    private float _memoryResizeStartHeight;

    public event Action<PlanNodeIdentifier>? PlanNodeSelected;

    public event Action<PlanNodeIdentifier?>? ExecutionPlanRequested;

    public event Action<string, bool>? TickTrackVisibilityChanged;

    public event Action<CallStackNode?>? TickTrackAddRequested;

    public event Action? TickTracksClearRequested;

    public event Action<int, TracePosition?>? MemoryWriteSelected;

    public event Action<int, ulong, TracePosition>? MemoryBlockSelected;

    private bool ShowsOperators => ShowOperators && _operators.HasRows;

    private float OperatorsTop => RulerTop + RulerHeight + PlayheadStripHeight;

    private bool ShowsFlameChart => ShowFlameChart || !ShowsOperators;

    private bool ShowsSplitter => ShowsOperators && (ShowsFlameChart || ShowsMemory);

    private bool ShowsMemory => _memory.HasBlocks;

    private float PanesArea => OperatorsHeight + (ShowsSplitter ? SplitterHeight : 0) + MemoryArea;

    private float ChartHeight => (float)_overlay.ActualHeight - OperatorsTop - BandHeight - SplitterHeight - MemoryArea;

    private float MemoryTop => OperatorsTop + OperatorsHeight + (ShowsSplitter ? SplitterHeight : 0);

    private float MemoryArea => ShowsMemory ? MemoryHeight + SplitterHeight : 0;

    private float MemoryHeight
    {
        get
        {
            if (!ShowsMemory)
            {
                return 0;
            }

            var available = (float)_overlay.ActualHeight - OperatorsTop - BandHeight - SplitterHeight * 2;

            var room = Math.Max(MinimumMemoryHeight, available - MinimumLanesHeight - (ShowsOperators ? MinimumOperatorsHeight : 0));

            return Math.Clamp(_memoryHeight ?? available * MemoryShare, MinimumMemoryHeight, room);
        }
    }

    private float OperatorsRoom => Math.Max(MinimumOperatorsHeight, ChartHeight - MinimumLanesHeight);

    private float OperatorsHeight
    {
        get
        {
            if (!ShowsOperators)
            {
                return 0;
            }

            if (!ShowsFlameChart)
            {
                return ChartHeight + (ShowsSplitter ? 0 : SplitterHeight);
            }

            var wanted = _operatorsHeight ?? ChartHeight * OperatorsShare;

            return Math.Clamp(wanted, MinimumOperatorsHeight, OperatorsRoom);
        }
    }

    private void DrawPanes(SKCanvas canvas, int width, double scale)
    {
        if (ShowsOperators)
        {
            _operators.Draw(canvas, width, scale);
        }

        if (ShowsSplitter)
        {
            DrawSplitter(canvas, OperatorsTop + OperatorsHeight, width);
        }

        if (ShowsMemory)
        {
            DrawSplitter(canvas, MemoryTop + MemoryHeight, width);
        }
    }

    private void DrawPaneOverlays(SKCanvas canvas, int width)
    {
        var scale = PixelsPerUnit(width);

        if (ShowsOperators)
        {
            _operators.DrawOverlay(canvas, width, scale);
        }

        if (ShowsMemory)
        {
            _memory.DrawOverlay(canvas, width, scale);
        }
    }

    private void DrawSplitter(SKCanvas canvas, float top, int width)
    {
        _paints.Fill.Color = _paints.LaneHeader;

        canvas.DrawRect(0, top, width, SplitterHeight, _paints.Fill);

        var centre = top + SplitterHeight / 2;

        canvas.DrawLine(width / 2f - SplitterGripWidth / 2, centre, width / 2f + SplitterGripWidth / 2, centre, _paints.Tick);
    }

    private void DrawSelectedCalls(SKCanvas canvas, int width, int height)
    {
        if (_operators.SelectedOperator is null || ShowCallStack || _visible is not { } timeline)
        {
            return;
        }

        var scale = PixelsPerUnit(width);

        var bottom = height - BandHeight;

        foreach (var (lane, selected) in _selectedLanes)
        {
            if (lane >= timeline.Threads.Count || lane >= _laneTops.Length || !IsExpanded(timeline.Threads[lane]))
            {
                continue;
            }

            var rowsTop = ContentTop + _laneTops[lane] - (float)_scrollY + LaneHeaderHeight;

            var rowsBottom = rowsTop + DepthOf(timeline.Threads[lane]) * _rowHeight;

            if (rowsBottom < ContentTop || rowsTop > bottom)
            {
                continue;
            }

            var regions = selected.Regions;

            var index = TraceScale.GetFirstAfter(selected.Ends, _axis, _viewStart);

            while (index < regions.Length && TraceScale.GetValue(regions[index].Span.From, _axis) <= _viewEnd)
            {
                var span = regions[index].Span;

                var left = Math.Max((float)((TraceScale.GetValue(span.From, _axis) - _viewStart) * scale), -1f);

                var end = TraceScale.GetValue(span.To, _axis);

                var right = Math.Min(Math.Max((float)((end - _viewStart) * scale), left + NarrowSpan), width + 1f);

                _pathBuilder.AddRect(new SKRect(left, rowsTop + regions[index].Depth * _rowHeight, right, rowsBottom));

                index = TraceScale.GetFirstAfter(selected.Ends, _axis, _viewStart + (MathF.Floor(right) + 1f) / scale, index + 1);
            }
        }

        using var path = _pathBuilder.Detach();

        canvas.Save();

        canvas.ClipRect(new SKRect(0, ContentTop, width, bottom));

        canvas.ClipPath(path, SKClipOperation.Difference);

        _paints.Fill.Color = _paints.Dim;

        canvas.DrawRect(0, ContentTop, width, bottom - ContentTop, _paints.Fill);

        canvas.Restore();
    }

    private void FindSelectedRegions()
    {
        _selectedLanes.Clear();

        if (_operators.SelectedOperator is not { } selected || _visible is not { } timeline)
        {
            return;
        }

        var instances = new HashSet<ulong>(selected.Instances);

        var found = new Dictionary<int, List<SelectedRegion>>();

        foreach (var span in timeline.GetSpanLocations())
        {
            if (span.Node.Frame is not { InstanceAddress: not 0 } frame || !instances.Contains(frame.InstanceAddress))
            {
                continue;
            }

            if (!found.TryGetValue(span.ThreadIndex, out var regions))
            {
                regions = [];

                found[span.ThreadIndex] = regions;
            }

            regions.Add(new SelectedRegion(span.Depth, span.Span));
        }

        foreach (var (lane, regions) in found)
        {
            var outermost = Outermost.Of(regions, r => r.Span.From.Position, r => r.Span.To.Position);

            _selectedLanes[lane] = new SelectedLane([.. outermost], [.. outermost.Select(r => r.Span.To)]);
        }
    }

    private bool IsOnSplitter(Point position)
    {
        if (!ShowsSplitter)
        {
            return false;
        }

        var top = OperatorsTop + OperatorsHeight;

        return position.Y >= top - SplitterHitMargin && position.Y < top + SplitterHeight + SplitterHitMargin;
    }

    private bool IsOnMemorySplitter(Point position)
    {
        if (!ShowsMemory)
        {
            return false;
        }

        var top = MemoryTop + MemoryHeight;

        return position.Y >= top - SplitterHitMargin && position.Y < top + SplitterHeight + SplitterHitMargin;
    }

    private MenuFlyout CreateOperatorsMenu(PlanNodeIdentifier? identifier)
    {
        var flyout = new MenuFlyout();

        flyout.Items.Add(MenuItems.Create("Execution Plan", () => ExecutionPlanRequested?.Invoke(identifier)));

        flyout.Items.Add(new MenuFlyoutSeparator());

        flyout.Items.Add(MenuItems.SubMenu("View", [.. CreateTickTrackItems()]));

        return flyout;
    }

    internal IEnumerable<MenuFlyoutItemBase> CreateTickTrackItems()
    {
        foreach (var track in TickTracks ?? [])
        {
            yield return MenuItems.Toggle(track.Name,
                                          HiddenTickTracks?.Contains(track.Name) != true,
                                          isChecked => TickTrackVisibilityChanged?.Invoke(track.Name, isChecked));
        }

        yield return new MenuFlyoutSeparator();

        yield return MenuItems.Create("Custom...", () => TickTrackAddRequested?.Invoke(null));

        var clear = MenuItems.Create("Clear All", () => TickTracksClearRequested?.Invoke());

        clear.IsEnabled = TickTracks?.Any(t => t.IsCustom) == true;

        yield return clear;
    }

    private void ZoomToOperator(TraceOperatorHit hit)
    {
        var (start, end) = _operators.GetActiveRange(hit);

        ZoomTo(start, end);
    }

    private void BeginOperatorsResize(Point position)
    {
        _isResizingOperators = true;

        _resizePressY = position.Y;

        _resizeStartHeight = OperatorsHeight;
    }

    private void ResizeOperators(Point position)
    {
        _operatorsHeight = Math.Clamp(_resizeStartHeight + (float)(position.Y - _resizePressY), MinimumOperatorsHeight, OperatorsRoom);

        Relayout();
    }

    private void BeginMemoryResize(Point position)
    {
        _isResizingMemory = true;

        _memoryResizePressY = position.Y;

        _memoryResizeStartHeight = MemoryHeight;
    }

    private void ResizeMemory(Point position)
    {
        _memoryHeight = Math.Max(MinimumMemoryHeight, _memoryResizeStartHeight + (float)(position.Y - _memoryResizePressY));

        Relayout();
    }

    private static void OnMemoryBlocksChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (TraceTimelineControl)d;

        control._memory.SetBlocks(e.NewValue as IReadOnlyList<TraceMemoryBlock>);

        control.SetMemoryWriters(null);

        control.Relayout();
    }

    private static void OnPaneVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (TraceTimelineControl)d;

        control.HideToolTip();

        control._operators.Hover = TraceOperatorsHover.None;

        control.Relayout();
    }

    private static void OnTickTracksChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (TraceTimelineControl)d;

        control._operators.SetTracks(control.TickTracks, control.HiddenTickTracks);

        control.Redraw();
    }

    private static void OnRowReturnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (TraceTimelineControl)d;

        control.Relayout();
    }

    private static void OnStatementNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (TraceTimelineControl)d;

        control._operators.StatementName = e.NewValue as string;

        control.RebuildVisible();

        control.Relayout();
    }

    private static void OnColourProviderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (TraceTimelineControl)d;

        control._operators.ColourProvider = e.NewValue as EventColourProvider;

        control.Redraw();
    }

    private readonly record struct SelectedRegion(int Depth, TraceSpan Span);

    private sealed record SelectedLane(SelectedRegion[] Regions, TracePoint[] Ends);
}
