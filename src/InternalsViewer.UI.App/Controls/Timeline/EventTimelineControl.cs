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
    private const float RulerBandHeight = 18f;
    private const float HandleBandHeight = 16f;
    private const float MarkerStripHeight = RulerBandHeight + HandleBandHeight;
    private const float HandleWidth = 7f;
    private const float HandleGap = 13f;
    private const float TriangleHalfWidth = 9f;
    private const double HitArea = 7;
    private const long DoubleClickMs = 300;
    private const float RowLabelWidth = 36f;
    private const float RowPadding = 2f;
    private const float MarkerWidth = 1f;

    private const double MinZoom = 1.0;
    private const double MaxZoom = 50.0;
    private const double ZoomStep = 1.15;

    private const double AxisUnitsPerMs = 1.0;

    // Small tick for smooth motion; the overall speed is one EventWallMs per in-range event.
    private const double PlayTickMs = 16;
    private const double EventWallMs = 80;

    // Gaps that would take longer than this many ticks to glide across are skipped: play snaps
    // the playhead straight to the next event instead of crawling through dead air.
    private const double GapSkipTicks = 8;

    // Playback speed multipliers cycled by the speed toggle button.
    private static readonly double[] PlaySpeeds = [0.5, 1.0, 2.0, 4.0];
    private static readonly TimeSpan PlayInterval = TimeSpan.FromMilliseconds(PlayTickMs);

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
    private readonly Button _speedButton;
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
    private List<double> _times = [];

    // Render-only time offset (ms) that fans point events sharing a timestamp evenly across the gap
    // to the next timestamp, so the capture's coarse time resolution doesn't stack them - while
    // staying within the bucket (and so within the parent operator).
    private double[] _nudge = [];

    private double _minTime;
    private double _maxTime;
    private double _timeRange;

    private double _playheadTime;
    private double _playStartTime;
    private double _playEndTime;
    private double _playStep;
    private double _basePlayStep;
    private int _speedIndex = 1;
    private bool _isPlaying;

    // True once the user has dragged a handle. While false the start/end handles track the playhead.
    private bool _selectionActivated;
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
        ctrl.BuildTimes();

        ctrl.StopPlay();
        ctrl.Reset();

        ctrl._skCanvas.Invalidate();
    }

    /// <summary>
    /// Optional crop (milliseconds): when set the timeline axis spans only [<see cref="StartTime"/>,
    /// <see cref="EndTime"/>], hiding activity before/after the cropped window. <c>null</c> means
    /// "no crop" and the axis uses the full event range.
    /// </summary>
    public double? StartTime
    {
        get => (double?)GetValue(StartTimeProperty);
        set => SetValue(StartTimeProperty, value);
    }

    public static readonly DependencyProperty StartTimeProperty =
        DependencyProperty.Register(nameof(StartTime), typeof(double?), typeof(EventTimelineControl),
            new PropertyMetadata(null, OnCropChanged));

    public double? EndTime
    {
        get => (double?)GetValue(EndTimeProperty);
        set => SetValue(EndTimeProperty, value);
    }

    public static readonly DependencyProperty EndTimeProperty =
        DependencyProperty.Register(nameof(EndTime), typeof(double?), typeof(EventTimelineControl),
            new PropertyMetadata(null, OnCropChanged));

    private static void OnCropChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (EventTimelineControl)d;

        ctrl.BuildTimes();

        if (ctrl._sortedEvents.Count > 0)
        {
            // Pull the playhead (and any non-activated handles) back into the cropped range, then
            // re-emit so the scope/active operator the other views show follows the clamped position.
            var clamped = Math.Clamp(ctrl._playheadTime, ctrl._minTime, ctrl._maxTime);

            if (clamped != ctrl._playheadTime)
            {
                ctrl._playheadTime = clamped;
                ctrl.SyncHandlesToPlayhead();
                ctrl.FirePlayhead();
            }
        }

        ctrl.UpdateScrollBar();
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

        // Skip-to-previous / skip-to-next glyphs (Segoe Fluent Icons).
        var stepBackButton = MakeTransportButton(new FontIcon { Glyph = "", FontSize = 12 }, 30);
        stepBackButton.Click += OnStepBackButtonClick;

        var stepForwardButton = MakeTransportButton(new FontIcon { Glyph = "", FontSize = 12 }, 30);
        stepForwardButton.Click += OnStepForwardButtonClick;

        _speedButton = MakeTransportButton(
            new TextBlock { FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center }, 36);
        _speedButton.Click += OnSpeedButtonClick;
        UpdateSpeedLabel();

        var transport = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Stretch };
        transport.Children.Add(stepBackButton);
        transport.Children.Add(_playButton);
        transport.Children.Add(stepForwardButton);
        transport.Children.Add(_speedButton);

        Grid.SetColumn(transport, 0);
        Grid.SetRowSpan(transport, 2);
        Children.Add(transport);

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

    private static Button MakeTransportButton(FrameworkElement content, double width) => new()
    {
        Content = content,
        Width = width,
        VerticalAlignment = VerticalAlignment.Stretch,
        Padding = new Thickness(0),
        Background = new SolidColorBrush(Color.FromArgb(0, 30, 30, 30)),
        BorderThickness = new Thickness(0),
        CornerRadius = new CornerRadius(0),
    };

    private double SpeedMultiplier => PlaySpeeds[_speedIndex];

    private void UpdateSpeedLabel()
    {
        if (_speedButton.Content is TextBlock label)
        {
            var speed = SpeedMultiplier;
            label.Text = (speed % 1 == 0 ? speed.ToString("0") : speed.ToString("0.#")) + "x";
        }
    }

    private void OnSpeedButtonClick(object sender, RoutedEventArgs e)
    {
        _speedIndex = (_speedIndex + 1) % PlaySpeeds.Length;
        UpdateSpeedLabel();

        // Re-scale the in-flight step so a speed change takes effect immediately while playing.
        if (_isPlaying)
        {
            _playStep = _basePlayStep * SpeedMultiplier;
        }
    }

    private void OnStepBackButtonClick(object sender, RoutedEventArgs e) => StepToAdjacentEvent(forward: false);

    private void OnStepForwardButtonClick(object sender, RoutedEventArgs e) => StepToAdjacentEvent(forward: true);

    /// <summary>Pauses play and moves the playhead to the previous/next distinct event time.</summary>
    private void StepToAdjacentEvent(bool forward)
    {
        if (_sortedEvents.Count == 0)
        {
            return;
        }

        StopPlay();

        var target = forward
            ? NextEventTime(_playheadTime, _maxTime)
            : PrevEventTime(_playheadTime, _minTime);

        if (target is not { } time)
        {
            return;
        }

        _playheadTime = Math.Clamp(time, _minTime, _maxTime);
        SyncHandlesToPlayhead();
        FirePlayhead();
        EnsurePlayheadVisible();
        _skCanvas.Invalidate();
    }

    /// <summary>When zoomed in, scrolls so the playhead stays within the visible content window.</summary>
    private void EnsurePlayheadVisible()
    {
        if (MaxScroll <= 0)
        {
            return;
        }

        var contentX = (_playheadTime - _minTime) / _timeRange * ContentWidth;
        const double margin = 24;

        if (contentX < _scrollX + margin)
        {
            _scrollX = Math.Clamp(contentX - margin, 0, MaxScroll);
        }
        else if (contentX > _scrollX + DrawWidth - margin)
        {
            _scrollX = Math.Clamp(contentX - DrawWidth + margin, 0, MaxScroll);
        }

        _scrollBar.Value = _scrollX;
    }

    private void Reset()
    {
        _zoom = MinZoom;
        _scrollX = 0;

        // Park the playhead at the end with no selection: everything is in scope and the start/end
        // handles cluster on the playhead at the right edge.
        _selectionActivated = false;
        _playheadTime = _maxTime;
        _startTime = _playheadTime;
        _endTime = _playheadTime;

        CurrentSequenceFrom = 0;
        CurrentSequenceTo = SequenceAtTime(_playheadTime);
        CurrentPlayhead = CurrentSequenceTo;

        UpdateScrollBar();
    }

    /// <summary>
    /// Positions are simply each event's start time in milliseconds (sequence id is only used for
    /// ordering). The axis spans the first start to the last end, where an operator's end is its
    /// start plus its duration.
    /// </summary>
    private void BuildTimes()
    {
        _times = new List<double>(_sortedEvents.Count);
        _nudge = new double[_sortedEvents.Count];

        if (_sortedEvents.Count == 0)
        {
            _minTime = 0;
            _maxTime = 1;
            _timeRange = 1;
            return;
        }

        var min = double.MaxValue;
        var max = double.MinValue;

        // Group size per (timestamp, visual lane) - needed up front to space the fan-out evenly.
        var groupSizes = new Dictionary<(long Time, int Lane), int>();

        // The end of each operator's bar; a point event's fan must never cross its operator's end.
        var operatorEnds = new Dictionary<PlanNodeIdentifier, double>();

        for (var i = 0; i < _sortedEvents.Count; i++)
        {
            var ev = _sortedEvents[i];
            var start = StartMs(ev);
            _times.Add(start);

            if (start < min) min = start;

            if (ev is ExecutionOperatorEvent)
            {
                // Operators occupy [start, start + duration].
                var operatorEnd = start + DurationMs(ev);
                if (operatorEnd > max) max = operatorEnd;
                if (ev.PlanNodeIdentifier is { } opId) operatorEnds[opId] = operatorEnd;
            }
            else
            {
                if (start > max) max = start;
                var key = (ev.TimeMs, RenderLane(ev));
                groupSizes[key] = groupSizes.TryGetValue(key, out var c) ? c + 1 : 1;
            }
        }

        // Apply the optional crop: a set Start/EndTime overrides the natural event extent so that
        // pre/post activity outside the cropped window falls off the axis (clipped by the canvas).
        _minTime = StartTime ?? min;
        _maxTime = EndTime ?? max;
        _timeRange = Math.Max(_maxTime - _minTime, 1.0);

        // The bucket width: the gap to the next distinct timestamp (the capture's time resolution).
        var bucket = NextTimestampGaps();

        // Fan each group evenly across its bucket, in sequence-id order, clamped so it never crosses
        // the parent operator's end.
        var placed = new Dictionary<(long Time, int Lane), int>();

        for (var i = 0; i < _sortedEvents.Count; i++)
        {
            var ev = _sortedEvents[i];
            if (ev is ExecutionOperatorEvent)
            {
                continue;
            }

            var key = (ev.TimeMs, RenderLane(ev));
            var size = groupSizes[key];

            placed.TryGetValue(key, out var index);
            placed[key] = index + 1;

            var t = _times[i];

            if (ev.PlanNodeIdentifier is { } id && operatorEnds.TryGetValue(id, out var opEnd))
            {
                // Hard limit: the marker (and its fan) must stay within the operator's bar.
                var room = opEnd - t;
                var fanWidth = Math.Max(0, Math.Min(bucket.GetValueOrDefault(t), room));
                _nudge[i] = Math.Min(size > 1 ? fanWidth * index / size : 0, room);
            }
            else if (size > 1)
            {
                _nudge[i] = bucket.GetValueOrDefault(t) * index / size;
            }
        }
    }

    /// <summary>Maps each distinct timestamp to the gap to the next one (the bucket width).</summary>
    private Dictionary<double, double> NextTimestampGaps()
    {
        var distinct = _times.Distinct().OrderBy(t => t).ToList();
        var gaps = new Dictionary<double, double>(distinct.Count);

        if (distinct.Count == 0)
        {
            return gaps;
        }

        // Default bucket for the last timestamp: reuse the smallest gap so it fans consistently.
        var smallest = double.MaxValue;
        for (var k = 1; k < distinct.Count; k++)
        {
            var gap = distinct[k] - distinct[k - 1];
            if (gap > 0 && gap < smallest) smallest = gap;
        }
        if (smallest == double.MaxValue) smallest = 1.0;

        for (var k = 0; k < distinct.Count; k++)
        {
            gaps[distinct[k]] = k + 1 < distinct.Count ? distinct[k + 1] - distinct[k] : smallest;
        }

        return gaps;
    }

    // EngineEvent.TimeMs / Duration are real milliseconds (AxisUnitsPerMs is 1).
    private static double StartMs(EngineEvent ev) => ev.TimeMs / AxisUnitsPerMs;

    private static double DurationMs(EngineEvent ev) => ev.Duration / AxisUnitsPerMs;

    // A point event's visual lane: its row, sub-divided by category band for locks/waits, so only
    // markers that truly overlap (same row + band) are fanned out.
    private static int RenderLane(EngineEvent ev)
    {
        var category = EventCategoryClassifier.GetCategory(ev);
        return GetRowIndex(ev) * 8 + (category.HasValue ? (int)category.Value : 7);
    }

    // The selection only counts once the user has explicitly dragged a handle.
    private bool SelectionActive => _selectionActivated;

    // While not activated the handles sit on the playhead, so they follow it as it scrubs/plays.
    private void SyncHandlesToPlayhead()
    {
        if (!_selectionActivated)
        {
            _startTime = _playheadTime;
            _endTime = _playheadTime;
        }
    }

    /// <summary>Sequence id of the most recent event at or before the given time.</summary>
    private long SequenceAtTime(double timeMs)
    {
        if (_sortedEvents.Count == 0)
        {
            return 0;
        }

        var seq = _sortedEvents[0].SequenceId;
        for (var i = 0; i < _times.Count; i++)
            if (_times[i] <= timeMs)
            {
                seq = _sortedEvents[i].SequenceId;
            }

        return seq;
    }

    /// <summary>Earliest event time strictly after <paramref name="timeMs"/> up to <paramref name="upper"/>, or null.</summary>
    private double? NextEventTime(double timeMs, double upper)
    {
        double? next = null;

        for (var i = 0; i < _times.Count; i++)
        {
            var t = _times[i];

            if (t > timeMs && t <= upper && (next is null || t < next))
            {
                next = t;
            }
        }

        return next;
    }

    /// <summary>Latest event time strictly before <paramref name="timeMs"/> down to <paramref name="lower"/>, or null.</summary>
    private double? PrevEventTime(double timeMs, double lower)
    {
        double? prev = null;

        for (var i = 0; i < _times.Count; i++)
        {
            var t = _times[i];

            if (t < timeMs && t >= lower && (prev is null || t > prev))
            {
                prev = t;
            }
        }

        return prev;
    }

    private float CanvasWidth => (float)_overlay.ActualWidth;
    private float DrawWidth => CanvasWidth - RowLabelWidth;
    private double ContentWidth => DrawWidth * _zoom;
    private double MaxScroll => Math.Max(0, ContentWidth - DrawWidth);

    private float TimeToX(double effectiveTimeMs)
        => RowLabelWidth + (float)((effectiveTimeMs - _minTime) / _timeRange * ContentWidth - _scrollX);

    private double XToTime(double x)
        => _minTime + (Math.Max(0, x - RowLabelWidth) + _scrollX) / ContentWidth * _timeRange;

    private float PlayheadX => TimeToX(_playheadTime);

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

            // Fan same-timestamp markers apart within their bucket (render only; the playhead and
            // selection still use the true time). The offset is in ms, so it scales with zoom.
            var x = TimeToX(_times[i] + _nudge[i]);
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
            var x = TimeToX(_minTime + tickMs);

            canvas.DrawLine(x, RulerBandHeight - 4, x, RulerBandHeight, _tickPaint);
            canvas.DrawText(FormatTime(tickMs), x + 2, RulerBandHeight - 6, SKTextAlign.Left, _labelFont, _labelPaint);
        }
    }

    /// <summary>Draws a red badge showing the playhead's time, on top of the ruler.</summary>
    private void DrawPlayheadTimeBadge(SKCanvas canvas, float px)
    {
        var text = FormatTime(EffectiveToMs(_playheadTime));

        const float padding = 4f;
        var badgeWidth = _labelFont.MeasureText(text) + padding * 2;
        var badgeHeight = RulerBandHeight - 2;
        var bx = Math.Clamp(px - badgeWidth / 2f, RowLabelWidth, Math.Max(RowLabelWidth, CanvasWidth - badgeWidth));

        canvas.DrawRoundRect(new SKRect(bx, 0, bx + badgeWidth, badgeHeight), 2, 2, _playheadFill);

        _operatorTextPaint.Color = SKColors.White;
        var baseline = badgeHeight / 2f + _labelFont.Size * 0.35f;
        canvas.DrawText(text, bx + padding, baseline, SKTextAlign.Left, _labelFont, _operatorTextPaint);
    }

    // The axis is already in milliseconds; this just makes it relative to the first event.
    private double EffectiveToMs(double effective) => effective - _minTime;

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
                .OrderBy(o => _times[o.Index])
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
            var startX = TimeToX(_times[index]);
            var endX = TimeToX(_times[index] + DurationMs(op));
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

            var barTop = y - lineWidth / 2f;
            var barBottom = y + lineWidth / 2f;

            // Subtle top-lit sheen: lighten the top edge, darken the bottom edge of the bar.
            var gradient = SKShader.CreateLinearGradient(
                new SKPoint(startX, barTop),
                new SKPoint(startX, barBottom),
                [Scale(barColour, 1f + GradientLift), Scale(barColour, 1f - GradientLift)],
                null,
                SKShaderTileMode.Clamp);

            _operatorPaint.Color = barColour;
            _operatorPaint.Shader = gradient;
            canvas.DrawRoundRect(new SKRect(startX, barTop, endX, barBottom),
                                 cornerRadius, cornerRadius, _operatorPaint);
            _operatorPaint.Shader = null;
            gradient.Dispose();

            if (lineWidth >= MinLabelBarHeight && endX - startX >= MinLabelBarWidth)
            {
                DrawOperatorLabel(canvas, op, startX, endX, y, lineWidth, barColour);
            }

            _hitRegions.Add((new SKRect(startX, y - slotHeight / 2f, endX, y + slotHeight / 2f), op));
        }
    }

    // The statement (SELECT) band is half the height of an operator level band.
    private const float StatementBandWeight = 0.5f;

    // RGB lift/drop for the subtle vertical sheen on operator bars (±14%).
    private const float GradientLift = 0.08f;

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

        // Hide the label entirely when it would overflow the bar rather than clipping it mid-word.
        const float textPadX = 4f;
        if (_labelFont.MeasureText(label) > endX - startX - textPadX * 2)
        {
            return;
        }

        _operatorTextPaint.Color = ContrastingColour(barColour);

        // Centre the text vertically within the bar.
        var baseline = y + _labelFont.Size * 0.35f;
        canvas.DrawText(label, startX + textPadX, baseline, SKTextAlign.Left, _labelFont, _operatorTextPaint);
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

    private static SKColor TintByCategory(SKColor colour, int category) => Scale(colour, CategoryShade[category]);

    /// <summary>Scales a colour's RGB channels by <paramref name="factor"/> (clamped), preserving alpha.</summary>
    private static SKColor Scale(SKColor colour, float factor) => new(
        (byte)Math.Clamp(colour.Red * factor, 0, 255),
        (byte)Math.Clamp(colour.Green * factor, 0, 255),
        (byte)Math.Clamp(colour.Blue * factor, 0, 255),
        colour.Alpha);

    /// <summary>
    /// Emits the in-scope window the other views highlight. With an explicit selection that is the
    /// selected range; otherwise it runs from the start up to the playhead, so the playhead drives
    /// the rest of the views as it is scrubbed or played.
    /// </summary>
    private void EmitScope()
    {
        long from, to;

        if (SelectionActive)
        {
            from = SequenceIdAtOrAfter(Math.Min(_startTime, _endTime));
            to = SequenceIdAtOrBefore(Math.Max(_startTime, _endTime));
        }
        else
        {
            from = 0;
            to = SequenceAtTime(_playheadTime);
        }

        // Smooth play ticks many times per event; only notify when the scope actually changes.
        if (from == CurrentSequenceFrom && to == CurrentSequenceTo)
        {
            return;
        }

        CurrentSequenceFrom = from;
        CurrentSequenceTo = to;
        SequenceChanged?.Invoke(from, to);
    }

    private void FirePlayhead()
    {
        var playhead = SequenceAtTime(_playheadTime);

        if (playhead != CurrentPlayhead)
        {
            CurrentPlayhead = playhead;
            PlayheadChanged?.Invoke(CurrentPlayhead);
        }

        // The scope's right edge tracks the playhead when there's no explicit selection.
        EmitScope();
    }

    private long SequenceIdAtOrAfter(double t)
    {
        if (_sortedEvents.Count == 0)
        {
            return 0;
        }

        for (var i = 0; i < _times.Count; i++)
            if (_times[i] >= t)
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

        for (var i = _times.Count - 1; i >= 0; i--)
            if (_times[i] <= t)
            {
                return _sortedEvents[i].SequenceId;
            }

        return _sortedEvents[0].SequenceId;
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

            // The triangle sits on top and is wider, so a press anywhere on it grabs the playhead.
            if (dPlay <= TriangleHalfWidth)
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
            // Reset: drop the selection so the handles re-attach to the playhead (= select all).
            DeactivateSelection();
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
                _selectionActivated = true;
                _startTime = Math.Min(t, _endTime);
                EmitScope();
                break;

            case DragTarget.End:
                _selectionActivated = true;
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
        _playheadTime = Math.Clamp(XToTime(x), _minTime, _maxTime);
        SyncHandlesToPlayhead();
        FirePlayhead();
    }

    /// <summary>Double-click reset: deactivate the selection and snap the handles to the playhead.</summary>
    private void DeactivateSelection()
    {
        _selectionActivated = false;
        SyncHandlesToPlayhead();
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
        var rangeEnd = SelectionActive ? Math.Max(_startTime, _endTime) : _maxTime;

        _playStartTime = rangeStart;
        _playEndTime = rangeEnd;

        // Resume from the current playhead position; only snap to the start if it sits outside the
        // range (e.g. parked at the end after a previous run). Playback loops back here at the end.
        if (_playheadTime < rangeStart || _playheadTime >= rangeEnd)
        {
            _playheadTime = rangeStart;
        }

        _isPlaying = true;

        // Advance the playhead smoothly: cover the range over (events in range) * EventWallMs of wall
        // time, so the overall speed matches the old per-event play but in small, even time steps.
        var rangeMs = Math.Max(rangeEnd - rangeStart, 1e-6);
        var eventsInRange = Math.Max(1, _times.Count(t => t >= rangeStart && t <= rangeEnd));
        _basePlayStep = rangeMs * PlayTickMs / (eventsInRange * EventWallMs);
        _playStep = _basePlayStep * SpeedMultiplier;

        SyncHandlesToPlayhead();
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
        _playheadTime += _playStep;

        // Loop continuously: when the playhead reaches the end of the range it wraps back to the
        // start and keeps playing until the user presses stop.
        if (_playheadTime >= _playEndTime)
        {
            _playheadTime = _playStartTime;
        }
        else if (NextEventTime(_playheadTime, _playEndTime) is { } next && next - _playheadTime > _basePlayStep * GapSkipTicks)
        {
            // Dead air ahead: jump straight to the next activity rather than gliding across the gap.
            // The gap threshold uses the base step so skipping behaves the same at every speed.
            _playheadTime = next;
        }

        SyncHandlesToPlayhead();
        FirePlayhead();
        _skCanvas.Invalidate();
    }
}
