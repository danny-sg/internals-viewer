using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI;
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

namespace InternalsViewer.UI.App.Controls.Timeline;

public sealed class EventTimelineControl : Grid, IDisposable
{
    private const float RulerBandHeight = 18f;
    private const float HandleBandHeight = 16f;
    private const float MarkerStripHeight = RulerBandHeight + HandleBandHeight;
    private const float HandleWidth = 7f;

    // The from/to handles are drawn as short flat-top triangles in the top of the handle band (rather
    // than full-height bars), so they present a small target that's harder to grab by accident while
    // scrubbing the playhead. The handle is only grabbable within this top slice of the strip.
    private const float HandleHeight = 8f;

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

    // Event times/durations are stored in microseconds; the timeline axis works in milliseconds, so
    // event values are divided by this to convert (1000 µs per ms).
    private const double AxisUnitsPerMs = 1000.0;

    // Timer tick for smooth motion. Play sweeps the whole range left-to-right over a fixed wall-clock
    // duration (BasePlayDurationMs at 1x), regardless of how many events or how short the range is.
    private const double PlayTickMs = 16;
    private const double BasePlayDurationMs = 10_000;

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

    // Pre-built blobs for the fixed row labels — rebuilt whenever _activeRows changes.
    private SKTextBlob?[] _rowLabelBlobs = [];

    // Event count per active row, used to widen markers on sparse rows.
    private int[] _rowEventCounts = [];

    private readonly Button _playButton;
    private readonly Button _stepBackButton;
    private readonly Button _stepForwardButton;
    // The I/O trace extensions are always drawn; the toolbar toggle controls the (more involved)
    // per-thread sub-lane overlay instead, which is off by default.
    private readonly ToggleButton _threadsButton;
    private bool _showThreads;
    private readonly SKXamlCanvas _skCanvas;
    private readonly Canvas _overlay;
    private readonly ScrollBar _scrollBar;
    private readonly Popup _toolTip;
    private readonly TextBlock _toolTipText;

    private readonly List<(SKRect Bounds, EngineEvent Event, string? Label)> _hitRegions = [];
    private EngineEvent? _hoverEvent;
    private string? _hoverLabel;

    // The plan node id of the clicked operator, whose row-flow path (up to the root) is highlighted.
    private int? _selectedNodeId;

    private readonly SKFont _labelFont = new(SKTypeface.Default, 10f);

    // Operator bar labels get their own font so the size can be scaled per bar (up to OperatorMaxFont).
    // The type (e.g. "Index Scan") is drawn bold, the object name that follows in the regular font.
    private readonly SKFont _operatorFont = new(SKTypeface.Default, 12f);
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

    // Data-access (scan/seek) bars are sized within their slot by rows processed; this is the smallest
    // fill fraction so even a tiny scan stays visible.
    private const float DataAccessMinFill = 0.15f;

    // Parallel operators draw one sub-lane per thread inside the bar; below this lane height the
    // threads are shown as a concurrency-density fill instead. A thin gap separates adjacent lanes.
    private const float MinThreadLaneHeight = 2.5f;
    private const float ThreadLaneGap = 1f;

    // Operator labels are only drawn when the bar is tall and wide enough to be legible.
    private const float MinLabelBarHeight = 11f;
    private const float MinLabelBarWidth = 26f;

    // Operator labels scale up to this size when the bar has room, and are hidden below the minimum.
    private const float OperatorMaxFont = 12f;
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
    // Dims the rows outside the from/to selection so only the selected (clipped) window stands out.
    private readonly SKPaint _clipDimPaint = new()
    {
        Color = new SKColor(0, 0, 0, 120),
        Style = SKPaintStyle.Fill,
    };
    private readonly SKPaint _tickPaint = new()
    {
        Color = new SKColor(110, 110, 110),
        StrokeWidth = 1,
        Style = SKPaintStyle.Stroke,
        IsAntialias = false,
    };

    private readonly SKColor _laneColour = new SKColor(30, 30, 30, 220);
    private readonly SKColor _alternateLaneColour = new SKColor(30, 30, 30, 220);

    private readonly SKPaint _separatorPaint = new SKPaint { Color = new SKColor(60, 60, 60), StrokeWidth = 1 };

    private List<EngineEvent> _sortedEvents = [];
    private List<double> _times = [];

    // Cached per-event-set layout data for the operator (Plan) row, rebuilt in BuildOperatorLayout.
    private List<(int Index, ExecutionOperatorEvent Op)> _orderedOperators = [];
    private double _maxCost;
    private long _maxRows;

    private double _minTime;
    private double _maxTime;
    private double _timeRange;

    private double _playheadTime;
    private double _playStartTime;
    private double _playEndTime;
    private double _playStep;
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
        ctrl.BuildOperatorLayout();

        ctrl.StopPlay();
        ctrl.Reset();

        ctrl._skCanvas.Invalidate();
    }

    /// <summary>
    /// Optional crop (microseconds): when set the timeline axis spans only [<see cref="StartOffset"/>,
    /// <see cref="EndOffset"/>], hiding activity outside the window (e.g. pre-query events) so the
    /// displayed time runs from 0 at <see cref="StartOffset"/>. Internally events keep their true time -
    /// only the displayed ruler and the visible extent are offset. <c>null</c> means "no crop".
    /// </summary>
    public long? StartOffset
    {
        get => (long?)GetValue(StartOffsetProperty);
        set => SetValue(StartOffsetProperty, value);
    }

    public static readonly DependencyProperty StartOffsetProperty =
        DependencyProperty.Register(nameof(StartOffset), typeof(long?), typeof(EventTimelineControl),
            new PropertyMetadata(null, OnCropChanged));

    public long? EndOffset
    {
        get => (long?)GetValue(EndOffsetProperty);
        set => SetValue(EndOffsetProperty, value);
    }

    public static readonly DependencyProperty EndOffsetProperty =
        DependencyProperty.Register(nameof(EndOffset), typeof(long?), typeof(EventTimelineControl),
            new PropertyMetadata(null, OnCropChanged));

    private static void OnCropChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (EventTimelineControl)d;

        control.BuildTimes();

        if (control._sortedEvents.Count > 0)
        {
            // Pull the playhead (and any non-activated handles) back into the cropped range, then
            // re-emit so the scope/active operator the other views show follows the clamped position.
            var clamped = Math.Clamp(control._playheadTime, control._minTime, control._maxTime);

            if (Math.Abs(clamped - control._playheadTime) > 0.1)
            {
                control._playheadTime = clamped;

                control.SyncHandlesToPlayhead();
                control.FirePlayhead();
            }
        }

        control.UpdateScrollBar();
        control._skCanvas.Invalidate();
    }

    // Last emitted values (microseconds), kept only to suppress duplicate notifications during play.
    private long _scopeFromUs;
    private long _scopeToUs = -1;
    private long _playheadUs = -1;

    /// <summary>
    /// Raised when the in-scope window changes, as microsecond times (from, to). The control deals only
    /// in time - mapping a time to events/sequences/pages is the consumer's responsibility.
    /// </summary>
    public event Action<long, long>? ScopeChanged;

    /// <summary>Raised when the playhead moves, with its position in microseconds.</summary>
    public event Action<long>? PlayheadTimeChanged;

    /// <summary>Raised when a plan operator in the timeline is clicked.</summary>
    public event Action<PlanNodeIdentifier>? PlanNodeSelected;

    /// <summary>Raised when an individual event marker is clicked (to reveal it in the event grid).</summary>
    public event Action<EngineEvent>? EventSelected;

    /// <summary>Raised when "Open Index" is chosen on a scan/seek operator (carries schema/table/index).</summary>
    public event Action<ExecutionOperatorEvent>? IndexOpenRequested;

    /// <summary>Raised when auto-play starts (true) or stops (false).</summary>
    public event Action<bool>? PlayStateChanged;

    public EventTimelineControl()
    {
        Background = new SolidColorBrush(Colors.Transparent);

        // transport toolbar
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // timeline
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // scroll bar
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

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
        _stepBackButton = MakeTransportButton(new FontIcon { Glyph = "", FontSize = 12 }, 30);
        _stepBackButton.Click += OnStepBackButtonClick;

        _stepForwardButton = MakeTransportButton(new FontIcon { Glyph = "", FontSize = 12 }, 30);
        _stepForwardButton.Click += OnStepForwardButtonClick;

        _threadsButton = new ToggleButton
        {
            Content = new TextBlock { Text = "Threads", FontSize = 10 },
            Height = TransportButtonHeight,
            Margin = new Thickness(8, 2, 0, 2),
            VerticalAlignment = VerticalAlignment.Center,
            BorderBrush = null,
            Background = new SolidColorBrush(Color.FromArgb(0, 30, 30, 30)),
        };
        _threadsButton.Checked += OnThreadsToggled;
        _threadsButton.Unchecked += OnThreadsToggled;

        var transport = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        transport.Children.Add(_stepBackButton);
        transport.Children.Add(_playButton);
        transport.Children.Add(_stepForwardButton);
        transport.Children.Add(_threadsButton);

        SetRow(transport, 0);
        Children.Add(transport);

        _skCanvas = new SKXamlCanvas { IgnorePixelScaling = true };
        _skCanvas.PaintSurface += OnPaintSurface;

        SetRow(_skCanvas, 1);
        Children.Add(_skCanvas);

        _overlay = new Canvas { Background = new SolidColorBrush(Colors.Transparent) };
        SetRow(_overlay, 1);
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
        SetRow(_scrollBar, 2);
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
        _overlay.ContextRequested += OnContextRequested;

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

    private void OnThreadsToggled(object sender, RoutedEventArgs e)
    {
        _showThreads = _threadsButton.IsChecked == true;
        _skCanvas.Invalidate();
    }

    private void OnStepBackButtonClick(object sender, RoutedEventArgs e) => StepToAdjacentEvent(forward: false);

    private void OnStepForwardButtonClick(object sender, RoutedEventArgs e) => StepToAdjacentEvent(forward: true);

    /// <summary>
    /// Jumps the playhead to the previous/next I/O read event - used to step across dead-air gaps to the
    /// next read. Playback is not interrupted: if playing, it continues from the new position.
    /// </summary>
    private void StepToAdjacentEvent(bool forward)
    {
        if (_sortedEvents.Count == 0)
        {
            return;
        }

        var (lo, hi) = ActiveRange;

        // The nearest read (by time) on the requested side of the playhead, within the active range.
        var target = -1;
        var bestTime = forward ? double.MaxValue : double.MinValue;

        for (var i = 0; i < _sortedEvents.Count; i++)
        {
            if (_sortedEvents[i] is not IoEvent { IsRead: true })
            {
                continue;
            }

            var t = _times[i];

            if (t < lo || t > hi)
            {
                continue;
            }

            if (forward ? t > _playheadTime && t < bestTime
                        : t < _playheadTime && t > bestTime)
            {
                bestTime = t;
                target = i;
            }
        }

        if (target < 0)
        {
            return;
        }

        _playheadTime = _times[target];

        SyncHandlesToPlayhead();

        FirePlayhead();

        EnsurePlayheadVisible();

        _skCanvas.Invalidate();
    }

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
        _selectedNodeId = null;

        _selectionActivated = false;
        _playheadTime = _minTime;
        _startTime = _playheadTime;
        _endTime = _playheadTime;

        _scopeFromUs = ToUs(_minTime);
        _scopeToUs = ToUs(_playheadTime);
        _playheadUs = _scopeToUs;

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

        if (_sortedEvents.Count == 0)
        {
            _minTime = 0;
            _maxTime = 1;
            _timeRange = 1;
            return;
        }

        var min = double.MaxValue;
        var max = double.MinValue;

        for (var i = 0; i < _sortedEvents.Count; i++)
        {
            var ev = _sortedEvents[i];
            var start = StartMs(ev);
            _times.Add(start);

            if (start < min)
            {
                min = start;
            }

            // Operators occupy [start, start + duration]; point events are an instant at start. Events
            // sharing a coarse timestamp are already spread to distinct times upstream (see
            // EventReader.SpreadCoincidentEvents), so nothing is fanned out here.
            var end = ev is ExecutionOperatorEvent ? start + DurationMs(ev) : start;

            if (end > max)
            {
                max = end;
            }
        }

        // Apply the optional crop: a set Start/EndOffset (microseconds) overrides the natural event
        // extent so that activity outside the cropped window falls off the axis (clipped by the canvas).
        _minTime = StartOffset.HasValue ? StartOffset.Value / 1000.0 : min;
        _maxTime = EndOffset.HasValue ? EndOffset.Value / 1000.0 : max;
        _timeRange = Math.Max(_maxTime - _minTime, 1.0);
    }

    // Precomputes the ordered operator list and per-set aggregates used every paint frame.
    private void BuildOperatorLayout()
    {
        var operators = new List<(int Index, ExecutionOperatorEvent Op)>();

        for (var i = 0; i < _sortedEvents.Count; i++)
        {
            if (_sortedEvents[i] is ExecutionOperatorEvent op)
            {
                operators.Add((i, op));
            }
        }

        _orderedOperators = [.. operators
            .OrderBy(o => o.Op.NodeLevel)
            .ThenBy(o => _times[o.Index])
            .ThenBy(o => o.Op.PlanNodeIdentifier?.NodeId ?? 0)];

        _maxCost = operators.Count > 0
            ? operators.Where(o => o.Op.NodeLevel > 0).Select(o => o.Op.Cost ?? 0).DefaultIfEmpty(0).Max()
            : 0;

        _maxRows = operators.Count > 0
            ? operators.Where(o => o.Op.Category == OperatorCategory.DataAccess).Select(o => o.Op.RowsProcessed).DefaultIfEmpty(0).Max()
            : 0;
    }

    // EngineEvent.TimeUs / DurationUs are microseconds; divide by AxisUnitsPerMs (1000) to get ms.
    private static double StartMs(EngineEvent ev) => ev.TimeUs / AxisUnitsPerMs;

    private static double DurationMs(EngineEvent ev) => ev.DurationUs / AxisUnitsPerMs;

    // The selection only counts once the user has explicitly dragged a handle.
    private bool SelectionActive => _selectionActivated;

    // The active window the playhead is confined to and playback loops within: the from/to selection
    // when set, otherwise the whole axis.
    private (double Lo, double Hi) ActiveRange => SelectionActive
        ? (Math.Min(_startTime, _endTime), Math.Max(_startTime, _endTime))
        : (_minTime, _maxTime);

    // While not activated the handles sit on the playhead, so they follow it as it scrubs/plays.
    private void SyncHandlesToPlayhead()
    {
        if (!_selectionActivated)
        {
            _startTime = _playheadTime;
            _endTime = _playheadTime;
        }
    }

    // The axis works in milliseconds; events and the emitted playhead/scope are in microseconds.
    private static long ToUs(double ms) => (long)Math.Round(ms * 1000.0);

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
            var y = rowTops[r];
            var rowHeight = rowHeights[r];

            _rowBgPaint.Color = r % 2 == 0
                ? _laneColour
                : _alternateLaneColour;

            canvas.DrawRect(0, y, w, rowHeight, _rowBgPaint);

            var blob = r < _rowLabelBlobs.Length ? _rowLabelBlobs[r] : null;
            if (blob is not null)
            {
                canvas.DrawText(blob, 2, y + rowHeight / 2 + _labelFont.Size / 2, _labelPaint);
            }

            canvas.DrawLine(0, y + rowHeight, w, y + rowHeight, _separatorPaint);
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
            var sourceEvent = _sortedEvents[i];

            // Operator events have a duration and are drawn as lines in a separate pass.
            if (sourceEvent is ExecutionOperatorEvent)
            {
                continue;
            }

            var x = TimeToX(_times[i]);

            // Virtualisation: skip markers that fall outside the visible content. Skia would clip the
            // pixels anyway, but this also avoids the per-event work and the hit-region entry, which
            // dominates when zoomed in on a large capture.
            if (x > w || x < RowLabelWidth - SparseMarkerWidth)
            {
                continue;
            }

            var rowIndex = GetRowIndex(sourceEvent);

            if (rowIndex < 0)
            {
                continue;
            }

            var rowTop = rowTops[rowIndex];
            var innerTop = rowTop + RowPadding;
            var innerHeight = rowHeights[rowIndex] - RowPadding * 2;

            float markerTop;
            float markerHeight;

            var category = sourceEvent.Category;

            if (category.HasValue)
            {
                var stepHeight = innerHeight / EventCategoryClassifier.CategoryCount;
                var step = (int)category.Value;

                markerTop = innerTop + step * stepHeight;
                markerHeight = Math.Max(2f, stepHeight - 1f);

                _markerPaint.Color = TintByCategory(_activeRows[rowIndex].Color, step);
            }
            else
            {
                var displayColour = sourceEvent.DisplayColour;

                markerTop = innerTop;
                markerHeight = innerHeight;

                _markerPaint.Color = displayColour.A == 0 ? _activeRows[rowIndex].Color : displayColour.ToSkColor();
            }

            var markerWidth = RowMarkerWidth(rowIndex);

            canvas.DrawRect(x, markerTop, markerWidth, markerHeight, _markerPaint);

            // Widen the hit target a little so the thin markers are easy to hover.
            _hitRegions.Add((new SKRect(x - 3, markerTop, x + markerWidth + 3, markerTop + markerHeight), sourceEvent, null));
        }

        DrawOperatorLines(canvas, rowTops, rowHeights);

        DrawRuler(canvas);

        // Clip to the from/to selection: dim everything outside it so only the selected window - the
        // region the playhead is confined to and playback loops within - stands out.
        if (SelectionActive)
        {
            var lo = Math.Min(TimeToX(_startTime), TimeToX(_endTime));
            var hi = Math.Max(TimeToX(_startTime), TimeToX(_endTime));

            if (lo > RowLabelWidth)
            {
                canvas.DrawRect(RowLabelWidth, rowsTop, lo - RowLabelWidth, rowsHeight, _clipDimPaint);
            }

            if (hi < w)
            {
                canvas.DrawRect(hi, rowsTop, w - hi, rowsHeight, _clipDimPaint);
            }
        }

        DrawHandle(canvas, StartDrawX, isStart: true);
        DrawHandle(canvas, EndDrawX, isStart: false);

        var px = PlayheadX;

        canvas.DrawLine(px, MarkerStripHeight, px, h, _playheadPaint);

        DrawPlayheadTriangle(canvas, px);

        DrawPlayheadTimeBadge(canvas, px);

        canvas.Restore();
    }

    /// <summary>
    /// Draws header time ruler
    /// </summary>
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

        Span<char> textBuffer = stackalloc char[12];

        for (var tickMs = Math.Ceiling(leftMs / interval) * interval; tickMs <= rightMs; tickMs += interval)
        {
            var x = TimeToX(_minTime + tickMs);

            canvas.DrawLine(x, RulerBandHeight - 4, x, RulerBandHeight, _tickPaint);

            textBuffer.Clear();

            var length = FormatTimeIntoSpan(tickMs, textBuffer);

            using var blob = SKTextBlob.Create(textBuffer[..length], _labelFont, SKPoint.Empty);

            if (blob is not null)
            {
                canvas.DrawText(blob, x + 2, RulerBandHeight - 6, _labelPaint);
            }
        }
    }

    private void DrawPlayheadTimeBadge(SKCanvas canvas, float px)
    {
        Span<char> buf = stackalloc char[12];
        var len = FormatTimeIntoSpan(EffectiveToMs(_playheadTime), buf);
        var text = buf[..len];

        const float padding = 4f;

        var badgeWidth = _labelFont.MeasureText(text, null) + padding * 2;

        const float badgeHeight = RulerBandHeight - 2;

        var bx = Math.Clamp(px - badgeWidth / 2f, RowLabelWidth, Math.Max(RowLabelWidth, CanvasWidth - badgeWidth));

        canvas.DrawRoundRect(new SKRect(bx, 0, bx + badgeWidth, badgeHeight), 2, 2, _playheadFill);

        _operatorTextPaint.Color = SKColors.White;

        var baseline = badgeHeight / 2f + _labelFont.Size * 0.35f;

        using var blob = SKTextBlob.Create(text, _labelFont, SKPoint.Empty);

        if (blob is not null)
        {
            canvas.DrawText(blob, bx + padding, baseline, _operatorTextPaint);
        }
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

    private static int FormatTimeIntoSpan(double ms, Span<char> buf)
    {
        if (ms < 0)
        {
            ms = 0;
        }

        bool ok;
        int written;

        if (ms < 10)
        {
            ok = ((double)ms).TryFormat(buf, out written, "0.##");
        }
        else if (ms < 1000)
        {
            ok = ((double)ms).TryFormat(buf, out written, "0");
        }
        else
        {
            var seconds = ms / 1000.0;
            ok = seconds < 10
                ? seconds.TryFormat(buf, out written, "0.00")
                : seconds.TryFormat(buf, out written, "0.0");

            if (ok && written + 1 <= buf.Length)
            {
                buf[written++] = 's';
                return written;
            }

            return ok ? written : 0;
        }

        if (ok && written + 2 <= buf.Length)
        {
            buf[written++] = 'm';
            buf[written++] = 's';
        }

        return ok ? written : 0;
    }

    private readonly record struct OperatorBar(ExecutionOperatorEvent Op,
                                               float StartX,
                                               float EndX,
                                               float BarTop,
                                               float BarBottom,
                                               float BarCentreY,
                                               float LineWidth,
                                               float CornerRadius,
                                               float SlotCentreY,
                                               float SlotHeight,
                                               SKColor BarColour);

    private void DrawOperatorLines(SKCanvas canvas, float[] rowTops, float[] rowHeights)
    {
        var planRow = -1;

        for (var r = 0; r < _activeRows.Length; r++)
        {
            if (_activeRows[r].EventType == typeof(ExecutionOperatorEvent))
            {
                planRow = r; break;
            }
        }

        if (planRow < 0)
        {
            return;
        }

        var ordered = _orderedOperators;

        if (ordered.Count == 0)
        {
            return;
        }

        var maxCost = _maxCost;
        var maxRows = _maxRows;

        var top = rowTops[planRow] + RowPadding;
        var height = rowHeights[planRow] - RowPadding * 2;

        var totalWeight = 0f;

        foreach (var (_, op) in ordered)
        {
            totalWeight += CostWeight(op);
        }

        var unit = totalWeight > 0 ? height / totalWeight : height;

        var slotByIndex = new Dictionary<int, (float Y, float Height)>(ordered.Count);
        var slotAcc = top;

        foreach (var (index, op) in ordered)
        {
            var slot = CostWeight(op) * unit;

            slotByIndex[index] = (slotAcc + slot / 2f, slot);
            slotAcc += slot;
        }

        var bars = new List<OperatorBar>(ordered.Count);

        foreach (var (index, op) in ordered)
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

            // Lay the bar out within the slot. Buffer operators collapse to a thin bar; everything else
            // fills the slot less a margin.
            var slotTop = y - slotHeight / 2f;
            var slotBottom = y + slotHeight / 2f;

            // In Trace mode add extra padding so stacked bars leave a gap for the trace lines to show.
            var effectiveMargin = OperatorLineMargin + TraceStackGap;

            var pad = effectiveMargin / 2f;

            var availTop = slotTop + pad;
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
            else if (op.Category == OperatorCategory.DataAccess && maxRows > 0)
            {
                // Size scan/seek bars by rows processed: thicker = more data, sqrt-compressed against the
                // busiest data-access operator, with a floor so even a tiny scan stays visible.
                var avail = availBottom - availTop;
                var fill = op.RowsProcessed > 0
                    ? Math.Clamp((float)Math.Sqrt(op.RowsProcessed / (double)maxRows), DataAccessMinFill, 1f)
                    : DataAccessMinFill;
                var barHeight = Math.Max(1f, avail * fill);
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

        // Trace: draw the extensions first so the operator bars paint over them (always on).
        DrawTraces(canvas, bars, rowTops, rowHeights);

        var rightEdge = CanvasWidth;

        foreach (var b in bars)
        {
            // Skip operators whose bar is entirely off-screen
            if (b.EndX < RowLabelWidth || b.StartX > rightEdge)
            {
                continue;
            }

            // Subtle top-lit sheen: lighten the top edge, darken the bottom edge of the bar
            var gradient = SKShader.CreateLinearGradient(new SKPoint(b.StartX, b.BarTop),
                                                         new SKPoint(b.StartX, b.BarBottom),
                                                         [
                                                             ColourScale(b.BarColour, 1f + GradientLift), 
                                                             ColourScale(b.BarColour, 1f - GradientLift)
                                                         ],
                                                         null,
                                                         SKShaderTileMode.Clamp);

            _operatorPaint.Color = b.BarColour;
            _operatorPaint.Shader = gradient;

            canvas.DrawRoundRect(new SKRect(b.StartX, b.BarTop, b.EndX, b.BarBottom),
                                 b.CornerRadius, b.CornerRadius, _operatorPaint);

            _operatorPaint.Shader = null;
            
            gradient.Dispose();

            // Parallel operators: overlay per-thread sub-lanes (or a concurrency-density fill) on the envelope bar so
            // the degree of parallelism and thread skew are visible
            if (_showThreads && b.Op.Threads.Count > 1)
            {
                DrawOperatorThreads(canvas, b);
            }

            // Blocking operators: dim the consume phase, where the operator is consuming its input but not yet
            // emitting rows upward (e.g. a hash build, a sort). The solid remainder is the emit.
            DrawConsumeShade(canvas, b);

            if (b.LineWidth >= MinLabelBarHeight && b.EndX - b.StartX >= MinLabelBarWidth)
            {
                DrawOperatorLabel(canvas, b.Op, b.StartX, b.EndX, b.BarCentreY, b.LineWidth);
            }

            _hitRegions.Add((new SKRect(b.StartX, b.SlotCentreY - b.SlotHeight / 2f, b.EndX,
                                        b.SlotCentreY + b.SlotHeight / 2f), b.Op, null));
        }

        // On click, trace the selected operator's rows up to the root: a connector per hop, lit only while the source
        // is emitting (non-dimmed).
        if (_selectedNodeId is { } selected)
        {
            DrawRowFlowPath(canvas, bars, selected);
        }

        return;

        float CostWeight(ExecutionOperatorEvent op)
        {
            if (op.NodeLevel == 0)
            {
                return StatementBandWeight;
            }

            if (maxCost <= 0)
            {
                // No cost information - fall back to an equal share for every operator
                return MaxCostWeight;
            }

            var normalised = (float)Math.Sqrt(Math.Clamp((op.Cost ?? 0) / maxCost, 0, 1));
            return MinCostWeight + (MaxCostWeight - MinCostWeight) * normalised;
        }
    }

    /// <summary>
    /// Overlays a parallel operator's worker threads on its bar. The coordinator (thread 0) is the bar
    /// itself (its span is the whole block), so only the workers (non-zero ids) get sub-lanes: each
    /// spans its own start→end (time skew) and is as tall as its share of the rows processed (data
    /// skew). When the lanes would be too thin to read, falls back to a concurrency-density fill.
    /// </summary>
    private void DrawOperatorThreads(SKCanvas canvas, OperatorBar b)
    {
        var workers = b.Op.Threads.Where(t => t.ThreadId != 0).ToList();
        if (workers.Count == 0)
        {
            return;
        }

        var barHeight = b.BarBottom - b.BarTop;

        // If the lanes would on average be too thin to read, show the density fill instead.
        if (barHeight / workers.Count < MinThreadLaneHeight)
        {
            DrawThreadDensity(canvas, b, workers);
            return;
        }

        // Stack the workers, each lane as tall as its share of the rows (so an over-loaded thread reads
        // as a thick lane and an idle one as a sliver). Fall back to equal shares with no row counts.
        var totalRows = workers.Sum(t => t.RowsProcessed);

        var y = b.BarTop;

        foreach (var t in workers)
        {
            var share = totalRows > 0 ? (float)t.RowsProcessed / totalRows : 1f / workers.Count;
            var laneHeight = barHeight * share;

            if (laneHeight >= 0.5f)
            {
                var x0 = Math.Max(b.StartX, TimeToX(t.StartUs / AxisUnitsPerMs));
                var x1 = Math.Min(b.EndX, TimeToX(t.EndUs / AxisUnitsPerMs));
                if (x1 < x0 + 1f)
                {
                    x1 = x0 + 1f;
                }

                // Workers read a touch brighter than the envelope (coordinator) bar behind them.
                _markerPaint.Color = ColourScale(b.BarColour, 1.12f);
                canvas.DrawRect(x0, y, x1 - x0, Math.Max(1f, laneHeight - ThreadLaneGap), _markerPaint);
            }

            y += laneHeight;
        }
    }

    /// <summary>
    /// High degree-of-parallelism fallback: shades the envelope bar by the number of worker threads
    /// running concurrently over time (darker = more overlap), by sweeping their start/end points.
    /// </summary>
    private void DrawThreadDensity(SKCanvas canvas, OperatorBar b, List<OperatorThread> workers)
    {
        var points = new List<(double Ms, int Delta)>(workers.Count * 2);
        foreach (var t in workers)
        {
            points.Add((t.StartUs / AxisUnitsPerMs, +1));
            points.Add((t.EndUs / AxisUnitsPerMs, -1));
        }

        points.Sort((p, q) => p.Ms.CompareTo(q.Ms));

        var active = 0;
        var prevMs = points[0].Ms;

        foreach (var (ms, delta) in points)
        {
            if (ms > prevMs && active > 0)
            {
                var x0 = Math.Max(b.StartX, TimeToX(prevMs));
                var x1 = Math.Min(b.EndX, TimeToX(ms));

                if (x1 > x0)
                {
                    // Map 1..DOP concurrent workers onto a 0.7→1.3 brightness ramp over the envelope.
                    var intensity = (float)active / workers.Count;

                    _markerPaint.Color = ColourScale(b.BarColour, 0.7f + 0.6f * intensity);

                    canvas.DrawRect(x0, b.BarTop, x1 - x0, b.BarBottom - b.BarTop, _markerPaint);
                }
            }

            active += delta;
            prevMs = ms;
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
                if (_sortedEvents[i] is not IoEvent { PlanNodeIdentifier: { } id } io ||
                    !byNode.TryGetValue(id, out var b) ||
                    b.BarBottom >= ioTop)
                {
                    continue;
                }

                var x = TimeToX(_times[i]);
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
                if (_sortedEvents[i] is not TransactionLogEvent { PlanNodeIdentifier: { } id } log ||
                    !byNode.TryGetValue(id, out var b) ||
                    b.BarTop <= logBottom)
                {
                    continue;
                }

                var x = TimeToX(_times[i]);
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
    /// Dims the consume (build) phase of a blocking operator on its bar: the span where it is reading
    /// its input but has not yet started emitting rows to its parent (a hash build, a sort's run
    /// formation). The undimmed remainder of the bar is the emit phase. Streaming operators have no
    /// consume phase and so are left fully solid.
    /// </summary>
    private void DrawConsumeShade(SKCanvas canvas, OperatorBar b)
    {
        if (b.Op.BuildPhaseDurationUs <= 0)
        {
            return;
        }

        var consumeStartX = Math.Max(b.StartX, TimeToX(b.Op.BuildPhaseTimeUs / AxisUnitsPerMs));
        var consumeEndX = Math.Min(b.EndX,
            TimeToX((b.Op.BuildPhaseTimeUs + b.Op.BuildPhaseDurationUs) / AxisUnitsPerMs));

        if (consumeEndX <= consumeStartX)
        {
            return;
        }

        // Clip to the bar so the overlay respects its rounded corners.
        canvas.Save();
        canvas.ClipRoundRect(
            new SKRoundRect(new SKRect(b.StartX, b.BarTop, b.EndX, b.BarBottom), b.CornerRadius, b.CornerRadius),
            antialias: true);

        _markerPaint.Color = ConsumeShadeColour;
        canvas.DrawRect(consumeStartX, b.BarTop, consumeEndX - consumeStartX, b.BarBottom - b.BarTop, _markerPaint);

        canvas.Restore();

        _hitRegions.Add((new SKRect(consumeStartX, b.BarTop, consumeEndX, b.BarBottom), b.Op, "Consuming"));
    }

    /// <summary>
    /// Traces the clicked operator's rows up to the root: a connector for each child→parent hop, lit
    /// only over the window the source is emitting (its non-dimmed span). Because emit time only moves
    /// later up the tree, the lit segments form a rising staircase showing where the flow is held up.
    /// </summary>
    private void DrawRowFlowPath(SKCanvas canvas, List<OperatorBar> bars, int selectedNodeId)
    {
        var barByNode = new Dictionary<int, OperatorBar>(bars.Count);
        foreach (var bar in bars)
        {
            if (bar.Op.PlanNodeIdentifier is { } id)
            {
                barByNode[id.NodeId] = bar;
            }
        }

        if (!barByNode.TryGetValue(selectedNodeId, out var start))
        {
            return;
        }

        // The chain selected → … → root.
        var chain = new List<OperatorBar> { start };
        var current = start;
        while (current.Op.ParentNodeId is { } parentId && barByNode.TryGetValue(parentId, out var parent))
        {
            chain.Add(parent);
            current = parent;
        }

        // Connector ribbons between consecutive operators, lit over the child's emit window.
        for (var i = 0; i < chain.Count - 1; i++)
        {
            DrawFlowConnector(canvas, chain[i], chain[i + 1]);
        }

        // Outline each operator on the path; the clicked one stands out.
        foreach (var bar in chain)
        {
            var isSelected = bar.Op.PlanNodeIdentifier?.NodeId == selectedNodeId;
            OutlineBar(canvas, bar, isSelected ? FlowSelectedColour : FlowPathColour, isSelected ? 2f : 1f);
        }
    }

    private void DrawFlowConnector(SKCanvas canvas, OperatorBar child, OperatorBar parent)
    {
        // Rows flow from the child while it is emitting: [EmitStart, End].
        var x0 = Math.Max(RowLabelWidth, TimeToX(child.Op.EmitStartUs / AxisUnitsPerMs));
        var x1 = Math.Min(CanvasWidth, TimeToX((child.Op.TimeUs + child.Op.DurationUs) / AxisUnitsPerMs));

        if (x1 <= x0)
        {
            return;
        }

        // Bridge the two bars from the top edge of the upper to the bottom edge of the lower.
        var yLo = Math.Min(child.BarTop, parent.BarTop);
        var yHi = Math.Max(child.BarBottom, parent.BarBottom);

        using var paint = new SKPaint { Color = FlowConnectorColour, Style = SKPaintStyle.Fill, IsAntialias = true };
        canvas.DrawRect(x0, yLo, x1 - x0, yHi - yLo, paint);
    }

    private static void OutlineBar(SKCanvas canvas, OperatorBar b, SKColor colour, float strokeWidth)
    {
        using var paint = new SKPaint
        {
            Color = colour,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = strokeWidth,
            IsAntialias = true,
        };

        canvas.DrawRoundRect(new SKRect(b.StartX, b.BarTop, b.EndX, b.BarBottom),
                             b.CornerRadius, b.CornerRadius, paint);
    }

    // Row-flow overlay (shown when an operator is clicked): a translucent ribbon links each operator to
    // its parent over the window rows are flowing; the path operators are outlined, the clicked one brighter.
    private static readonly SKColor FlowConnectorColour = new(120, 200, 255, 70);
    private static readonly SKColor FlowPathColour = new(200, 200, 200, 200);
    private static readonly SKColor FlowSelectedColour = new(255, 255, 255, 230);

    // The statement (SELECT) band is a fixed slim share, independent of cost.
    private const float StatementBandWeight = 0.5f;

    // Operator slot heights scale with cost between these weights: the cheapest operator gets the
    // minimum share (so it stays legible) and the most expensive gets the maximum.
    private const float MinCostWeight = 0.35f;
    private const float MaxCostWeight = 1.5f;

    // RGB lift/drop for the subtle vertical sheen on operator bars (±14%).
    private const float GradientLift = 0.04f;

    // Translucent black overlay that dims the consume phase of a blocking operator (building/sorting
    // but not yet emitting), so it reads as "started but not producing rows".
    private static readonly SKColor ConsumeShadeColour = new(0, 0, 0, 115);

    // Gap between the bold type and the object name, as a fraction of the font size (scales with it).
    private const float OperatorLabelGapFraction = 0.5f;

    // Vertical gap between the stacked type and object-name lines in the two-line operator label.
    private const float TwoLineGap = 2f;

    /// <summary>
    /// Draws the operator type (bold) and, when present, the object name, sized to the largest that
    /// fits the bar. The layout is chosen by priority on the bar's height and width: the object name
    /// stacked below the type on two centred lines; failing that the two on a single line (type then
    /// object name, left aligned); failing that the operator type alone; otherwise nothing.
    /// </summary>
    private void DrawOperatorLabel(SKCanvas canvas,
                                   ExecutionOperatorEvent op,
                                   float startX,
                                   float endX,
                                   float y,
                                   float barHeight)
    {
        var opName = op.Name ?? string.Empty;
        var target = op.ObjectName ?? string.Empty;

        // Annotate parallel operators with their degree of parallelism (worker = non-zero thread id).
        var dop = op.Threads.Count(t => t.ThreadId != 0);

        // Write the type label (with optional DOP suffix) into a stack buffer to avoid heap allocation.
        Span<char> typeBuf = stackalloc char[64];
        var typeSpan = BuildTypeSpan(opName, dop, typeBuf);

        if (typeSpan.Length == 0 && target.Length == 0)
        {
            return;
        }

        const float textPadX = 8f;
        var availWidth = endX - startX - textPadX * 2;
        if (availWidth <= 0)
        {
            return;
        }

        // Widths scale linearly with font size, so measure once at the cap and derive each layout's
        // width-bound size from that.
        _operatorBoldFont.Size = OperatorMaxFont;
        _operatorFont.Size = OperatorMaxFont;

        var typeW = typeSpan.Length > 0 ? _operatorBoldFont.MeasureText(typeSpan, null) : 0f;
        var targetW = target.Length > 0 ? _operatorFont.MeasureText((ReadOnlySpan<char>)target, null) : 0f;
        var hasBoth = typeSpan.Length > 0 && target.Length > 0;

        _operatorTextPaint.Color = OperatorLabelColour;

        // 1. Two lines: type above the object name, left aligned. Each line scales with the wider of
        // the two, and the pair needs room for two rows of text plus the gap between them.
        if (hasBoth)
        {
            var widerAtMax = Math.Max(typeW, targetW);
            var sizeByWidth = widerAtMax <= 0 ? OperatorMaxFont : OperatorMaxFont * availWidth / widerAtMax;
            var sizeByHeight = (barHeight - 2f - TwoLineGap) / 2f;
            var size = Math.Min(OperatorMaxFont, Math.Min(sizeByWidth, sizeByHeight));

            if (size >= OperatorMinFont)
            {
                _operatorBoldFont.Size = size;
                _operatorFont.Size = size;

                var x = startX + textPadX;
                var halfGap = (size + TwoLineGap) / 2f;

                DrawTextSpan(canvas, typeSpan, x, y - halfGap + size * 0.35f, _operatorBoldFont, _operatorTextPaint);
                DrawTextSpan(canvas, target, x, y + halfGap + size * 0.35f, _operatorFont, _operatorTextPaint);
                return;
            }
        }

        // 2. Single line: type then object name, left aligned (the original layout) - when the bar is
        // too short for two lines but wide enough to fit both side by side.
        if (hasBoth)
        {
            var widthAtMax = typeW + targetW + OperatorMaxFont * OperatorLabelGapFraction;
            var sizeByWidth = widthAtMax <= 0 ? OperatorMaxFont : OperatorMaxFont * availWidth / widthAtMax;
            var sizeByHeight = barHeight - 2f;
            var size = Math.Min(OperatorMaxFont, Math.Min(sizeByWidth, sizeByHeight));

            if (size >= OperatorMinFont)
            {
                _operatorBoldFont.Size = size;
                _operatorFont.Size = size;

                var baseline = y + size * 0.35f;
                var x = startX + textPadX;

                DrawTextSpan(canvas, typeSpan, x, baseline, _operatorBoldFont, _operatorTextPaint);
                x += _operatorBoldFont.MeasureText(typeSpan, null) + size * OperatorLabelGapFraction;
                DrawTextSpan(canvas, target, x, baseline, _operatorFont, _operatorTextPaint);
                return;
            }
        }

        // 3. Single line: the operator type alone (or whichever single label is present) - when there
        // isn't room for both but the type still fits.
        var isPrimaryType = typeSpan.Length > 0;
        var primarySpan = isPrimaryType ? typeSpan : (ReadOnlySpan<char>)target;
        var primaryFont = isPrimaryType ? _operatorBoldFont : _operatorFont;
        var primaryWAtMax = isPrimaryType ? typeW : targetW;

        var nameSizeByWidth = primaryWAtMax <= 0 ? OperatorMaxFont : OperatorMaxFont * availWidth / primaryWAtMax;
        var nameSize = Math.Min(OperatorMaxFont, Math.Min(nameSizeByWidth, barHeight - 2f));

        if (nameSize >= OperatorMinFont)
        {
            primaryFont.Size = nameSize;
            DrawTextSpan(canvas, primarySpan, startX + textPadX, y + nameSize * 0.35f, primaryFont, _operatorTextPaint);
        }
    }

    // Writes the operator type label (with optional ×DOP suffix) into buf and returns the filled slice.
    private static ReadOnlySpan<char> BuildTypeSpan(string opName, int dop, Span<char> buf)
    {
        if (opName.Length == 0)
        {
            return ReadOnlySpan<char>.Empty;
        }

        opName.AsSpan().CopyTo(buf);
        var pos = opName.Length;

        if (dop > 1 && pos + 4 <= buf.Length)
        {
            buf[pos++] = ' ';
            buf[pos++] = '×';
            var ok = dop.TryFormat(buf[pos..], out var written);
            pos += ok ? written : 0;
        }

        return buf[..pos];
    }

    // Creates a SKTextBlob from a char span, draws it at (x, y), then disposes it.
    private static void DrawTextSpan(SKCanvas canvas, ReadOnlySpan<char> text, float x, float y,
                                     SKFont font, SKPaint paint)
    {
        using var blob = SKTextBlob.Create(text, font, SKPoint.Empty);
        if (blob is not null)
        {
            canvas.DrawText(blob, x, y, paint);
        }
    }

    // A short flat-top right triangle at the bottom of the handle band: the start (left) handle is a
    // "/-" - flat top, vertical left edge, "/" slope down to a point at the bottom-left; the end (right)
    // handle mirrors it as "-\" with the point at the bottom-right. Sitting low and small, they're easy
    // to avoid while scrubbing the playhead just below them.
    private void DrawHandle(SKCanvas canvas, float x, bool isStart)
    {
        var top = MarkerStripHeight - HandleHeight;
        var half = HandleWidth / 2f;

        using var path = new SKPath();

        path.MoveTo(x - half, top);
        path.LineTo(x + half, top);
        path.LineTo(isStart ? x - half : x + half, MarkerStripHeight);
        path.Close();

        canvas.DrawPath(path, _handlePaint);
    }

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

        foreach (var blob in _rowLabelBlobs)
        {
            blob?.Dispose();
        }

        _rowLabelBlobs = new SKTextBlob?[_activeRows.Length];

        for (var i = 0; i < _activeRows.Length; i++)
        {
            _rowLabelBlobs[i] = SKTextBlob.Create(_activeRows[i].Label, _labelFont, SKPoint.Empty);
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
    /// Emits the in-scope window (microseconds) the other views highlight. With an explicit selection
    /// that is the selected range; otherwise it runs from the axis start up to the playhead.
    /// </summary>
    private void EmitScope()
    {
        long fromUs, toUs;

        if (SelectionActive)
        {
            fromUs = ToUs(Math.Min(_startTime, _endTime));
            toUs = ToUs(Math.Max(_startTime, _endTime));
        }
        else
        {
            fromUs = ToUs(_minTime);
            toUs = ToUs(_playheadTime);
        }

        // Smooth play ticks many times per pixel; only notify when the scope actually changes.
        if (fromUs == _scopeFromUs && toUs == _scopeToUs)
        {
            return;
        }

        _scopeFromUs = fromUs;
        _scopeToUs = toUs;
        ScopeChanged?.Invoke(fromUs, toUs);
    }

    private void FirePlayhead()
    {
        // Emit the scope first so a consumer reacting to the playhead sees the current window.
        EmitScope();

        var playheadUs = ToUs(_playheadTime);

        if (playheadUs != _playheadUs)
        {
            _playheadUs = playheadUs;
            PlayheadTimeChanged?.Invoke(playheadUs);
        }
    }

    private bool IsOnTriangle(double x, double y)
        => y <= MarkerStripHeight + HitArea && Math.Abs(x - PlayheadX) <= TriangleHalfWidth + HitArea;

    private DragTarget HitTest(double x, double y)
    {
        if (y <= MarkerStripHeight + HitArea)
        {
            // The triangle sits on top and is wider, so a press anywhere on it grabs the playhead.
            if (Math.Abs(x - PlayheadX) <= TriangleHalfWidth)
            {
                return DragTarget.Playhead;
            }

            // The from/to handles are only the short triangles at the bottom of the band, so a press
            // higher in the strip (over the ruler) scrubs rather than grabbing a handle.
            if (y >= MarkerStripHeight - HandleHeight - HitArea)
            {
                var dStart = Math.Abs(x - StartDrawX);
                var dEnd = Math.Abs(x - EndDrawX);

                if (dStart <= HitArea && dStart <= dEnd)
                {
                    return DragTarget.Start;
                }

                if (dEnd <= HitArea)
                {
                    return DragTarget.End;
                }
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

        var point = e.GetCurrentPoint(_overlay);

        // Right-click is reserved for the context menu (ContextRequested) - don't scrub or select on it.
        if (point.Properties.IsRightButtonPressed)
        {
            return;
        }

        var position = point.Position;

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
            // Clicking empty space clears the selected operator's row-flow overlay.
            ClearOperatorSelection();
            return;
        }

        if (hit.Value.Event is ExecutionOperatorEvent { PlanNodeIdentifier: { } node })
        {
            // Track the selection so its row-flow path (child→parent, lit while emitting) is drawn.
            _selectedNodeId = node.NodeId;
            _skCanvas.Invalidate();
            PlanNodeSelected?.Invoke(node);
        }
        else
        {
            // A point marker (read/lock/wait/log): reveal that event in the event grid.
            ClearOperatorSelection();
            EventSelected?.Invoke(hit.Value.Event);
        }
    }

    private void ClearOperatorSelection()
    {
        if (_selectedNodeId is null)
        {
            return;
        }

        _selectedNodeId = null;
        _skCanvas.Invalidate();
    }

    /// <summary>
    /// Right-click on a scan/seek operator offers "Open Index" for its underlying index
    /// </summary>
    private void OnContextRequested(UIElement sender, ContextRequestedEventArgs e)
    {
        if (!e.TryGetPosition(_overlay, out var position))
        {
            return;
        }

        var hit = HitTestRegion(position.X, position.Y);

        // Only data-access operators (scan/seek/lookup) that run against a named index get the item.
        if (hit?.Event is not ExecutionOperatorEvent { Category: OperatorCategory.DataAccess, IndexName.Length: > 0 } op)
        {
            return;
        }

        var flyout = new MenuFlyout();
        var openIndex = new MenuFlyoutItem { Text = $"Open Index: {op.IndexName}" };
        openIndex.Click += (_, _) => IndexOpenRequested?.Invoke(op);
        flyout.Items.Add(openIndex);

        flyout.ShowAt(_overlay, new FlyoutShowOptions { Position = position });

        e.Handled = true;
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

                ConfinePlayheadToSelection();

                EmitScope();

                break;

            case DragTarget.End:
                _selectionActivated = true;

                _endTime = Math.Max(t, _startTime);

                ConfinePlayheadToSelection();

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
            _toolTipText.Text = region.Event.Description;
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

    /// <summary>
    /// Zoom on mouse wheel + scale axis
    /// </summary>
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
        var (lo, hi) = ActiveRange;

        _playheadTime = Math.Clamp(XToTime(x), lo, hi);

        SyncHandlesToPlayhead();

        FirePlayhead();
    }

    /// <summary>
    /// Pulls the playhead inside the from/to selection after it changes, so it stays clipped
    /// </summary>
    private void ConfinePlayheadToSelection()
    {
        var (lo, hi) = ActiveRange;
        var clamped = Math.Clamp(_playheadTime, lo, hi);

        if (clamped != _playheadTime)
        {
            _playheadTime = clamped;
            FirePlayhead();
        }
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

        var (rangeStart, rangeEnd) = ActiveRange;

        _playStartTime = rangeStart;
        _playEndTime = rangeEnd;

        if (_playheadTime < rangeStart || _playheadTime >= rangeEnd)
        {
            _playheadTime = rangeStart;
        }

        _isPlaying = true;

        var rangeMs = Math.Max(rangeEnd - rangeStart, 1e-6);

        _playStep = rangeMs * PlayTickMs / BasePlayDurationMs;

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

        if (_playheadTime >= _playEndTime)
        {
            _playheadTime = _playStartTime;
        }

        SyncHandlesToPlayhead();

        FirePlayhead();

        _skCanvas.Invalidate();
    }

    public void Dispose()
    {
        _playTimer.Stop();
        _playTimer.Tick -= OnPlayTimerTick;

        _playButton.Click -= OnPlayButtonClick;
        _stepBackButton.Click -= OnStepBackButtonClick;
        _stepForwardButton.Click -= OnStepForwardButtonClick;
        _threadsButton.Checked -= OnThreadsToggled;
        _threadsButton.Unchecked -= OnThreadsToggled;

        _skCanvas.PaintSurface -= OnPaintSurface;

        _scrollBar.Scroll -= OnScrollBarScroll;

        _overlay.PointerPressed -= OnPointerPressed;
        _overlay.PointerMoved -= OnPointerMoved;
        _overlay.PointerReleased -= OnPointerReleased;
        _overlay.PointerCaptureLost -= OnPointerReleased;
        _overlay.PointerWheelChanged -= OnPointerWheelChanged;
        _overlay.PointerExited -= OnPointerExited;
        _overlay.SizeChanged -= OnOverlaySizeChanged;
        _overlay.ContextRequested -= OnContextRequested;

        _labelFont.Dispose();
        _operatorFont.Dispose();
        _operatorBoldFont.Dispose();

        foreach (var blob in _rowLabelBlobs)
        {
            blob?.Dispose();
        }

        _labelPaint.Dispose();
        _rowBgPaint.Dispose();
        _markerPaint.Dispose();
        _operatorPaint.Dispose();
        _operatorTextPaint.Dispose();
        _playheadPaint.Dispose();
        _playheadFill.Dispose();
        _handlePaint.Dispose();
        _clipDimPaint.Dispose();
        _tickPaint.Dispose();
        _separatorPaint.Dispose();
    }
}
