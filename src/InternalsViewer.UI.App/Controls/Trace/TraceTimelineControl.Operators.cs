using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Helpers;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Helpers;
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

    private const float ReadLaneHeight = 14f;

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

    public static readonly DependencyProperty ShowIteratorCallsProperty =
        DependencyProperty.Register(nameof(ShowIteratorCalls),
                                    typeof(bool),
                                    typeof(TraceTimelineControl),
                                    new PropertyMetadata(true, OnRowReturnsChanged));

    public bool ShowIteratorCalls
    {
        get => (bool)GetValue(ShowIteratorCallsProperty);
        set => SetValue(ShowIteratorCallsProperty, value);
    }

    public static readonly DependencyProperty ShowReadsProperty =
        DependencyProperty.Register(nameof(ShowReads),
                                    typeof(bool),
                                    typeof(TraceTimelineControl),
                                    new PropertyMetadata(true, OnPaneVisibilityChanged));

    public bool ShowReads
    {
        get => (bool)GetValue(ShowReadsProperty);
        set => SetValue(ShowReadsProperty, value);
    }

    public static readonly DependencyProperty ReadsProperty =
        DependencyProperty.Register(nameof(Reads),
                                    typeof(TraceReads),
                                    typeof(TraceTimelineControl),
                                    new PropertyMetadata(null, OnReadsChanged));

    public TraceReads? Reads
    {
        get => (TraceReads?)GetValue(ReadsProperty);
        set => SetValue(ReadsProperty, value);
    }

    public static readonly DependencyProperty OutputColumnNamesProperty =
        DependencyProperty.Register(nameof(OutputColumnNames),
                                    typeof(IReadOnlyList<string>),
                                    typeof(TraceTimelineControl),
                                    new PropertyMetadata(null, OnOutputColumnNamesChanged));

    public IReadOnlyList<string>? OutputColumnNames
    {
        get => (IReadOnlyList<string>?)GetValue(OutputColumnNamesProperty);
        set => SetValue(OutputColumnNamesProperty, value);
    }

    private readonly Dictionary<int, SelectedLane> _selectedLanes = [];

    private float? _operatorsHeight;

    private bool _isResizingOperators;

    private double _resizePressY;

    private float _resizeStartHeight;

    public event Action<PlanNodeIdentifier>? PlanNodeSelected;

    public event Action<PlanNodeIdentifier?>? ExecutionPlanRequested;

    private bool ShowsOperators => ShowOperators && _operators.HasRows;

    private float OperatorsTop => RulerTop + RulerHeight + PlayheadStripHeight;

    private bool ShowsFlameChart => ShowFlameChart || !ShowsOperators;

    private bool ShowsReads => ShowReads && _reads.HasReads;

    private float ReadsTop => OperatorsTop + OperatorsHeight;

    private float ReadsHeight => ShowsReads ? ReadLaneHeight : 0;

    private bool ShowsSplitter => ShowsOperators && ShowsFlameChart;

    private float PanesArea => OperatorsHeight + ReadsHeight + (ShowsSplitter ? SplitterHeight : 0);

    private float ChartHeight => (float)_overlay.ActualHeight - OperatorsTop - BandHeight - SplitterHeight - ReadsHeight;

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
                return ChartHeight + SplitterHeight;
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

        if (ShowsReads)
        {
            _reads.Draw(canvas, width, scale);
        }

        if (ShowsSplitter)
        {
            DrawSplitter(canvas, ReadsTop + ReadsHeight, width);
        }
    }

    private void DrawPaneOverlays(SKCanvas canvas, int width)
    {
        var scale = PixelsPerUnit(width);

        if (ShowsOperators)
        {
            _operators.DrawOverlay(canvas, width, scale);
        }

        if (ShowsReads)
        {
            _reads.DrawOverlay(canvas, width, scale, ShowsOperators ? _operators.GetTrackBottom : null);
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

            var index = SortedSearch.FirstAfter(selected.Ends(_axis), _viewStart);

            while (index < regions.Length && regions[index].Of(_axis).Start <= _viewEnd)
            {
                var span = regions[index].Of(_axis);

                var left = Math.Max((float)((span.Start - _viewStart) * scale), -1f);

                var right = Math.Min(Math.Max((float)((span.End - _viewStart) * scale), left + NarrowSpan), width + 1f);

                _pathBuilder.AddRect(new SKRect(left, rowsTop + regions[index].Depth * _rowHeight, right, rowsBottom));

                index = SortedSearch.FirstAfter(selected.Ends(_axis), _viewStart + (MathF.Floor(right) + 1f) / scale, index + 1);
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
            if (span.Node.Frame is not { Instance: not 0 } frame || !instances.Contains(frame.Instance))
            {
                continue;
            }

            if (!found.TryGetValue(span.ThreadIndex, out var regions))
            {
                regions = [];

                found[span.ThreadIndex] = regions;
            }

            regions.Add(new SelectedRegion(span.Depth,
                                           span.Span(TraceTimelineAxis.Position),
                                           span.Span(TraceTimelineAxis.Instructions)));
        }

        foreach (var (lane, regions) in found)
        {
            var outermost = Outermost.Of(regions, r => r.Position.Start, r => r.Position.End);

            _selectedLanes[lane] = new SelectedLane([.. outermost],
                                                    [.. outermost.Select(r => r.Position.End)],
                                                    [.. outermost.Select(r => r.Instructions.End)]);
        }
    }

    private bool IsOnSplitter(Point position)
    {
        if (!ShowsSplitter)
        {
            return false;
        }

        var top = ReadsTop + ReadsHeight;

        return position.Y >= top - SplitterHitMargin && position.Y < top + SplitterHeight + SplitterHitMargin;
    }

    private MenuFlyout CreateOperatorsMenu(PlanNodeIdentifier? identifier)
    {
        var flyout = new MenuFlyout();

        flyout.Items.Add(MenuItems.Create("Execution Plan", () => ExecutionPlanRequested?.Invoke(identifier)));

        flyout.Items.Add(new MenuFlyoutSeparator());

        var iteratorCalls = MenuItems.Toggle("Iterator Calls", ShowIteratorCalls, isChecked => ShowIteratorCalls = isChecked);

        var reads = MenuItems.Toggle("Reads", ShowReads, isChecked => ShowReads = isChecked);

        flyout.Items.Add(MenuItems.SubMenu("View", iteratorCalls, reads));

        return flyout;
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

    private static void OnPaneVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (TraceTimelineControl)d;

        control.HideToolTip();

        control._operators.Hover = TraceOperatorsHover.None;

        control._reads.Hover = null;

        control.Relayout();
    }

    private static void OnReadsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (TraceTimelineControl)d;

        control._reads.Reads = e.NewValue as TraceReads;

        control._reads.Hover = null;

        control.Relayout();
    }

    private static void OnRowReturnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (TraceTimelineControl)d;

        control._operators.RowReturnMarks = control.RowReturnMarks;

        control._operators.ShowIteratorCalls = control.ShowIteratorCalls;

        control.Relayout();
    }

    private static void OnOutputColumnNamesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (TraceTimelineControl)d;

        control._operators.OutputColumnNames = e.NewValue as IReadOnlyList<string>;

        control.Redraw();
    }

    private readonly record struct SelectedRegion(int Depth, TraceSpan Position, TraceSpan Instructions)
    {
        public TraceSpan Of(TraceTimelineAxis axis) => axis == TraceTimelineAxis.Position ? Position : Instructions;
    }

    private sealed record SelectedLane(SelectedRegion[] Regions, double[] PositionEnds, double[] InstructionEnds)
    {
        public ReadOnlySpan<double> Ends(TraceTimelineAxis axis)
            => axis == TraceTimelineAxis.Position ? PositionEnds : InstructionEnds;
    }
}
