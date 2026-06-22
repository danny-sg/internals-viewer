using InternalsViewer.Replay.Events;
using InternalsViewer.Replay.Events.EventTypes;
using InternalsViewer.Replay.Plans;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI;

namespace InternalsViewer.UI.App.Controls.Timeline;

public sealed class EventTimelineControl : Grid
{
    private const float RulerBandHeight = 16f;
    private const float HandleBandHeight = 12f;
    private const float MarkerStripHeight = RulerBandHeight + HandleBandHeight;
    private const float HandleWidth = 7f;
    private const float HandleGap = 9f;
    private const float TriangleHalfWidth = 6f;
    private const double HitArea = 7;
    private const long DoubleClickMs = 300;
    private const double MaxResolutionMs = 10.0;
    private const float RowLabelWidth = 36f;
    private const float RowPadding = 2f;
    private const float MarkerWidth = 2f;

    private const double MinZoom = 1.0;
    private const double MaxZoom = 50.0;
    private const double ZoomStep = 1.15;

    // The relative-time axis (EngineEvent.TimeMs) is Ticks/1000, i.e. 10 units per real millisecond,
    // so an operator's duration (in ms) is scaled by this to find its length on the timeline.
    private const double AxisUnitsPerMs = 10.0;

    private static readonly TimeSpan PlayInterval = TimeSpan.FromMilliseconds(80);

    private static readonly SKColor PlayheadColour = new(230, 60, 60);
    private static readonly SKColor HandleColour = new(95, 95, 95);
    private static readonly SKColor StatementColour = new(130, 130, 130);

    // Per-category brightness applied to the row colour so each category band reads slightly differently.
    private static readonly float[] CategoryShade = [0.70f, 0.85f, 1.0f, 1.15f];

    // Operator events span a duration and are drawn as lines; the row is given extra weight so the
    // per-level tracks have room.
    private static readonly (Type EventType, string Label, SKColor Color, float Weight)[] Rows =
    [
        (typeof(ExecutionOperatorEvent), "Plan", SKColors.LimeGreen, 3f),
        (typeof(IoEvent),   "I/O",  ColourConstants.IoColour.ToSkColor().WithAlpha(255),   0.5f),
        (typeof(LockEvent), "Lock", ColourConstants.LockColour.ToSkColor().WithAlpha(255), 0.5f),
        (typeof(WaitEvent), "Wait", ColourConstants.WaitColour.ToSkColor().WithAlpha(255), 0.5f),
    ];

    private readonly Button _playButton;
    private readonly SKXamlCanvas _skCanvas;
    private readonly Canvas _overlay;
    private readonly ScrollBar _scrollBar;
    private readonly Popup _toolTip;
    private readonly TextBlock _toolTipText;

    private readonly List<(SKRect Bounds, EngineEvent Event)> _hitRegions = [];
    private EngineEvent? _hoverEvent;

    private readonly SKFont _labelFont = new(SKTypeface.Default, 10f);

    private readonly SKPaint _labelPaint = new()
    {
        Color = SKColors.LightGray,
        IsAntialias = true,
    };

    private readonly SKPaint _rowBgPaint = new() { Style = SKPaintStyle.Fill };
    private readonly SKPaint _markerPaint = new() { Style = SKPaintStyle.Fill };
    private readonly SKPaint _operatorPaint = new()
    {
        Color = SKColors.LimeGreen,
        Style = SKPaintStyle.Fill,
        IsAntialias = true,
    };

    // Gap left between stacked operator lines so they don't touch as they fill their slot.
    private const float OperatorLineMargin = 3f;

    // Operator labels are only drawn when the bar is tall and wide enough to be legible.
    private const float MinLabelBarHeight = 11f;
    private const float MinLabelBarWidth = 26f;

    private readonly SKPaint _operatorTextPaint = new() { IsAntialias = true };
    private readonly SKPaint _playheadPaint = new()
    {
        Color = PlayheadColour,
        StrokeWidth = 2,
        Style = SKPaintStyle.Stroke,
        IsAntialias = false,
    };
    private readonly SKPaint _playheadFill = new()
    {
        Color = PlayheadColour,
        Style = SKPaintStyle.Fill,
        IsAntialias = true,
    };
    private readonly SKPaint _handlePaint = new()
    {
        Color = HandleColour,
        Style = SKPaintStyle.Fill,
        IsAntialias = true,
    };
    private readonly SKPaint _selectionBandPaint = new()
    {
        Color = new SKColor(255, 255, 255, 28),
        Style = SKPaintStyle.Fill,
    };
    private readonly SKPaint _tickPaint = new()
    {
        Color = new SKColor(110, 110, 110),
        StrokeWidth = 1,
        Style = SKPaintStyle.Stroke,
        IsAntialias = false,
    };

    private List<EngineEvent> _sortedEvents = [];
    private List<double> _effectiveTimes = [];

    private double _minTime;
    private double _maxTime;
    private double _timeRange;

    private int _playIndex;
    private int _playheadIndex;
    private bool _isPlaying;
    private readonly DispatcherTimer _playTimer;

    private bool _isDragging;
    private DragTarget _dragTarget;

    private double _startTime;
    private double _endTime;

    private double _zoom = MinZoom;
    private double _scrollX;

    private long _lastPressTicks;
    private double _lastPressX;

    private enum DragTarget { None, Start, End, Playhead }

    public List<EngineEvent> Events
    {
        get => (List<EngineEvent>)GetValue(EventsProperty);
        set => SetValue(EventsProperty, value);
    }

    public static readonly DependencyProperty EventsProperty =
        DependencyProperty.Register(nameof(Events), typeof(List<EngineEvent>), typeof(EventTimelineControl),
            new PropertyMetadata(new List<EngineEvent>(), OnEventsChanged));

    private static void OnEventsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (EventTimelineControl)d;
        var events = (List<EngineEvent>)e.NewValue;

        ctrl._sortedEvents = [.. events.OrderBy(ev => ev.SequenceId)];
        ctrl.BuildEffectiveTimes();

        ctrl.StopPlay();
        ctrl.Reset();

        ctrl._skCanvas.Invalidate();
    }

    public long CurrentSequenceFrom { get; private set; }
    public long CurrentSequenceTo { get; private set; }
    public long CurrentPlayhead { get; private set; }

    /// <summary>Raised when the selection range changes. A collapsed selection reports (0, 0) = all.</summary>
    public event Action<long, long>? SequenceChanged;

    /// <summary>Raised when the playhead moves, with the sequence id of the event under it.</summary>
    public event Action<long>? PlayheadChanged;

    /// <summary>Raised when a plan operator in the timeline is clicked.</summary>
    public event Action<PlanNodeIdentifier>? PlanNodeSelected;

    /// <summary>Raised when auto-play starts (true) or stops (false).</summary>
    public event Action<bool>? PlayStateChanged;

    public EventTimelineControl()
    {
        Background = new SolidColorBrush(Colors.Transparent);

        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _playButton = new Button
        {
            Content = new FontIcon { Glyph = "", FontSize = 14 },
            Width = 40,
            VerticalAlignment = VerticalAlignment.Stretch,
            Padding = new Thickness(0),
            Background = new SolidColorBrush(Color.FromArgb(0, 30, 30, 30)),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
        };
        _playButton.Click += OnPlayButtonClick;
        Grid.SetColumn(_playButton, 0);
        Grid.SetRowSpan(_playButton, 2);
        Children.Add(_playButton);

        _skCanvas = new SKXamlCanvas { IgnorePixelScaling = true };
        _skCanvas.PaintSurface += OnPaintSurface;
        Grid.SetColumn(_skCanvas, 1);
        Children.Add(_skCanvas);

        _overlay = new Canvas { Background = new SolidColorBrush(Colors.Transparent) };
        Grid.SetColumn(_overlay, 1);
        Children.Add(_overlay);

        _scrollBar = new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            Visibility = Visibility.Collapsed,
            Height = 12,
            Minimum = 0,
            IndicatorMode = ScrollingIndicatorMode.MouseIndicator,
        };
        _scrollBar.Scroll += OnScrollBarScroll;
        Grid.SetColumn(_scrollBar, 1);
        Grid.SetRow(_scrollBar, 1);
        Children.Add(_scrollBar);

        _toolTipText = new TextBlock
        {
            Foreground = new SolidColorBrush(Colors.White),
            FontSize = 11,
            Margin = new Thickness(6, 3, 6, 3),
        };
        _toolTip = new Popup
        {
            IsHitTestVisible = false,
            Child = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(235, 30, 30, 30)),
                CornerRadius = new CornerRadius(3),
                IsHitTestVisible = false,
                Child = _toolTipText,
            },
        };
        _overlay.Children.Add(_toolTip);

        _overlay.PointerPressed += OnPointerPressed;
        _overlay.PointerMoved += OnPointerMoved;
        _overlay.PointerReleased += OnPointerReleased;
        _overlay.PointerCaptureLost += OnPointerReleased;
        _overlay.PointerWheelChanged += OnPointerWheelChanged;
        _overlay.PointerExited += OnPointerExited;
        _overlay.SizeChanged += OnOverlaySizeChanged;

        _playTimer = new DispatcherTimer { Interval = PlayInterval };
        _playTimer.Tick += OnPlayTimerTick;
    }

    private void Reset()
    {
        _zoom = MinZoom;
        _scrollX = 0;

        // Park the playhead at the end with a collapsed selection: everything is in scope and the
        // start/end handles cluster on the playhead at the right edge.
        _playheadIndex = _sortedEvents.Count > 0 ? _sortedEvents.Count - 1 : 0;
        _playIndex = _playheadIndex;

        var time = _playheadIndex < _effectiveTimes.Count ? _effectiveTimes[_playheadIndex] : 0;
        _startTime = time;
        _endTime = time;

        CurrentSequenceFrom = 0;
        CurrentSequenceTo = _sortedEvents.Count > 0 ? _sortedEvents[_playheadIndex].SequenceId : 0;
        CurrentPlayhead = CurrentSequenceTo;

        UpdateScrollBar();
    }

    private void BuildEffectiveTimes()
    {
        _effectiveTimes = new List<double>(_sortedEvents.Count);

        if (_sortedEvents.Count == 0)
        {
            _minTime = 0;
            _maxTime = 1;
            _timeRange = 1;
            return;
        }

        var bucketCounts = new Dictionary<long, int>();

        foreach (var ev in _sortedEvents)
        {
            var bucket = (long)Math.Floor(ev.TimeMs / MaxResolutionMs);
            bucketCounts[bucket] = bucketCounts.TryGetValue(bucket, out var c) ? c + 1 : 1;
        }

        var bucketIndex = new Dictionary<long, int>();
        foreach (var ev in _sortedEvents)
        {
            var bucket = (long)Math.Floor(ev.TimeMs / MaxResolutionMs);
            var bucketStart = bucket * MaxResolutionMs;
            bucketIndex.TryGetValue(bucket, out var idx);
            var count = bucketCounts[bucket];
            var step = count > 1 ? MaxResolutionMs / count : 0.0;
            _effectiveTimes.Add(bucketStart + idx * step);
            bucketIndex[bucket] = idx + 1;
        }

        _minTime = _effectiveTimes.Min();
        _maxTime = _effectiveTimes.Max();
        _timeRange = Math.Max(_maxTime - _minTime, 1.0);
    }

    private bool SelectionActive => Math.Abs(_startTime - _endTime) > 1e-6;

    private float CanvasWidth => (float)_overlay.ActualWidth;
    private float DrawWidth => CanvasWidth - RowLabelWidth;
    private double ContentWidth => DrawWidth * _zoom;
    private double MaxScroll => Math.Max(0, ContentWidth - DrawWidth);

    private float TimeToX(double effectiveTimeMs)
        => RowLabelWidth + (float)((effectiveTimeMs - _minTime) / _timeRange * ContentWidth - _scrollX);

    private double XToTime(double x)
        => _minTime + (Math.Max(0, x - RowLabelWidth) + _scrollX) / ContentWidth * _timeRange;

    private int XToEventIndex(double x)
    {
        if (_sortedEvents.Count == 0)
        {
            return 0;
        }

        var t = XToTime(x);
        var best = 0;
        var bestDist = double.MaxValue;
        for (var i = 0; i < _effectiveTimes.Count; i++)
        {
            var d = Math.Abs(_effectiveTimes[i] - t);
            if (d < bestDist) { bestDist = d; best = i; }
        }
        return best;
    }

    private float PlayheadX => _playheadIndex < _effectiveTimes.Count
        ? TimeToX(_effectiveTimes[_playheadIndex])
        : RowLabelWidth;

    private float StartDrawX => SelectionActive ? TimeToX(_startTime) : TimeToX(_startTime) - HandleGap;
    private float EndDrawX => SelectionActive ? TimeToX(_endTime) : TimeToX(_endTime) + HandleGap;

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var w = e.Info.Width;
        var h = e.Info.Height;

        if (_sortedEvents.Count == 0 || w <= 0 || h <= 0)
        {
            return;
        }

        var rowsTop = MarkerStripHeight;
        var rowsHeight = h - rowsTop;
        var rowCount = Rows.Length;

        var totalWeight = Rows.Sum(r => r.Weight);
        var rowTops = new float[rowCount];
        var rowHeights = new float[rowCount];
        var acc = rowsTop;
        for (var r = 0; r < rowCount; r++)
        {
            rowTops[r] = acc;
            rowHeights[r] = rowsHeight * Rows[r].Weight / totalWeight;
            acc += rowHeights[r];
        }

        for (var r = 0; r < rowCount; r++)
        {
            var (_, label, _, _) = Rows[r];
            var y = rowTops[r];
            var rowHeight = rowHeights[r];

            _rowBgPaint.Color = r % 2 == 0
                ? new SKColor(30, 30, 30, 220)
                : new SKColor(20, 20, 20, 220);
            canvas.DrawRect(0, y, w, rowHeight, _rowBgPaint);

            canvas.DrawText(label, 2, y + rowHeight / 2 + _labelFont.Size / 2, SKTextAlign.Left, _labelFont, _labelPaint);

            using var sepPaint = new SKPaint { Color = new SKColor(60, 60, 60), StrokeWidth = 1 };
            canvas.DrawLine(0, y + rowHeight, w, y + rowHeight, sepPaint);
        }

        canvas.Save();
        canvas.ClipRect(new SKRect(RowLabelWidth, 0, w, h));

        _hitRegions.Clear();

        for (var i = 0; i < _sortedEvents.Count; i++)
        {
            var ev = _sortedEvents[i];

            // Operator events have a duration and are drawn as lines in a separate pass.
            if (ev is ExecutionOperatorEvent)
            {
                continue;
            }

            var x = TimeToX(_effectiveTimes[i]);
            var rowIndex = GetRowIndex(ev);
            if (rowIndex < 0)
            {
                continue;
            }

            var rowTop = rowTops[rowIndex];
            var innerTop = rowTop + RowPadding;
            var innerHeight = rowHeights[rowIndex] - RowPadding * 2;

            float markerTop;
            float markerHeight;

            var category = EventCategoryClassifier.GetCategory(ev);

            if (category.HasValue)
            {
                // Locks and waits step into one of four category bands within their row, each a
                // slightly different shade of the row colour.
                var stepHeight = innerHeight / EventCategoryClassifier.CategoryCount;
                var step = (int)category.Value;

                markerTop = innerTop + step * stepHeight;
                markerHeight = Math.Max(2f, stepHeight - 1f);
                _markerPaint.Color = TintByCategory(Rows[rowIndex].Color, step);
            }
            else
            {
                // The single marker paint is reused; only its (struct) colour changes per event.
                // Fall back to the row colour when the event has no display colour set.
                var displayColour = ev.DisplayColour;
                markerTop = innerTop;
                markerHeight = innerHeight;
                _markerPaint.Color = displayColour.A == 0 ? Rows[rowIndex].Color : displayColour.ToSkColor();
            }

            canvas.DrawRect(x, markerTop, MarkerWidth, markerHeight, _markerPaint);

            // Widen the hit target a little so the 2px markers are easy to hover.
            _hitRegions.Add((new SKRect(x - 3, markerTop, x + MarkerWidth + 3, markerTop + markerHeight), ev));
        }

        DrawOperatorLines(canvas, rowTops, rowHeights);

        // Time ruler (ticks + labels) along the top.
        DrawRuler(canvas);

        // Selection band between the two handles.
        if (SelectionActive)
        {
            var lo = Math.Min(TimeToX(_startTime), TimeToX(_endTime));
            var hi = Math.Max(TimeToX(_startTime), TimeToX(_endTime));
            canvas.DrawRect(lo, rowsTop, hi - lo, rowsHeight, _selectionBandPaint);
        }

        // Dark-grey rectangle handles for start and end.
        DrawHandle(canvas, StartDrawX);
        DrawHandle(canvas, EndDrawX);

        // Red playhead: line down through the rows with a triangle in the strip.
        var px = PlayheadX;
        canvas.DrawLine(px, MarkerStripHeight, px, h, _playheadPaint);
        DrawPlayheadTriangle(canvas, px);
        DrawPlayheadTimeBadge(canvas, px);

        canvas.Restore();
    }

    /// <summary>Draws evenly spaced time ticks with labels across the visible range.</summary>
    private void DrawRuler(SKCanvas canvas)
    {
        var leftMs = EffectiveToMs(XToTime(RowLabelWidth));
        var rightMs = EffectiveToMs(XToTime(CanvasWidth));
        var rangeMs = rightMs - leftMs;

        if (rangeMs <= 0)
        {
            return;
        }

        var targetTicks = Math.Max(2, DrawWidth / 80f);
        var interval = NiceInterval(rangeMs / targetTicks);

        if (interval <= 0)
        {
            return;
        }

        for (var tickMs = Math.Ceiling(leftMs / interval) * interval; tickMs <= rightMs; tickMs += interval)
        {
            var x = TimeToX(_minTime + tickMs * AxisUnitsPerMs);

            canvas.DrawLine(x, RulerBandHeight - 4, x, RulerBandHeight, _tickPaint);
            canvas.DrawText(FormatTime(tickMs), x + 2, RulerBandHeight - 6, SKTextAlign.Left, _labelFont, _labelPaint);
        }
    }

    /// <summary>Draws a red badge showing the playhead's time, on top of the ruler.</summary>
    private void DrawPlayheadTimeBadge(SKCanvas canvas, float px)
    {
        if (_playheadIndex >= _effectiveTimes.Count)
        {
            return;
        }

        var text = FormatTime(EffectiveToMs(_effectiveTimes[_playheadIndex]));

        const float padding = 4f;
        var badgeWidth = _labelFont.MeasureText(text) + padding * 2;
        var badgeHeight = RulerBandHeight - 2;
        var bx = Math.Clamp(px - badgeWidth / 2f, RowLabelWidth, Math.Max(RowLabelWidth, CanvasWidth - badgeWidth));

        canvas.DrawRoundRect(new SKRect(bx, 0, bx + badgeWidth, badgeHeight), 2, 2, _playheadFill);

        _operatorTextPaint.Color = SKColors.White;
        var baseline = badgeHeight / 2f + _labelFont.Size * 0.35f;
        canvas.DrawText(text, bx + padding, baseline, SKTextAlign.Left, _labelFont, _operatorTextPaint);
    }

    private double EffectiveToMs(double effective) => (effective - _minTime) / AxisUnitsPerMs;

    private static double NiceInterval(double raw)
    {
        if (raw <= 0)
        {
            return 0;
        }

        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var fraction = raw / magnitude;
        var nice = fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10;

        return nice * magnitude;
    }

    private static string FormatTime(double ms)
    {
        if (ms < 0)
        {
            ms = 0;
        }

        if (ms < 10)
        {
            return $"{ms:0.##}ms";
        }

        if (ms < 1000)
        {
            return $"{ms:0}ms";
        }

        var seconds = ms / 1000.0;
        return seconds < 10 ? $"{seconds:0.00}s" : $"{seconds:0.0}s";
    }

    /// <summary>
    /// Draws operator events as horizontal lines spanning their duration. Each plan level is a band;
    /// nodes sharing a level are offset within it, ordered by start time then node id. The per-node
    /// slot height is anchored to the busiest level so it stays consistent across all levels.
    /// </summary>
    private void DrawOperatorLines(SKCanvas canvas, float[] rowTops, float[] rowHeights)
    {
        var planRow = -1;
        for (var r = 0; r < Rows.Length; r++)
        {
            if (Rows[r].EventType == typeof(ExecutionOperatorEvent)) { planRow = r; break; }
        }

        if (planRow < 0)
        {
            return;
        }

        // Gather operators with the index that gives their start time on the axis.
        var operators = new List<(int Index, ExecutionOperatorEvent Op)>();
        for (var i = 0; i < _sortedEvents.Count; i++)
        {
            if (_sortedEvents[i] is ExecutionOperatorEvent op)
            {
                operators.Add((i, op));
            }
        }

        if (operators.Count == 0)
        {
            return;
        }

        var levels = operators.Max(o => o.Op.NodeLevel) + 1;

        // Order each level by start time (then node id) and record each node's slot within its level.
        var slotByIndex = new Dictionary<int, int>(operators.Count);
        var countByLevel = new Dictionary<int, int>();
        var maxNodesInLevel = 1;

        foreach (var level in operators.GroupBy(o => o.Op.NodeLevel))
        {
            var ordered = level
                .OrderBy(o => _effectiveTimes[o.Index])
                .ThenBy(o => o.Op.PlanNodeIdentifier?.NodeId ?? 0)
                .ToList();

            maxNodesInLevel = Math.Max(maxNodesInLevel, ordered.Count);
            countByLevel[level.Key] = ordered.Count;

            for (var slot = 0; slot < ordered.Count; slot++)
            {
                slotByIndex[ordered[slot].Index] = slot;
            }
        }

        var top = rowTops[planRow] + RowPadding;
        var height = rowHeights[planRow] - RowPadding * 2;

        // Weighted level bands: the statement node (level 0) gets a half-height band, each operator
        // level a full band. Within a band the slots are sized by the busiest level, so the operator
        // bars stay consistent.
        var bandTop = new float[levels];
        var bandHeight = new float[levels];
        var totalWeight = 0f;
        for (var level = 0; level < levels; level++) totalWeight += LevelWeight(level);

        var bandAcc = top;
        for (var level = 0; level < levels; level++)
        {
            bandTop[level] = bandAcc;
            bandHeight[level] = height * LevelWeight(level) / totalWeight;
            bandAcc += bandHeight[level];
        }

        foreach (var (index, op) in operators)
        {
            var startX = TimeToX(_effectiveTimes[index]);
            var endX = TimeToX(_effectiveTimes[index] + op.Duration * AxisUnitsPerMs);
            if (endX < startX + 2)
            {
                endX = startX + 2;
            }

            var level = op.NodeLevel;

            float slotHeight;
            float y;
            SKColor barColour;

            if (level == 0)
            {
                // The statement (SELECT) node fills its half-height band as a single grey bar.
                slotHeight = bandHeight[level];
                y = bandTop[level] + slotHeight / 2f;
                barColour = StatementColour;
            }
            else
            {
                // Consistent bar thickness across levels (anchored to the busiest level), but spread
                // the level's nodes evenly across its band so sparse levels centre rather than
                // top-align with a gap underneath.
                slotHeight = bandHeight[level] / maxNodesInLevel;
                var spacing = bandHeight[level] / countByLevel[level];
                y = bandTop[level] + (slotByIndex[index] + 0.5f) * spacing;

                // Fall back to the row colour when the event has no display colour set.
                var displayColour = op.DisplayColour;
                barColour = displayColour.A == 0 ? Rows[planRow].Color : displayColour.ToSkColor();
            }

            // Bar thickness fills the slot less a margin (never below 1px when crowded).
            var lineWidth = Math.Max(1f, slotHeight - OperatorLineMargin);
            var cornerRadius = Math.Min(lineWidth / 2f, 3f);

            _operatorPaint.Color = barColour;
            canvas.DrawRoundRect(new SKRect(startX, y - lineWidth / 2f, endX, y + lineWidth / 2f),
                                 cornerRadius, cornerRadius, _operatorPaint);

            if (lineWidth >= MinLabelBarHeight && endX - startX >= MinLabelBarWidth)
            {
                DrawOperatorLabel(canvas, op, startX, endX, y, lineWidth, barColour);
            }

            _hitRegions.Add((new SKRect(startX, y - slotHeight / 2f, endX, y + slotHeight / 2f), op));
        }
    }

    // The statement (SELECT) band is half the height of an operator level band.
    private const float StatementBandWeight = 0.5f;

    private static float LevelWeight(int level) => level == 0 ? StatementBandWeight : 1f;

    /// <summary>Draws the operator name and target inside the bar, clipped to it, in a contrasting colour.</summary>
    private void DrawOperatorLabel(SKCanvas canvas, ExecutionOperatorEvent op,
                                   float startX, float endX, float y, float barHeight, SKColor barColour)
    {
        var label = BuildOperatorLabel(op);
        if (label.Length == 0)
        {
            return;
        }

        _operatorTextPaint.Color = ContrastingColour(barColour);

        canvas.Save();
        canvas.ClipRect(new SKRect(startX, y - barHeight / 2f, endX, y + barHeight / 2f));

        // Centre the text vertically within the bar.
        var baseline = y + _labelFont.Size * 0.35f;
        canvas.DrawText(label, startX + 4, baseline, SKTextAlign.Left, _labelFont, _operatorTextPaint);

        canvas.Restore();
    }

    private static string BuildOperatorLabel(ExecutionOperatorEvent op)
    {
        var name = op.Name;
        var target = op.ObjectName;

        if (string.IsNullOrEmpty(name))
        {
            return target;
        }

        if (string.IsNullOrEmpty(target))
        {
            return name;
        }

        return $"{name}  {target}";
    }

    /// <summary>Black or white, whichever reads better on the given fill colour.</summary>
    private static SKColor ContrastingColour(SKColor background)
    {
        var luminance = 0.299 * background.Red + 0.587 * background.Green + 0.114 * background.Blue;
        return luminance > 150 ? SKColors.Black : SKColors.White;
    }

    private void DrawHandle(SKCanvas canvas, float x)
        => canvas.DrawRect(x - HandleWidth / 2f, RulerBandHeight, HandleWidth, HandleBandHeight, _handlePaint);

    private void DrawPlayheadTriangle(SKCanvas canvas, float x)
    {
        using var path = new SKPath();

        path.MoveTo(x, MarkerStripHeight);
        path.LineTo(x - TriangleHalfWidth, RulerBandHeight);
        path.LineTo(x + TriangleHalfWidth, RulerBandHeight);
        path.Close();

        canvas.DrawPath(path, _playheadFill);
    }

    private static int GetRowIndex(EngineEvent ev)
    {
        for (var i = 0; i < Rows.Length; i++)
            if (Rows[i].EventType.IsInstanceOfType(ev))
            {
                return i;
            }

        return -1;
    }

    private static SKColor TintByCategory(SKColor colour, int category)
    {
        var factor = CategoryShade[category];

        return new SKColor(
            (byte)Math.Clamp(colour.Red * factor, 0, 255),
            (byte)Math.Clamp(colour.Green * factor, 0, 255),
            (byte)Math.Clamp(colour.Blue * factor, 0, 255),
            colour.Alpha);
    }

    /// <summary>
    /// Emits the in-scope window the other views highlight. With an explicit selection that is the
    /// selected range; otherwise it runs from the start up to the playhead, so the playhead drives
    /// the rest of the views as it is scrubbed or played.
    /// </summary>
    private void EmitScope()
    {
        if (SelectionActive)
        {
            var lo = Math.Min(_startTime, _endTime);
            var hi = Math.Max(_startTime, _endTime);

            CurrentSequenceFrom = SequenceIdAtOrAfter(lo);
            CurrentSequenceTo = SequenceIdAtOrBefore(hi);
        }
        else
        {
            CurrentSequenceFrom = 0;
            CurrentSequenceTo = _sortedEvents.Count > 0 ? _sortedEvents[_playheadIndex].SequenceId : 0;
        }

        SequenceChanged?.Invoke(CurrentSequenceFrom, CurrentSequenceTo);
    }

    private void FirePlayhead()
    {
        CurrentPlayhead = _sortedEvents.Count > 0 ? _sortedEvents[_playheadIndex].SequenceId : 0;
        PlayheadChanged?.Invoke(CurrentPlayhead);

        // The scope's right edge tracks the playhead when there's no explicit selection.
        EmitScope();
    }

    private long SequenceIdAtOrAfter(double t)
    {
        if (_sortedEvents.Count == 0)
        {
            return 0;
        }

        for (var i = 0; i < _effectiveTimes.Count; i++)
            if (_effectiveTimes[i] >= t)
            {
                return _sortedEvents[i].SequenceId;
            }

        return _sortedEvents[^1].SequenceId;
    }

    private long SequenceIdAtOrBefore(double t)
    {
        if (_sortedEvents.Count == 0)
        {
            return 0;
        }

        for (var i = _effectiveTimes.Count - 1; i >= 0; i--)
            if (_effectiveTimes[i] <= t)
            {
                return _sortedEvents[i].SequenceId;
            }

        return _sortedEvents[0].SequenceId;
    }

    private int IndexAtOrAfterTime(double t)
    {
        for (var i = 0; i < _effectiveTimes.Count; i++)
            if (_effectiveTimes[i] >= t)
            {
                return i;
            }

        return Math.Max(0, _effectiveTimes.Count - 1);
    }

    private bool IsOnTriangle(double x, double y)
        => y <= MarkerStripHeight + HitArea && Math.Abs(x - PlayheadX) <= TriangleHalfWidth + HitArea;

    private DragTarget HitTest(double x, double y)
    {
        if (y <= MarkerStripHeight + HitArea)
        {
            var dPlay = Math.Abs(x - PlayheadX);
            var dStart = Math.Abs(x - StartDrawX);
            var dEnd = Math.Abs(x - EndDrawX);

            // Nearest handle within reach; the playhead triangle wins ties (it sits on top).
            if (dPlay <= HitArea && dPlay <= dStart && dPlay <= dEnd)
            {
                return DragTarget.Playhead;
            }

            if (dStart <= HitArea && dStart <= dEnd)
            {
                return DragTarget.Start;
            }

            if (dEnd <= HitArea)
            {
                return DragTarget.End;
            }
        }

        return DragTarget.Playhead;
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_sortedEvents.Count == 0)
        {
            return;
        }

        HideTooltip();

        var position = e.GetCurrentPoint(_overlay).Position;

        var now = Environment.TickCount64;
        var isDoubleClick = now - _lastPressTicks <= DoubleClickMs && Math.Abs(position.X - _lastPressX) <= HitArea;
        _lastPressTicks = now;
        _lastPressX = position.X;

        if (isDoubleClick && IsOnTriangle(position.X, position.Y))
        {
            // Collapse the selection back onto the playhead (= select all).
            CollapseSelectionToPlayhead();
            _skCanvas.Invalidate();
            return;
        }

        // Scrubbing and the range handles live in the ruler strip only. A press over the rows
        // selects the plan operator under it rather than moving the playhead.
        if (position.Y > MarkerStripHeight)
        {
            SelectOperatorAt(position);
            return;
        }

        _overlay.CapturePointer(e.Pointer);

        _dragTarget = HitTest(position.X, position.Y);
        _isDragging = true;

        // Clicking the strip moves the playhead immediately so a plain click scrubs.
        if (_dragTarget == DragTarget.Playhead)
        {
            MovePlayheadToX(position.X);
            _skCanvas.Invalidate();
        }
    }

    private void SelectOperatorAt(Windows.Foundation.Point position)
    {
        if (HitTestEvent(position.X, position.Y) is ExecutionOperatorEvent { PlanNodeIdentifier: { } node })
        {
            PlanNodeSelected?.Invoke(node);
        }
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging)
        {
            UpdateHoverTooltip(e.GetCurrentPoint(_overlay).Position);
            return;
        }

        var x = Math.Clamp(e.GetCurrentPoint(_overlay).Position.X, RowLabelWidth, CanvasWidth);
        var t = XToTime(x);

        switch (_dragTarget)
        {
            case DragTarget.Start:
                _startTime = Math.Min(t, _endTime);
                EmitScope();
                break;

            case DragTarget.End:
                _endTime = Math.Max(t, _startTime);
                EmitScope();
                break;

            case DragTarget.Playhead:
                MovePlayheadToX(x);
                break;
        }

        _skCanvas.Invalidate();
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _overlay.ReleasePointerCaptures();
        _isDragging = false;
        _dragTarget = DragTarget.None;
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e) => HideTooltip();

    private void HideTooltip()
    {
        _toolTip.IsOpen = false;
        _hoverEvent = null;
    }

    /// <summary>Shows a pointer-following tooltip with the name of the event under the pointer.</summary>
    private void UpdateHoverTooltip(Windows.Foundation.Point position)
    {
        var ev = HitTestEvent(position.X, position.Y);

        if (ev is null)
        {
            HideTooltip();
            return;
        }

        if (!ReferenceEquals(ev, _hoverEvent))
        {
            _hoverEvent = ev;
            _toolTipText.Text = ev.Description;
        }

        _toolTip.HorizontalOffset = position.X + 12;
        _toolTip.VerticalOffset = position.Y + 12;
        _toolTip.IsOpen = true;
    }

    private EngineEvent? HitTestEvent(double x, double y)
    {
        // Operators are added after markers, so iterate in reverse to prefer them on overlap.
        for (var i = _hitRegions.Count - 1; i >= 0; i--)
        {
            if (_hitRegions[i].Bounds.Contains((float)x, (float)y))
            {
                return _hitRegions[i].Event;
            }
        }

        return null;
    }

    private void OnOverlaySizeChanged(object sender, SizeChangedEventArgs e)
    {
        ClampScroll();
        UpdateScrollBar();
        _skCanvas.Invalidate();
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (_sortedEvents.Count == 0)
        {
            return;
        }

        var point = e.GetCurrentPoint(_overlay);
        var delta = point.Properties.MouseWheelDelta;
        if (delta == 0)
        {
            return;
        }

        e.Handled = true;

        var cursorX = point.Position.X;
        var timeAtCursor = XToTime(cursorX);

        var newZoom = Math.Clamp(delta > 0 ? _zoom * ZoomStep : _zoom / ZoomStep, MinZoom, MaxZoom);
        if (Math.Abs(newZoom - _zoom) < 1e-9)
        {
            return;
        }

        _zoom = newZoom;

        // Keep the time under the cursor pinned as the axis stretches.
        _scrollX = RowLabelWidth + (timeAtCursor - _minTime) / _timeRange * ContentWidth - cursorX;
        ClampScroll();
        UpdateScrollBar();

        _skCanvas.Invalidate();
    }

    private void OnScrollBarScroll(object sender, ScrollEventArgs e)
    {
        _scrollX = e.NewValue;
        ClampScroll();
        _skCanvas.Invalidate();
    }

    private void ClampScroll() => _scrollX = Math.Clamp(_scrollX, 0, MaxScroll);

    private void UpdateScrollBar()
    {
        if (_zoom <= MinZoom + 1e-9 || MaxScroll <= 0 || DrawWidth <= 0)
        {
            _scrollBar.Visibility = Visibility.Collapsed;
            _scrollX = 0;
            return;
        }

        _scrollBar.Visibility = Visibility.Visible;
        _scrollBar.Maximum = MaxScroll;
        _scrollBar.ViewportSize = DrawWidth;
        _scrollBar.LargeChange = DrawWidth * 0.9;
        _scrollBar.SmallChange = DrawWidth * 0.1;
        _scrollBar.Value = _scrollX;
    }

    private void MovePlayheadToX(double x)
    {
        // When the collapsed selection sits on the playhead, the handles travel with it so the
        // "select all" cluster stays under the triangle while scrubbing.
        var glued = !SelectionActive
                    && _playheadIndex < _effectiveTimes.Count
                    && Math.Abs(_startTime - _effectiveTimes[_playheadIndex]) < 1e-6;

        _playheadIndex = XToEventIndex(x);
        _playIndex = _playheadIndex;

        if (glued && _playheadIndex < _effectiveTimes.Count)
        {
            _startTime = _effectiveTimes[_playheadIndex];
            _endTime = _startTime;
        }

        FirePlayhead();
    }

    private void CollapseSelectionToPlayhead()
    {
        var time = _playheadIndex < _effectiveTimes.Count ? _effectiveTimes[_playheadIndex] : _minTime;
        _startTime = time;
        _endTime = time;
        EmitScope();
    }

    private void OnPlayButtonClick(object sender, RoutedEventArgs e)
    {
        if (_isPlaying)
        {
            StopPlay();
        }
        else
        {
            StartPlay();
        }
    }

    private void StartPlay()
    {
        if (_sortedEvents.Count == 0)
        {
            return;
        }

        var rangeStart = SelectionActive ? Math.Min(_startTime, _endTime) : _minTime;

        _playIndex = IndexAtOrAfterTime(rangeStart);
        _playheadIndex = _playIndex;
        _isPlaying = true;

        FirePlayhead();

        SetPlayButtonIcon(isPlaying: true);
        _playTimer.Start();

        PlayStateChanged?.Invoke(true);

        _skCanvas.Invalidate();
    }

    private void StopPlay()
    {
        var wasPlaying = _isPlaying;

        _playTimer.Stop();
        _isPlaying = false;
        SetPlayButtonIcon(isPlaying: false);

        if (wasPlaying)
        {
            PlayStateChanged?.Invoke(false);
        }
    }

    private void SetPlayButtonIcon(bool isPlaying)
    {
        if (_playButton.Content is FontIcon icon)
        {
            icon.Glyph = isPlaying ? "" : "";
        }
    }

    private void OnPlayTimerTick(object? sender, object e)
    {
        var rangeEnd = SelectionActive ? Math.Max(_startTime, _endTime) : _maxTime;

        if (_playIndex >= _sortedEvents.Count || _effectiveTimes[_playIndex] > rangeEnd)
        {
            StopPlay();
            return;
        }

        _playheadIndex = _playIndex;

        FirePlayhead();

        _skCanvas.Invalidate();

        _playIndex++;
    }
}
