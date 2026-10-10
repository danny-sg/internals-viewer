using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.CallStack.TimeTravel;
using InternalsViewer.Query.CallStack.TimeTravel.Timeline;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Controls.Timeline;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml;
using SkiaSharp;
using Windows.Foundation;

namespace InternalsViewer.UI.App.Controls.FlameChart;

public sealed partial class FlameChartControl
{
    private const float SplitterHeight = 6f;

    private const float SplitterGripWidth = 32f;

    private const float SplitterHitMargin = 2f;

    private const float CompactOperatorRowHeight = 14f;

    private const float CompactOperatorsShare = 0.4f;

    private const float MinimumOperatorsHeight = 20f;

    private const float OperatorsPadding = 4f;

    private const float OperatorRowGap = 2f;

    private const float MinimumLanesHeight = 60f;

    private const float OperatorBarPadding = 1f;

    private const float OperatorBarShare = 0.15f;

    private const float OperatorCornerRadius = 3f;

    private const float OperatorGradientLift = 0.04f;

    private const float OperatorIdleShade = 0.7f;

    private const float OperatorMaximumFont = 12f;

    private const float OperatorMinimumFont = 7f;

    private const float OperatorLabelGap = 0.5f;

    private const float OperatorBaselineShift = 0.35f;

    private const float MinimumOperatorLabelWidth = 26f;

    private const float OperatorSelectedStroke = 2f;

    private static readonly SKColor OperatorPaneColour = new(30, 30, 30, 220);

    private static readonly SKColor OperatorLabelColour = new(235, 235, 235);

    private static readonly SKColor OperatorSelectedColour = new(255, 255, 255, 230);

    private static readonly SKColor OperatorHoverColour = new(255, 255, 255, 150);

    private static readonly SKColor OperatorDimColour = new(0, 0, 0, 160);

    public static readonly DependencyProperty ShowOperatorsProperty =
        DependencyProperty.Register(nameof(ShowOperators),
                                    typeof(bool),
                                    typeof(FlameChartControl),
                                    new PropertyMetadata(true, OnShowOperatorsChanged));

    public bool ShowOperators
    {
        get => (bool)GetValue(ShowOperatorsProperty);
        set => SetValue(ShowOperatorsProperty, value);
    }

    public static readonly DependencyProperty ColourProviderProperty =
        DependencyProperty.Register(nameof(ColourProvider),
                                    typeof(EventColourProvider),
                                    typeof(FlameChartControl),
                                    new PropertyMetadata(null, OnColourProviderChanged));

    public EventColourProvider? ColourProvider
    {
        get => (EventColourProvider?)GetValue(ColourProviderProperty);
        set => SetValue(ColourProviderProperty, value);
    }

    private readonly Dictionary<int, SelectedLane> _selectedLanes = [];

    private readonly Dictionary<TimeTravelOperatorLifetime, string> _operatorDetails = new(ReferenceEqualityComparer.Instance);

    private readonly Dictionary<SKColor, SKShader> _operatorGradients = [];

    private readonly SKRoundRect _operatorShape = new();

    private OperatorRow[] _operatorRows = [];

    private float? _operatorsHeight;

    private bool _isResizingOperators;

    private double _resizePressY;

    private float _resizeStartHeight;

    private ExecutionOperatorEvent? _selectedOperator;

    private OperatorHit? _hoverOperator;

    public event Action<PlanNodeIdentifier>? PlanNodeSelected;

    private bool ShowsOperators => ShowOperators && _operatorRows.Length > 0;

    private float OperatorsTop => RulerTop + RulerHeight + PlayheadStripHeight;

    private float OperatorsArea => ShowsOperators ? OperatorsHeight + SplitterHeight : 0;

    private float OperatorsRoom
        => Math.Max(MinimumOperatorsHeight,
                    (float)_overlay.ActualHeight - OperatorsTop - BandHeight - SplitterHeight - MinimumLanesHeight);

    private float OperatorsHeight
    {
        get
        {
            if (!ShowsOperators)
            {
                return 0;
            }

            var room = OperatorsRoom;

            var compact = _operatorRows.Length * CompactOperatorRowHeight
                          + (_operatorRows.Length - 1) * OperatorRowGap
                          + OperatorsPadding * 2;

            var wanted = _operatorsHeight ?? Math.Min(compact, room * CompactOperatorsShare);

            return Math.Clamp(wanted, MinimumOperatorsHeight, room);
        }
    }

    private float OperatorRowHeight
    {
        get
        {
            var rows = Math.Max(_operatorRows.Length, 1);

            return Math.Max((OperatorsHeight - OperatorsPadding * 2 - (rows - 1) * OperatorRowGap) / rows, 1);
        }
    }

    private static OperatorRow[] OperatorRowsOf(TimeTravelTimeline? timeline)
    {
        if (timeline is null)
        {
            return [];
        }

        var lanes = new Dictionary<uint, int>();

        for (var lane = 0; lane < timeline.Threads.Count; lane++)
        {
            lanes[timeline.Threads[lane].ThreadId] = lane;
        }

        return [.. timeline.Lifetimes
                           .Where(l => lanes.ContainsKey(l.Thread))
                           .GroupBy(l => l.Operator, ReferenceEqualityComparer.Instance)
                           .Select(g => RowOf((ExecutionOperatorEvent)g.Key!, [.. g.OrderBy(l => lanes[l.Thread])]))
                           .OrderBy(r => r.Operator.NodeLevel)
                           .ThenBy(r => r.Lifetimes.Min(l => l.StartOf(TimeTravelTimelineAxis.Position)))
                           .ThenBy(r => r.Operator.PlanNodeIdentifier?.NodeId ?? 0)];
    }

    private static OperatorRow RowOf(ExecutionOperatorEvent operatorEvent, TimeTravelOperatorLifetime[] lifetimes)
    {
        var target = operatorEvent.ObjectName.Length > 0
            ? operatorEvent.ObjectName
            : operatorEvent.LogicalOperator != operatorEvent.Name ? operatorEvent.LogicalOperator : string.Empty;

        var title = operatorEvent.PlanNodeIdentifier is { } identifier
            ? $"{operatorEvent.OperatorDescription} (Node {identifier.NodeId})"
            : operatorEvent.OperatorDescription;

        return new OperatorRow(operatorEvent, lifetimes, operatorEvent.Name, target, title);
    }

    private void DrawOperators(SKCanvas canvas, int width, double scale)
    {
        if (!ShowsOperators)
        {
            return;
        }

        var top = OperatorsTop;

        var height = OperatorsHeight;

        _paints.Fill.Color = OperatorPaneColour;

        canvas.DrawRect(0, top, width, height, _paints.Fill);

        canvas.Save();

        canvas.ClipRect(new SKRect(0, top, width, top + height));

        for (var row = 0; row < _operatorRows.Length; row++)
        {
            for (var track = 0; track < _operatorRows[row].Lifetimes.Length; track++)
            {
                if (TrackRect(row, track, width, scale) is { } rect)
                {
                    var lifetime = _operatorRows[row].Lifetimes[track];

                    DrawOperatorBar(canvas, _operatorRows[row], lifetime, rect, scale, !IsRaised(lifetime));
                }
            }
        }

        canvas.Restore();

        DrawSplitter(canvas, top + height, width);
    }

    private void DrawOperatorBar(SKCanvas canvas,
                                 OperatorRow row,
                                 TimeTravelOperatorLifetime lifetime,
                                 SKRect rect,
                                 double scale,
                                 bool labelled)
    {
        var colour = OperatorColourOf(row.Operator, lifetime);

        var radius = Math.Min(rect.Height / 2, OperatorCornerRadius);

        _operatorShape.SetRect(rect, radius, radius);

        _paints.Fill.IsAntialias = true;

        _paints.Fill.Color = TimelineColours.Scale(colour, OperatorIdleShade);

        canvas.DrawRoundRect(_operatorShape, _paints.Fill);

        _paints.Fill.IsAntialias = false;

        canvas.Save();

        canvas.ClipRoundRect(_operatorShape, antialias: true);

        canvas.Translate(0, rect.Top);

        canvas.Scale(1, rect.Height);

        _paints.Fill.Color = colour;

        _paints.Fill.Shader = OperatorGradientOf(colour);

        DrawActivity(canvas, lifetime, scale);

        _paints.Fill.Shader = null;

        canvas.Restore();

        if (labelled)
        {
            DrawOperatorLabel(canvas, row, rect);
        }
    }

    private bool IsRaised(TimeTravelOperatorLifetime lifetime)
        => ShowMemory && MemoryMode == FlameChartMemoryMode.InUse && _inUseMaximum > 0 && !lifetime.InUse.IsEmpty;

    private SKShader OperatorGradientOf(SKColor colour)
    {
        if (!_operatorGradients.TryGetValue(colour, out var gradient))
        {
            gradient = SKShader.CreateLinearGradient(new SKPoint(0, 0),
                                                     new SKPoint(0, 1),
                                                     [
                                                         TimelineColours.Scale(colour, 1f + OperatorGradientLift),
                                                         TimelineColours.Scale(colour, 1f - OperatorGradientLift)
                                                     ],
                                                     null,
                                                     SKShaderTileMode.Clamp);

            _operatorGradients[colour] = gradient;
        }

        return gradient;
    }

    private void DrawActivity(SKCanvas canvas, TimeTravelOperatorLifetime lifetime, double scale)
    {
        var calls = lifetime.Calls(_axis);

        var runStart = -1f;

        var runEnd = -1f;

        var index = lifetime.FirstEndingAfter(_axis, _viewStart);

        while (index < calls.Count && calls[index].Start <= _viewEnd)
        {
            var left = (float)((calls[index].Start - _viewStart) * scale);

            var right = Math.Max((float)((calls[index].End - _viewStart) * scale), left + NarrowSpan);

            if (runStart >= 0 && left <= runEnd + NarrowSpan)
            {
                runEnd = Math.Max(runEnd, right);
            }
            else
            {
                if (runStart >= 0)
                {
                    canvas.DrawRect(runStart, 0, runEnd - runStart, 1, _paints.Fill);
                }

                runStart = left;

                runEnd = right;
            }

            index = lifetime.FirstEndingAfter(_axis, _viewStart + (MathF.Floor(right) + 1f) / scale, index + 1);
        }

        if (runStart >= 0)
        {
            canvas.DrawRect(runStart, 0, runEnd - runStart, 1, _paints.Fill);
        }
    }

    private void DrawOperatorLabel(SKCanvas canvas, OperatorRow row, SKRect rect)
    {
        var left = Math.Max(rect.Left, 0) + LabelPadding * 2;

        var available = rect.Right - LabelPadding * 2 - left;

        var sizeByHeight = Math.Min(OperatorMaximumFont, rect.Height - 2);

        if (available < MinimumOperatorLabelWidth || sizeByHeight < OperatorMinimumFont)
        {
            return;
        }

        var bold = _paints.OperatorBoldFont;

        var regular = _paints.OperatorFont;

        bold.Size = OperatorMaximumFont;

        regular.Size = OperatorMaximumFont;

        var nameWidth = Math.Max(bold.MeasureText(row.Name), 1);

        var bothWidth = row.Target.Length > 0
            ? nameWidth + OperatorMaximumFont * OperatorLabelGap + regular.MeasureText(row.Target)
            : nameWidth;

        var bothSize = Math.Min(sizeByHeight, OperatorMaximumFont * available / bothWidth);

        var withTarget = row.Target.Length > 0 && bothSize >= OperatorMinimumFont;

        var size = withTarget ? bothSize : Math.Min(sizeByHeight, OperatorMaximumFont * available / nameWidth);

        if (size < OperatorMinimumFont)
        {
            return;
        }

        bold.Size = size;

        regular.Size = size;

        var baseline = rect.MidY + size * OperatorBaselineShift;

        _paints.Text.Color = OperatorLabelColour;

        canvas.Save();

        canvas.ClipRect(rect);

        canvas.DrawText(row.Name, left, baseline, SKTextAlign.Left, bold, _paints.Text);

        if (withTarget)
        {
            canvas.DrawText(row.Target,
                            left + bold.MeasureText(row.Name) + size * OperatorLabelGap,
                            baseline,
                            SKTextAlign.Left,
                            regular,
                            _paints.Text);
        }

        canvas.Restore();
    }

    private void DrawSplitter(SKCanvas canvas, float top, int width)
    {
        _paints.Fill.Color = _paints.LaneHeader;

        canvas.DrawRect(0, top, width, SplitterHeight, _paints.Fill);

        var centre = top + SplitterHeight / 2;

        canvas.DrawLine(width / 2f - SplitterGripWidth / 2, centre, width / 2f + SplitterGripWidth / 2, centre, _paints.Tick);
    }

    private SKRect? TrackRect(int row, int track, int width, double scale)
    {
        var (top, height) = TrackBounds(row, track);

        var lifetime = _operatorRows[row].Lifetimes[track];

        var start = lifetime.StartOf(_axis);

        var end = lifetime.EndOf(_axis);

        if (end < _viewStart || start > _viewEnd)
        {
            return null;
        }

        var left = (float)((start - _viewStart) * scale);

        var right = Math.Max((float)((end - _viewStart) * scale), left + NarrowSpan * 2);

        return new SKRect(Math.Max(left, -OperatorCornerRadius * 2),
                          top,
                          Math.Min(right, width + OperatorCornerRadius * 2),
                          top + height);
    }

    private (float Top, float Height) TrackBounds(int row, int track)
    {
        var rowHeight = OperatorRowHeight;

        var trackHeight = rowHeight / _operatorRows[row].Lifetimes.Length;

        var padding = Math.Min(OperatorBarPadding, trackHeight * OperatorBarShare);

        return (OperatorRowTop(row) + track * trackHeight + padding, Math.Max(trackHeight - padding * 2, 1));
    }

    private float OperatorRowTop(int row) => OperatorsTop + OperatorsPadding + row * (OperatorRowHeight + OperatorRowGap);

    private SKColor OperatorColourOf(ExecutionOperatorEvent operatorEvent, TimeTravelOperatorLifetime lifetime)
        => ColourProvider is { } colours ? colours.GetColour(operatorEvent).ToSkColor() : ColourOf(lifetime.Node);

    private void DrawOperatorOverlay(SKCanvas canvas, int width)
    {
        if (!ShowsOperators)
        {
            return;
        }

        var scale = PixelsPerUnit(width);

        canvas.Save();

        canvas.ClipRect(new SKRect(0, OperatorsTop, width, OperatorsTop + OperatorsHeight));

        DimUnselectedOperators(canvas, width);

        if (_hoverOperator is { } hover && hover.Row < _operatorRows.Length)
        {
            OutlineTrack(canvas, hover.Row, hover.Track, width, scale, OperatorHoverColour, 1f);
        }

        for (var row = 0; row < _operatorRows.Length; row++)
        {
            if (!ReferenceEquals(_operatorRows[row].Operator, _selectedOperator))
            {
                continue;
            }

            for (var track = 0; track < _operatorRows[row].Lifetimes.Length; track++)
            {
                OutlineTrack(canvas, row, track, width, scale, OperatorSelectedColour, OperatorSelectedStroke);
            }
        }

        canvas.Restore();
    }

    private void DimUnselectedOperators(SKCanvas canvas, int width)
    {
        if (_selectedOperator is null)
        {
            return;
        }

        var rowHeight = OperatorRowHeight;

        for (var row = 0; row < _operatorRows.Length; row++)
        {
            if (ReferenceEquals(_operatorRows[row].Operator, _selectedOperator))
            {
                var top = OperatorRowTop(row);

                _pathBuilder.AddRect(new SKRect(0, top, width, top + rowHeight));
            }
        }

        using var selected = _pathBuilder.Detach();

        canvas.Save();

        canvas.ClipPath(selected, SKClipOperation.Difference);

        _paints.Fill.Color = OperatorDimColour;

        canvas.DrawRect(0, OperatorsTop, width, OperatorsHeight, _paints.Fill);

        canvas.Restore();
    }

    private void OutlineTrack(SKCanvas canvas, int row, int track, int width, double scale, SKColor colour, float stroke)
    {
        if (TrackRect(row, track, width, scale) is not { } rect)
        {
            return;
        }

        var radius = Math.Min(rect.Height / 2, OperatorCornerRadius);

        _paints.Outline.Color = colour;

        _paints.Outline.StrokeWidth = stroke;

        canvas.DrawRoundRect(rect, radius, radius, _paints.Outline);
    }

    private void DrawSelectedCalls(SKCanvas canvas, int width, int height)
    {
        if (_selectedOperator is null || _visible is not { } timeline)
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

        if (_selectedOperator is not { } selected || _visible is not { } timeline)
        {
            return;
        }

        var instances = new HashSet<ulong>(selected.Instances);

        var found = new Dictionary<int, List<SelectedRegion>>();

        foreach (var span in timeline.ResolvedSpans())
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
                                           span.Span(TimeTravelTimelineAxis.Position),
                                           span.Span(TimeTravelTimelineAxis.Instructions)));
        }

        foreach (var (lane, regions) in found)
        {
            var outermost = Outermost.Of(regions, r => r.Position.Start, r => r.Position.End);

            _selectedLanes[lane] = new SelectedLane([.. outermost],
                                                    [.. outermost.Select(r => r.Position.End)],
                                                    [.. outermost.Select(r => r.Instructions.End)]);
        }
    }

    private void CollectOperatorSurfaces(float firstPixel, float lastPixel, double scale)
    {
        if (!ShowsOperators)
        {
            return;
        }

        for (var row = 0; row < _operatorRows.Length; row++)
        {
            var rowStart = StartSpikeRow();

            for (var track = 0; track < _operatorRows[row].Lifetimes.Length; track++)
            {
                var lifetime = _operatorRows[row].Lifetimes[track];

                if (lifetime.InUse.IsEmpty)
                {
                    continue;
                }

                var left = (float)((lifetime.StartOf(_axis) - _viewStart) * scale);

                var right = (float)((lifetime.EndOf(_axis) - _viewStart) * scale);

                if (right < firstPixel || left > lastPixel || right - left < SurfaceBucketWidth)
                {
                    continue;
                }

                AddOperatorSurface(row, track, Math.Max(left, firstPixel), Math.Min(right, lastPixel), scale);
            }

            EndSpikeRow(rowStart);
        }
    }

    private void AddOperatorSurface(int row, int track, float left, float right, double scale)
    {
        var operatorRow = _operatorRows[row];

        var lifetime = operatorRow.Lifetimes[track];

        var (top, height) = TrackBounds(row, track);

        var colour = OperatorColourOf(operatorRow.Operator, lifetime);

        var source = BlockSource.Of(new OperatorHit(row, track));

        var first = _spikes.Count;

        for (var x = left; x < right; x += SurfaceBucketWidth)
        {
            var end = Math.Min(x + SurfaceBucketWidth, right);

            var bytes = lifetime.InUse.PeakDuring(lifetime.PositionAt(_axis, _viewStart + x / scale),
                                                  lifetime.PositionAt(_axis, _viewStart + end / scale));

            var last = _spikes.Count - 1;

            if (last >= first && _spikes[last].Bytes == bytes && _spikes[last].Right >= x)
            {
                _spikes[last] = _spikes[last] with { Right = end };
            }
            else
            {
                _spikes.Add(Raised(source, lifetime.Node, x, end, top, height, bytes, _inUseMaximum) with { Colour = colour });
            }
        }

        FinishSurface(first);
    }

    private string TextOf(Block block)
        => block.Source.Operator is { } operatorHit && operatorHit.Row < _operatorRows.Length
            ? _operatorRows[operatorHit.Row].Name
            : LabelOf(block.Node);

    private OperatorHit? OperatorAt(Point position)
    {
        if (!IsInOperators(position))
        {
            return null;
        }

        var rowHeight = OperatorRowHeight;

        var y = (float)position.Y - OperatorsTop - OperatorsPadding;

        var row = (int)Math.Floor(y / (rowHeight + OperatorRowGap));

        var inRow = y - row * (rowHeight + OperatorRowGap);

        if (row < 0 || row >= _operatorRows.Length || inRow >= rowHeight)
        {
            return null;
        }

        var lifetimes = _operatorRows[row].Lifetimes;

        var track = Math.Clamp((int)(inRow / (rowHeight / lifetimes.Length)), 0, lifetimes.Length - 1);

        var scale = PixelsPerUnit((int)_overlay.ActualWidth);

        var value = _viewStart + position.X / scale;

        var tolerance = HitTolerance / scale;

        return lifetimes[track].StartOf(_axis) - tolerance <= value && value <= lifetimes[track].EndOf(_axis) + tolerance
            ? new OperatorHit(row, track)
            : null;
    }

    private bool IsInOperators(Point position)
        => ShowsOperators && position.Y >= OperatorsTop && position.Y < OperatorsTop + OperatorsHeight;

    private bool IsOnSplitter(Point position)
    {
        if (!ShowsOperators)
        {
            return false;
        }

        var top = OperatorsTop + OperatorsHeight;

        return position.Y >= top - SplitterHitMargin && position.Y < top + SplitterHeight + SplitterHitMargin;
    }

    private void ClickOperator(OperatorHit? hit)
    {
        var clicked = hit is { } found ? _operatorRows[found.Row].Operator : null;

        _selectedOperator = ReferenceEquals(clicked, _selectedOperator) ? null : clicked;

        FindSelectedRegions();

        _canvas.Invalidate();

        if (_selectedOperator?.PlanNodeIdentifier is { } identifier)
        {
            PlanNodeSelected?.Invoke(identifier);
        }
    }

    private void ZoomToOperator(OperatorHit hit)
    {
        var lifetime = _operatorRows[hit.Row].Lifetimes[hit.Track];

        var start = lifetime.StartOf(_axis);

        var end = lifetime.EndOf(_axis);

        var margin = (end - start) * 0.05;

        SetView(start - margin, end + margin);
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

    private void ShowOperatorToolTip(OperatorHit hit, Point position)
    {
        var lifetime = _operatorRows[hit.Row].Lifetimes[hit.Track];

        if (!_operatorDetails.TryGetValue(lifetime, out var details))
        {
            details = DetailsOf(_operatorRows[hit.Row], lifetime);

            _operatorDetails[lifetime] = details;
        }

        OpenToolTip(details, position);
    }

    private string DetailsOf(OperatorRow row, TimeTravelOperatorLifetime lifetime)
    {
        var methods = new Dictionary<string, int>();

        var active = 0d;

        foreach (var call in lifetime.InstructionCalls)
        {
            var label = LabelOf(call.Node);

            var separator = label.LastIndexOf("::", StringComparison.Ordinal);

            var method = separator < 0 ? label : label[(separator + 2)..];

            methods[method] = methods.GetValueOrDefault(method) + 1;

            active += call.End - call.Start;
        }

        var instructions = lifetime.EndOf(TimeTravelTimelineAxis.Instructions) - lifetime.StartOf(TimeTravelTimelineAxis.Instructions);

        var start = lifetime.StartOf(TimeTravelTimelineAxis.Position);

        var end = lifetime.EndOf(TimeTravelTimelineAxis.Position);

        var lines = new List<string>
        {
            row.Title,
            $"Thread {lifetime.Thread}",
            string.Join("  ·  ", methods.Select(m => $"{m.Key} {m.Value:N0}")),
            $"Instructions {instructions:N0}",
            $"Position {Math.Floor(start):N0} to {Math.Floor(end):N0}"
        };

        if (instructions > 0)
        {
            lines.Add($"Inside Calls {active / instructions:P1} (Including Child Operators)");
        }

        if (!lifetime.InUse.IsEmpty)
        {
            lines.Add($"Peak In Use {SizeFormat.Format((long)lifetime.InUse.PeakDuring(start, end))}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static void OnShowOperatorsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FlameChartControl)d;

        control.HideToolTip();

        control._hoverOperator = null;

        control.Relayout();
    }

    private static void OnColourProviderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((FlameChartControl)d).Redraw();

    private sealed record OperatorRow(ExecutionOperatorEvent Operator,
                                      TimeTravelOperatorLifetime[] Lifetimes,
                                      string Name,
                                      string Target,
                                      string Title);

    private readonly record struct OperatorHit(int Row, int Track);

    private readonly record struct SelectedRegion(int Depth, TimeTravelTimelineSpan Position, TimeTravelTimelineSpan Instructions)
    {
        public TimeTravelTimelineSpan Of(TimeTravelTimelineAxis axis) => axis == TimeTravelTimelineAxis.Position ? Position : Instructions;
    }

    private sealed record SelectedLane(SelectedRegion[] Regions, double[] PositionEnds, double[] InstructionEnds)
    {
        public ReadOnlySpan<double> Ends(TimeTravelTimelineAxis axis)
            => axis == TimeTravelTimelineAxis.Position ? PositionEnds : InstructionEnds;
    }
}
