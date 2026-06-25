using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Query.Plans;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI;
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

    // Rows with few events use a wider marker so the sparse ticks are easier to see.
    private const float SparseMarkerWidth = 4f;
    private const int SparseRowThreshold = 25;

    private const double TransportButtonHeight = 26;

    // Opacity of the I/O trace extensions (Trace I/O mode) — faint so they read as a background hint.
    private const byte TraceAlpha = 90;

    private const double MinZoom = 1.0;
    private const double MaxZoom = 100.0;
    private const double ZoomStep = 1.15;

    private const double AxisUnitsPerMs = 1.0;

    // Timer tick for smooth motion. Play sweeps the whole range left-to-right over a fixed wall-clock
    // duration (BasePlayDurationMs at 1x), regardless of how many events or how short the range is.
    private const double PlayTickMs = 16;
    private const double BasePlayDurationMs = 10_000;

    // Dead-air gaps wider than this many times the average event spacing are skipped so the sweep
    // doesn't crawl through empty time. Based on spacing (not the step) so it never skips normal gaps.
    private const double GapSkipFactor = 6;

    // Playback speed multipliers cycled by the speed button (1x = BasePlayDurationMs, 2x = half, etc.).
    private static readonly double[] PlaySpeeds = [0.5, 1.0, 2.0, 4.0];
    private static readonly TimeSpan PlayInterval = TimeSpan.FromMilliseconds(PlayTickMs);

    private static readonly SKColor PlayheadColour = new(230, 60, 60);
    private static readonly SKColor HandleColour = new(95, 95, 95);
    private static readonly SKColor StatementColour = new(130, 130, 130);

    // Operator bar labels: a soft off-white that reads on every category colour.
    private static readonly SKColor OperatorLabelColour = new(235, 235, 235);

    // Per-category brightness applied to the row colour so each category band reads slightly differently.
    private static readonly float[] CategoryShade = [0.70f, 0.85f, 1.0f, 1.15f];

    // Operator events span a duration and are drawn as lines; the row is given extra weight so the
    // per-level tracks have room. The Log row is only shown when there are transaction-log events
    // (see _activeRows); the rest are always present.
    private static readonly (Type EventType, string Label, SKColor Color, float Weight)[] AllRows =
    [
        (typeof(TransactionLogEvent), "Log",  ColourConstants.LogColour.ToSkColor().WithAlpha(255),  0.5f),
        (typeof(ExecutionOperatorEvent), "Plan", SKColors.LimeGreen, 3f),
        (typeof(IoEvent),   "Read", ColourConstants.IoColour.ToSkColor().WithAlpha(255),   0.5f),
        (typeof(LockEvent), "Lock", ColourConstants.LockColour.ToSkColor().WithAlpha(255), 0.5f),
        (typeof(WaitEvent), "Wait", ColourConstants.WaitColour.ToSkColor().WithAlpha(255), 0.5f),
    ];

    // The rows actually rendered: AllRows minus the Log lane when there are no log events.
    private (Type EventType, string Label, SKColor Color, float Weight)[] _activeRows = AllRows;

    // Event count per active row, used to widen markers on sparse rows.
    private int[] _rowEventCounts = [];

    private readonly Button _playButton;
    private readonly Button _speedButton;
    private readonly ToggleButton _traceIoButton;
    private bool _traceIo;
    private readonly SKXamlCanvas _skCanvas;
    private readonly Canvas _overlay;
    private readonly ScrollBar _scrollBar;
    private readonly Popup _toolTip;
    private readonly TextBlock _toolTipText;

    private readonly List<(SKRect Bounds, EngineEvent Event, string? Label)> _hitRegions = [];
    private EngineEvent? _hoverEvent;
    private string? _hoverLabel;

    private readonly SKFont _labelFont = new(SKTypeface.Default, 10f);

    // Operator bar labels get their own font so the size can be scaled per bar (up to OperatorMaxFont).
    // The type (e.g. "Index Scan") is drawn bold, the object name that follows in the regular font.
    private readonly SKFont _operatorFont = new(SKTypeface.Default, 10f);
    private readonly SKFont _operatorBoldFont = new(SKTypeface.FromFamilyName(SKTypeface.Default.FamilyName, SKFontStyle.Bold), 10f);

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

    // Extra per-block padding added in Trace mode so stacked bars leave a gap for the trace lines.
    private const float TraceStackGap = 12f;

    // Buffer-category operators (spool/sort/exchange) are drawn as a thin collapsed bar.
    private const float BufferHeightScale = 0.3f;

    // Operator labels are only drawn when the bar is tall and wide enough to be legible.
    private const float MinLabelBarHeight = 11f;
    private const float MinLabelBarWidth = 26f;

    // Operator labels scale up to this size when the bar has room, and are hidden below the minimum.
    private const float OperatorMaxFont = 11f;
    private const float OperatorMinFont = 7f;

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
    private double _gapThreshold;
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
        ctrl.BuildActiveRows();
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

            if (Math.Abs(clamped - ctrl._playheadTime) > 0.1)
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

    /// <summary>Raised when an individual event marker is clicked (to reveal it in the event grid).</summary>
    public event Action<EngineEvent>? EventSelected;

    /// <summary>Raised when auto-play starts (true) or stops (false).</summary>
    public event Action<bool>? PlayStateChanged;

    public EventTimelineControl()
    {
        Background = new SolidColorBrush(Colors.Transparent);

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                       // transport toolbar
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });   // timeline
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                       // scroll bar

        _playButton = new Button
        {
            Content = new FontIcon { Glyph = "", FontSize = 14 },
            Width = 36,
            Height = TransportButtonHeight,
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

        _traceIoButton = new ToggleButton
        {
            Content = new TextBlock { Text = "Trace", FontSize = 10 },
            Height = TransportButtonHeight,
            //Padding = new Thickness(8, 0, 8, 0),
            Margin = new Thickness(8, 2, 0, 2),
            VerticalAlignment = VerticalAlignment.Center,
            BorderBrush = null,
            Background = new SolidColorBrush(Color.FromArgb(0, 30, 30, 30)),
        };
        _traceIoButton.Checked += OnTraceIoToggled;
        _traceIoButton.Unchecked += OnTraceIoToggled;

        var transport = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        transport.Children.Add(stepBackButton);
        transport.Children.Add(_playButton);
        transport.Children.Add(stepForwardButton);
        transport.Children.Add(_speedButton);
        transport.Children.Add(_traceIoButton);

        Grid.SetRow(transport, 0);
        Children.Add(transport);

        _skCanvas = new SKXamlCanvas { IgnorePixelScaling = true };
        _skCanvas.PaintSurface += OnPaintSurface;
        Grid.SetRow(_skCanvas, 1);
        Children.Add(_skCanvas);

        _overlay = new Canvas { Background = new SolidColorBrush(Colors.Transparent) };
        Grid.SetRow(_overlay, 1);
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
        Grid.SetRow(_scrollBar, 2);
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
        Height = TransportButtonHeight,
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

            switch (speed)
            {
                case 0.001:
                    label.Text = "0.001x";
                    break;
                default:
                    label.Text = (speed % 1 == 0 ? speed.ToString("0") : speed.ToString("0.#")) + "x";
                    break;
            }
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

    private void OnTraceIoToggled(object sender, RoutedEventArgs e)
    {
        _traceIo = _traceIoButton.IsChecked == true;
        _skCanvas.Invalidate();
    }

    private void OnStepBackButtonClick(object sender, RoutedEventArgs e) => StepToAdjacentEvent(forward: false);

    private void OnStepForwardButtonClick(object sender, RoutedEventArgs e) => StepToAdjacentEvent(forward: true);

    /// <summary>Pauses play and moves the playhead to the previous/next event by sequence id.</summary>
    private void StepToAdjacentEvent(bool forward)
    {
        if (_sortedEvents.Count == 0)
        {
            return;
        }

        StopPlay();

        var current = IndexAtTime(_playheadTime);

        int target;
        if (forward)
        {
            target = current + 1;
        }
        else
        {
            // If the playhead sits in a gap past the current event, step back lands on that event;
            // otherwise it moves to the preceding one.
            target = current >= 0 && _times[current] < _playheadTime ? current : current - 1;
        }

        target = Math.Clamp(target, 0, _sortedEvents.Count - 1);

        _playheadTime = Math.Clamp(_times[target], _minTime, _maxTime);
        SyncHandlesToPlayhead();
        FirePlayhead();
        EnsurePlayheadVisible();
        _skCanvas.Invalidate();
    }

    /// <summary>Index (in sequence-id order) of the latest event at or before the given time, or -1.</summary>
    private int IndexAtTime(double timeMs)
    {
        var index = -1;

        for (var i = 0; i < _times.Count; i++)
        {
            if (_times[i] <= timeMs)
            {
                index = i;
            }
        }

        return index;
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

        // Park the playhead at the start with no selection: the start/end handles cluster on the
        // playhead at the left edge, ready to play forward from the beginning.
        _selectionActivated = false;
        _playheadTime = _minTime;
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

            if (start < min)
            {
                min = start;
            }

            if (ev is ExecutionOperatorEvent)
            {
                // Operators occupy [start, start + duration].
                var operatorEnd = start + DurationMs(ev);

                if (operatorEnd > max)
                {
                    max = operatorEnd;
                }

                if (ev.PlanNodeIdentifier is { } opId)
                {
                    operatorEnds[opId] = operatorEnd;
                }
            }
            else
            {
                if (start > max)
                {
                    max = start;
                }

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
            if (gap > 0 && gap < smallest)
            {
                smallest = gap;
            }
        }
        if (smallest == double.MaxValue)
        {
            smallest = 1.0;
        }

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
    private int RenderLane(EngineEvent ev)
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

        if (w <= 0 || h <= 0)
        {
            return;
        }

        var rowsTop = MarkerStripHeight;
        var rowsHeight = h - rowsTop;
        var rowCount = _activeRows.Length;

        var totalWeight = _activeRows.Sum(r => r.Weight);
        var rowTops = new float[rowCount];
        var rowHeights = new float[rowCount];
        var acc = rowsTop;
        for (var r = 0; r < rowCount; r++)
        {
            rowTops[r] = acc;
            rowHeights[r] = rowsHeight * _activeRows[r].Weight / totalWeight;
            acc += rowHeights[r];
        }

        for (var r = 0; r < rowCount; r++)
        {
            var (_, label, _, _) = _activeRows[r];
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

        _hitRegions.Clear();

        // No events yet — show the empty lane scaffold (rows + labels) rather than a blank control.
        if (_sortedEvents.Count == 0)
        {
            return;
        }

        canvas.Save();
        canvas.ClipRect(new SKRect(RowLabelWidth, 0, w, h));

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

            // Virtualisation: skip markers that fall outside the visible content. Skia would clip the
            // pixels anyway, but this also avoids the per-event work and the hit-region entry, which
            // dominates when zoomed in on a large capture.
            if (x > w || x < RowLabelWidth - SparseMarkerWidth)
            {
                continue;
            }

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
                _markerPaint.Color = TintByCategory(_activeRows[rowIndex].Color, step);
            }
            else
            {
                // The single marker paint is reused; only its (struct) colour changes per event.
                // Fall back to the row colour when the event has no display colour set.
                var displayColour = ev.DisplayColour;
                markerTop = innerTop;
                markerHeight = innerHeight;
                _markerPaint.Color = displayColour.A == 0 ? _activeRows[rowIndex].Color : displayColour.ToSkColor();
            }

            var markerWidth = RowMarkerWidth(rowIndex);
            canvas.DrawRect(x, markerTop, markerWidth, markerHeight, _markerPaint);

            // Widen the hit target a little so the thin markers are easy to hover.
            _hitRegions.Add((new SKRect(x - 3, markerTop, x + markerWidth + 3, markerTop + markerHeight), ev, null));
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

    // A laid-out operator bar; computed first so I/O traces can be drawn behind the bars.
    private readonly record struct OperatorBar(
        ExecutionOperatorEvent Op,
        float StartX, float EndX,
        float BarTop, float BarBottom, float BarCentreY,
        float LineWidth, float CornerRadius,
        float SlotCentreY, float SlotHeight,
        SKColor BarColour);

    /// <summary>
    /// Draws operator events as horizontal lines spanning their duration. Operators are stacked
    /// top-to-bottom ordered by plan level (then start time, then node id); each takes an equal
    /// weighted share of the row height (the statement node a half share), so the hierarchy reads by
    /// depth with no gaps.
    /// </summary>
    private void DrawOperatorLines(SKCanvas canvas, float[] rowTops, float[] rowHeights)
    {
        var planRow = -1;
        for (var r = 0; r < _activeRows.Length; r++)
        {
            if (_activeRows[r].EventType == typeof(ExecutionOperatorEvent)) { planRow = r; break; }
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

        var top = rowTops[planRow] + RowPadding;
        var height = rowHeights[planRow] - RowPadding * 2;

        // Stack the operators top-to-bottom, ordered by plan level (then start time, then node id),
        // each taking a weighted share of the row height. The share is driven by the operator's cost
        // so the timeline reads by where the work is rather than by plan depth, with a minimum share
        // so even the cheapest operator stays legible. The statement (SELECT) node keeps a fixed slim
        // share. Costs are normalised against the most expensive operator and run through a square
        // root, compressing the range so a single dominant operator doesn't crush the rest - the bars
        // indicate relative cost, not exact ratios.
        var ordered = operators
            .OrderBy(o => o.Op.NodeLevel)
            .ThenBy(o => _times[o.Index])
            .ThenBy(o => o.Op.PlanNodeIdentifier?.NodeId ?? 0)
            .ToList();

        var maxCost = operators
            .Where(o => o.Op.NodeLevel > 0)
            .Select(o => o.Op.Cost ?? 0)
            .DefaultIfEmpty(0)
            .Max();

        float CostWeight(ExecutionOperatorEvent op)
        {
            if (op.NodeLevel == 0)
            {
                return StatementBandWeight;
            }

            if (maxCost <= 0)
            {
                // No cost information: fall back to an equal share for every operator.
                return MaxCostWeight;
            }

            var normalised = (float)Math.Sqrt(Math.Clamp((op.Cost ?? 0) / maxCost, 0, 1));
            return MinCostWeight + (MaxCostWeight - MinCostWeight) * normalised;
        }

        var totalWeight = ordered.Sum(o => CostWeight(o.Op));
        var unit = totalWeight > 0 ? height / totalWeight : height;

        var slotByIndex = new Dictionary<int, (float Y, float Height)>(ordered.Count);
        var slotAcc = top;
        foreach (var (index, op) in ordered)
        {
            var slot = CostWeight(op) * unit;
            slotByIndex[index] = (slotAcc + slot / 2f, slot);
            slotAcc += slot;
        }

        var bars = new List<OperatorBar>(operators.Count);

        foreach (var (index, op) in operators)
        {
            var startX = TimeToX(_times[index]);
            var endX = TimeToX(_times[index] + DurationMs(op));
            if (endX < startX + 2)
            {
                endX = startX + 2;
            }

            // Pad the right edge so an I/O event landing on the operator's end time (its marker drawn
            // rightward from endX) still falls within the bar, allowing for the wider sparse-row marker.
            endX += SparseMarkerWidth;

            var level = op.NodeLevel;
            var (y, slotHeight) = slotByIndex[index];

            SKColor barColour;

            if (level == 0)
            {
                // The statement (SELECT) node is a single grey bar (a half-height slot in the stack).
                barColour = StatementColour;
            }
            else
            {
                // Fall back to the row colour when the event has no display colour set.
                var displayColour = op.DisplayColour;
                barColour = displayColour.A == 0 ? _activeRows[planRow].Color : displayColour.ToSkColor();
            }

            // Lay the bar out within the slot. Phased operators reserve a strip above the bar for the
            // build/probe lanes (keeping a gap to the block above); buffer operators collapse to a thin
            // bar; everything else fills the slot less a margin.
            var slotTop = y - slotHeight / 2f;
            var slotBottom = y + slotHeight / 2f;

            // In Trace mode add extra padding so stacked bars leave a gap for the trace lines to show.
            var effectiveMargin = OperatorLineMargin + (_traceIo ? TraceStackGap : 0f);
            var pad = effectiveMargin / 2f;

            var hasPhases = op.BuildPhaseDuration != 0 || op.ProbePhaseDuration != 0;
            var reserveAbove = hasPhases ? PhaseStripReserve : 0f;

            var availTop = slotTop + pad + reserveAbove;
            var availBottom = Math.Max(availTop + 1f, slotBottom - pad);

            float barTop, barBottom;
            if (op.Category == OperatorCategory.Buffer)
            {
                // Collapse buffer operators (spool/sort/exchange) to a thin bar centred in the band.
                var barHeight = Math.Max(1f, (slotHeight - effectiveMargin) * BufferHeightScale);
                var centre = (availTop + availBottom) / 2f;
                barTop = centre - barHeight / 2f;
                barBottom = centre + barHeight / 2f;
            }
            else
            {
                barTop = availTop;
                barBottom = availBottom;
            }

            var lineWidth = Math.Max(1f, barBottom - barTop);
            var barCentreY = (barTop + barBottom) / 2f;
            var cornerRadius = Math.Min(lineWidth / 2f, 3f);

            bars.Add(new OperatorBar(op, startX, endX, barTop, barBottom, barCentreY,
                                     lineWidth, cornerRadius, y, slotHeight, barColour));
        }

        // Trace: draw the extensions first so the operator bars paint over them.
        if (_traceIo)
        {
            DrawTraces(canvas, bars, rowTops, rowHeights);
        }

        var rightEdge = CanvasWidth;

        foreach (var b in bars)
        {
            // Virtualisation: skip operators whose bar is entirely off-screen.
            if (b.EndX < RowLabelWidth || b.StartX > rightEdge)
            {
                continue;
            }

            // Subtle top-lit sheen: lighten the top edge, darken the bottom edge of the bar.
            var gradient = SKShader.CreateLinearGradient(
                new SKPoint(b.StartX, b.BarTop),
                new SKPoint(b.StartX, b.BarBottom),
                [ColourScale(b.BarColour, 1f + GradientLift), ColourScale(b.BarColour, 1f - GradientLift)],
                null,
                SKShaderTileMode.Clamp);

            _operatorPaint.Color = b.BarColour;
            _operatorPaint.Shader = gradient;
            canvas.DrawRoundRect(new SKRect(b.StartX, b.BarTop, b.EndX, b.BarBottom),
                                 b.CornerRadius, b.CornerRadius, _operatorPaint);
            _operatorPaint.Shader = null;
            gradient.Dispose();

            if (b.LineWidth >= MinLabelBarHeight && b.EndX - b.StartX >= MinLabelBarWidth)
            {
                DrawOperatorLabel(canvas, b.Op, b.StartX, b.EndX, b.BarCentreY, b.LineWidth, b.BarColour);
            }

            _hitRegions.Add((new SKRect(b.StartX, b.SlotCentreY - b.SlotHeight / 2f, b.EndX,
                                        b.SlotCentreY + b.SlotHeight / 2f), b.Op, null));

            // Build/probe phase bars sit in the reserved strip above the operator; add them last so
            // they win hit-testing.
            DrawOperatorPhases(canvas, b.Op, b.BarTop, b.BarColour);
        }
    }

    /// <summary>
    /// Trace mode: extends point markers out of their row to the operator that produced them, at
    /// reduced opacity. Reads sit below the plan and extend up to the operator's bottom; log writes
    /// sit above the plan and extend down to the modification operator's top.
    /// </summary>
    private void DrawTraces(SKCanvas canvas, List<OperatorBar> bars, float[] rowTops, float[] rowHeights)
    {
        var byNode = new Dictionary<PlanNodeIdentifier, OperatorBar>(bars.Count);
        foreach (var b in bars)
        {
            if (b.Op.PlanNodeIdentifier is { } id)
            {
                byNode[id] = b;
            }
        }

        var ioRow = RowIndexOf(typeof(IoEvent));
        var logRow = RowIndexOf(typeof(TransactionLogEvent));

        if (ioRow < 0 && logRow < 0)
        {
            return;
        }

        // Composite all extensions through a single layer at reduced opacity so overlapping traces
        // don't stack up to full opacity (the layer merges them first, then fades the whole thing once).
        using var layerPaint = new SKPaint { Color = SKColors.White.WithAlpha(TraceAlpha) };
        canvas.SaveLayer(layerPaint);

        var rightEdge = CanvasWidth;

        if (ioRow >= 0)
        {
            // Reads are below the plan: extend from the operator's bottom down to the read row.
            var ioTop = rowTops[ioRow] + RowPadding;
            var width = RowMarkerWidth(ioRow);

            for (var i = 0; i < _sortedEvents.Count; i++)
            {
                if (_sortedEvents[i] is not IoEvent io ||
                    io.PlanNodeIdentifier is not { } id ||
                    !byNode.TryGetValue(id, out var b) ||
                    b.BarBottom >= ioTop)
                {
                    continue;
                }

                var x = TimeToX(_times[i] + _nudge[i]);
                if (x > rightEdge || x < RowLabelWidth - width)
                {
                    continue;
                }

                _markerPaint.Color = TraceColour(io, ioRow);
                canvas.DrawRect(x, b.BarBottom, width, ioTop - b.BarBottom, _markerPaint);
            }
        }

        if (logRow >= 0)
        {
            // Log writes are above the plan: extend from the log row down to the modification operator's top.
            var logBottom = rowTops[logRow] + rowHeights[logRow] - RowPadding;
            var width = RowMarkerWidth(logRow);

            for (var i = 0; i < _sortedEvents.Count; i++)
            {
                if (_sortedEvents[i] is not TransactionLogEvent log ||
                    log.PlanNodeIdentifier is not { } id ||
                    !byNode.TryGetValue(id, out var b) ||
                    b.BarTop <= logBottom)
                {
                    continue;
                }

                var x = TimeToX(_times[i] + _nudge[i]);
                if (x > rightEdge || x < RowLabelWidth - width)
                {
                    continue;
                }

                _markerPaint.Color = TraceColour(log, logRow);
                canvas.DrawRect(x, logBottom, width, b.BarTop - logBottom, _markerPaint);
            }
        }

        canvas.Restore();
    }

    private SKColor TraceColour(EngineEvent ev, int rowIndex) =>
        (ev.DisplayColour.A == 0 ? _activeRows[rowIndex].Color : ev.DisplayColour.ToSkColor()).WithAlpha(255);

    private int RowIndexOf(Type eventType)
    {
        for (var r = 0; r < _activeRows.Length; r++)
        {
            if (_activeRows[r].EventType == eventType)
            {
                return r;
            }
        }

        return -1;
    }

    /// <summary>
    /// Draws the hash build/probe phases (when present) as thin half-opacity bars on two lanes just
    /// above the operator: build (orange) on the upper lane, probe (operator colour lightened) on the
    /// lower lane. They get their own lanes because the phases can overlap in time.
    /// </summary>
    private void DrawOperatorPhases(SKCanvas canvas, ExecutionOperatorEvent op, float barTop, SKColor barColour)
    {
        if (op.BuildPhaseDuration == 0 && op.ProbePhaseDuration == 0)
        {
            return;
        }

        var probeBottom = barTop - PhaseGapAboveBar;
        var probeTop = probeBottom - PhaseLaneHeight;
        var buildBottom = probeTop;
        var buildTop = buildBottom - PhaseLaneHeight;

        if (op.BuildPhaseDuration != 0)
        {
            DrawPhaseBar(canvas, op, op.BuildPhaseTimeMs, op.BuildPhaseDuration, buildTop, buildBottom,
                         PhaseBuildColour, "Build");
        }

        if (op.ProbePhaseDuration != 0)
        {
            DrawPhaseBar(canvas, op, op.ProbePhaseTimeMs, op.ProbePhaseDuration, probeTop, probeBottom,
                         ColourScale(barColour, PhaseLighten), "Probe");
        }
    }

    private void DrawPhaseBar(SKCanvas canvas, ExecutionOperatorEvent op, long timeMs, long duration,
                              float top, float bottom, SKColor colour, string label)
    {
        var x0 = TimeToX(timeMs / AxisUnitsPerMs);
        var x1 = TimeToX((timeMs + duration) / AxisUnitsPerMs);
        if (x1 < x0 + 1)
        {
            x1 = x0 + 1;
        }

        var rect = new SKRect(x0, top, x1, bottom);

        _markerPaint.Color = colour.WithAlpha(PhaseAlpha);
        canvas.DrawRect(rect, _markerPaint);

        _hitRegions.Add((rect, op, label));
    }

    // The statement (SELECT) band is a fixed slim share, independent of cost.
    private const float StatementBandWeight = 0.5f;

    // Operator slot heights scale with cost between these weights: the cheapest operator gets the
    // minimum share (so it stays legible) and the most expensive gets the maximum.
    private const float MinCostWeight = 0.35f;
    private const float MaxCostWeight = 1.5f;

    // RGB lift/drop for the subtle vertical sheen on operator bars (±14%).
    private const float GradientLift = 0.04f;

    // Build/probe phase bars: thin lanes above the operator at half opacity. Build is a fixed orange;
    // probe is the operator colour lightened.
    private const float PhaseLaneHeight = 3f;
    private const float PhaseGapAboveBar = 1f;

    // Vertical space reserved above a phased operator's bar for the two phase lanes plus their gap.
    private const float PhaseStripReserve = PhaseGapAboveBar + 2 * PhaseLaneHeight;

    private const float PhaseLighten = 1.4f;
    private const byte PhaseAlpha = 128;
    private static readonly SKColor PhaseBuildColour = new(240, 150, 60);

    // Gap between the bold type and the object name, as a fraction of the font size (scales with it).
    private const float OperatorLabelGapFraction = 0.5f;

    /// <summary>
    /// Draws the operator type (bold) and, if present, the object name following it (regular weight),
    /// sized to the largest that fits the bar's height and width.
    /// </summary>
    private void DrawOperatorLabel(SKCanvas canvas, ExecutionOperatorEvent op,
                                   float startX, float endX, float y, float barHeight, SKColor barColour)
    {
        var type = op.Name ?? string.Empty;
        var target = op.ObjectName ?? string.Empty;

        if (type.Length == 0 && target.Length == 0)
        {
            return;
        }

        const float textPadX = 4f;
        var availWidth = endX - startX - textPadX * 2;
        if (availWidth <= 0)
        {
            return;
        }

        // Grow the label to the largest size that fits the bar's height and width, capped at the max.
        // Width (type + gap + object name) scales linearly with font size, so derive the width-bound
        // size from a measure at the cap.
        _operatorBoldFont.Size = OperatorMaxFont;
        _operatorFont.Size = OperatorMaxFont;

        var typeW = type.Length > 0 ? _operatorBoldFont.MeasureText(type) : 0f;
        var targetW = target.Length > 0 ? _operatorFont.MeasureText(target) : 0f;
        var hasBoth = type.Length > 0 && target.Length > 0;
        var widthAtMax = typeW + targetW + (hasBoth ? OperatorMaxFont * OperatorLabelGapFraction : 0f);

        var sizeByWidth = widthAtMax <= 0 ? OperatorMaxFont : OperatorMaxFont * availWidth / widthAtMax;
        var sizeByHeight = barHeight - 2f;
        var size = Math.Min(OperatorMaxFont, Math.Min(sizeByWidth, sizeByHeight));

        // Hide the label entirely if it can't be shown at a legible size rather than clipping it.
        if (size < OperatorMinFont)
        {
            return;
        }

        _operatorBoldFont.Size = size;
        _operatorFont.Size = size;
        _operatorTextPaint.Color = OperatorLabelColour;

        // Centre the text vertically within the bar.
        var baseline = y + size * 0.35f;
        var x = startX + textPadX;

        if (type.Length > 0)
        {
            canvas.DrawText(type, x, baseline, SKTextAlign.Left, _operatorBoldFont, _operatorTextPaint);
            x += _operatorBoldFont.MeasureText(type) + (target.Length > 0 ? size * OperatorLabelGapFraction : 0f);
        }

        if (target.Length > 0)
        {
            canvas.DrawText(target, x, baseline, SKTextAlign.Left, _operatorFont, _operatorTextPaint);
        }
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

    private int GetRowIndex(EngineEvent ev)
    {
        for (var i = 0; i < _activeRows.Length; i++)
            if (_activeRows[i].EventType.IsInstanceOfType(ev))
            {
                return i;
            }

        return -1;
    }

    // AllRows minus the Log lane unless there are transaction-log events to show.
    private void BuildActiveRows()
    {
        var hasLog = _sortedEvents.Any(e => e is TransactionLogEvent);

        _activeRows = hasLog
            ? AllRows
            : AllRows.Where(r => r.EventType != typeof(TransactionLogEvent)).ToArray();

        _rowEventCounts = new int[_activeRows.Length];
        foreach (var ev in _sortedEvents)
        {
            var idx = GetRowIndex(ev);
            if (idx >= 0)
            {
                _rowEventCounts[idx]++;
            }
        }
    }

    // Markers on rows with fewer than SparseRowThreshold events are drawn wider so they stand out.
    private float RowMarkerWidth(int rowIndex) =>
        rowIndex >= 0 && rowIndex < _rowEventCounts.Length && _rowEventCounts[rowIndex] < SparseRowThreshold
            ? SparseMarkerWidth
            : MarkerWidth;

    private static SKColor TintByCategory(SKColor colour, int category) 
        => ColourScale(colour, CategoryShade[category]);

    /// <summary>Scales a colour's RGB channels by <paramref name="factor"/> (clamped), preserving alpha.</summary>
    private static SKColor ColourScale(SKColor colour, float factor) => new(
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
        var hit = HitTestRegion(position.X, position.Y);

        if (hit is null)
        {
            return;
        }

        if (hit.Value.Event is ExecutionOperatorEvent { PlanNodeIdentifier: { } node })
        {
            PlanNodeSelected?.Invoke(node);
        }
        else
        {
            // A point marker (read/lock/wait/log): reveal that event in the event grid.
            EventSelected?.Invoke(hit.Value.Event);
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
        _hoverLabel = null;
    }

    /// <summary>Shows a pointer-following tooltip with the name of the event under the pointer.</summary>
    private void UpdateHoverTooltip(Windows.Foundation.Point position)
    {
        var hit = HitTestRegion(position.X, position.Y);

        if (hit is null)
        {
            HideTooltip();
            return;
        }

        var region = hit.Value;

        if (!ReferenceEquals(region.Event, _hoverEvent) || region.Label != _hoverLabel)
        {
            _hoverEvent = region.Event;
            _hoverLabel = region.Label;
            _toolTipText.Text = region.Label ?? region.Event.Description;
        }

        _toolTip.HorizontalOffset = position.X + 12;
        _toolTip.VerticalOffset = position.Y + 12;
        _toolTip.IsOpen = true;
    }

    private (EngineEvent Event, string? Label)? HitTestRegion(double x, double y)
    {
        // Operators (and their phase bars) are added after markers, so iterate in reverse to prefer them.
        for (var i = _hitRegions.Count - 1; i >= 0; i--)
        {
            if (_hitRegions[i].Bounds.Contains((float)x, (float)y))
            {
                return (_hitRegions[i].Event, _hitRegions[i].Label);
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

        // Sweep the whole range over BasePlayDurationMs (at 1x) so the wall-clock duration is the same
        // for a 25ms query and a multi-second one - the playhead just moves slower over a short range.
        var rangeMs = Math.Max(rangeEnd - rangeStart, 1e-6);
        var eventsInRange = Math.Max(1, _times.Count(t => t >= rangeStart && t <= rangeEnd));
        _basePlayStep = rangeMs * PlayTickMs / BasePlayDurationMs;
        _playStep = _basePlayStep * SpeedMultiplier;

        // Gap-skip threshold from the average event spacing, so only genuine dead air is skipped.
        _gapThreshold = rangeMs / eventsInRange * GapSkipFactor;

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
        else if (NextEventTime(_playheadTime, _playEndTime) is { } next && next - _playheadTime > _gapThreshold)
        {
            // Dead air ahead: jump straight to the next activity rather than gliding across the gap.
            _playheadTime = next;
        }

        SyncHandlesToPlayhead();
        FirePlayhead();
        _skCanvas.Invalidate();
    }
}
