using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI;
using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Query.Plans;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Events.Operators;

namespace InternalsViewer.UI.App.Controls.Timeline;

/// <summary>
/// Interactive timeline of a query's engine events
/// </summary>
public sealed partial class EventTimelineControl : Grid, IDisposable
{



    private const double HitArea = 7;
    private const long DoubleClickMs = 300;

    private const float VerticalLabelPad = 1f;

    // Wider marker for sparse rows (see TimelineRowSet.IsSparse) so their few ticks stay easy to see.
    private const float SparseMarkerWidth = 4f;

    // Pixel separation between a read's per-page return rails, which bunch at its end (the pages hit the buffer together
    // on I/O completion) — purely for legibility, not real timing.
    private const float PageRailGapPx = 3f;

    // Minimum start-to-end pixel gap for the dotted call rail to be worth drawing: on a short read the call rail (at the
    // start) and the solid return rail (at the end) would sit on top of each other, so below this the return rail alone
    // stands for the read.
    private const float MinCallRailGapPx = 4f;

    // Opacity of the I/O trace extensions (Trace I/O mode) — faint so they read as a background hint.
    private const byte TraceAlpha = 90;

    // When an operator is selected, markers and trace extensions belonging to other operators fade to
    // this alpha so the selected block's I/O and trace lines stand out.
    private const byte FocusedDimAlpha = 70;

    // Locks are drawn semi-transparent so concurrent, overlapping holds all show through instead of the longest one
    // opaquely hiding the shorter ones beneath it.
    private const byte LockOverlayAlpha = 150;

    // Lock escalation granularity levels for the staircase: row (rid/key) at the bottom, page in the middle, object
    // (object/hobt) at the top — escalation climbs these over time.
    private const int LockLevels = 3;

    private const byte DurationOverlayAlpha = 96;

    private const double MinZoom = 1.0;
    private const double MaxZoom = 400.0;
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

    private static readonly SKColor OperatorLabelColour = new(235, 235, 235);

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

    private const float MinLabelBarHeight = 11f;
    private const float MinLabelBarWidth = 26f;

    private const float OperatorMaxFont = 12f;
    private const float OperatorMinFont = 7f;

    private const float ObjectMarkerMargin = 3f;
    private const float ObjectMarkerRadius = 6f;
    private const float ObjectMarkerBandWidth = 12f;

    private const float StatementBandWeight = 0.5f;

    private const float MinCostWeight = 0.35f;
    private const float MaxCostWeight = 1.5f;

    private const float GradientLift = 0.04f;

    private static readonly SKColor ConsumeShadeColour = new(0, 0, 0, 115);

    private const float OperatorLabelGapFraction = 0.5f;

    private const float TwoLineGap = 2f;

    private static readonly SKColor FlowConnectorColour = new(120, 200, 255, 70);
    private static readonly SKColor FlowPathColour = new(200, 200, 200, 200);
    private static readonly SKColor FlowSelectedColour = new(255, 255, 255, 230);

    private readonly TimelineRowSet _rows = new();

    private bool _showThreads;

    private readonly TimelineAudioPlayer _audioPlayer = new();
    private readonly TimelineTransport _transport;
    private readonly SKXamlCanvas _skCanvas;
    private readonly Canvas _overlay;
    private readonly ScrollBar _scrollBar;
    private readonly Popup _toolTip;
    private readonly TextBlock _toolTipText;

    private readonly List<(SKRect Bounds, EngineEvent Event, string? Label)> _hitRegions = [];
    private EngineEvent? _hoverEvent;
    private string? _hoverLabel;

    private int? _selectedNodeId;

    private string _selectedSchema = string.Empty;
    private string _selectedTable = string.Empty;

    private readonly SKFont _labelFont = new(SKTypeface.Default, 10f);

    private readonly SKFont _operatorFont = new(SKTypeface.Default, 12f);
    private readonly SKFont _operatorBoldFont = new(SKTypeface.FromFamilyName(SKTypeface.Default.FamilyName, SKFontStyle.Bold),
                                                    10f);

    private readonly SKPaint _labelPaint = new()
    {
        Color = SKColors.LightGray,
        IsAntialias = true,
    };

    private readonly SKPaint _rowBackgroundPaint = new() { Style = SKPaintStyle.Fill };
    
    private readonly SKPaint _markerPaint = new() { Style = SKPaintStyle.Fill };
    
    private readonly SKPaint _operatorPaint = new()
    {
        Color = SKColors.LimeGreen,
        Style = SKPaintStyle.Fill,
        IsAntialias = true,
    };

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

    private readonly SKPaint _traceLayerPaint = new() { Color = SKColors.White.WithAlpha(TraceAlpha) };

    private readonly SKPaint _flowConnectorPaint = new() { Style = SKPaintStyle.Fill, IsAntialias = true };
    
    private readonly SKPaint _outlinePaint = new() { Style = SKPaintStyle.Stroke, IsAntialias = true };

    private readonly SKPaint _readBoundaryPaint = new()
    {
        StrokeWidth = 1,
        Style = SKPaintStyle.Stroke,
        IsAntialias = false,
        PathEffect = SKPathEffect.CreateDash([2f, 2f], 0f),
    };

    private readonly SKPaint _readReturnPaint = new()
    {
        StrokeWidth = 1.5f,
        Style = SKPaintStyle.Stroke,
        IsAntialias = false,
    };

    private List<EngineEvent> _sortedEvents = [];

    // Pre-filtered and TimeUs-sorted arrays of read groups / LatchEvents; built whenever _sortedEvents
    // changes. Used by PlayAudioForCurrentPosition so the per-frame audio sweep is O(log n + hits) via
    // binary search rather than O(all events).
    private ReadEventGroup[] _readEventsByTime = [];

    private LatchEvent[] _latchEventsByTime = [];

    // The markers/operators/traces/ruler don't change as the playhead sweeps, so they're recorded once
    // into a picture and replayed each frame; only the playhead, handles and selection dim are redrawn
    // live. The picture (and the _hitRegions built alongside it) is re-recorded only when an input that
    // affects the static content changes - see StaticLayerKey. This keeps the per-frame cost of playback
    // and hover off the O(event count) draw path.

    // Bumped whenever the event set (and the layout derived from it) is rebuilt, so the cached static
    // layer is invalidated without having to compare the events themselves.
    private int _eventsVersion;

    private List<double> _times = [];

    private List<(int Index, ExecutionOperatorEvent Op)> _orderedOperators = [];

    private double _maxCost;

    private long _maxRows;

    private double _minTime;

    private double _maxTime;

    private double _timeRange;

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

    // Last emitted values (microseconds), kept only to suppress duplicate notifications during play.
    private long _scopeFromUs;
    private long _scopeToUs = -1;
    private long _playheadUs = -1;

    /// <summary>
    /// Raised when the in-scope window changes, as microsecond times (from, to). The control deals only
    /// in time - mapping a time to events/sequences/pages is the consumer's responsibility.
    /// </summary>
    public event Action<long, long>? ScopeChanged;

    /// <summary>
    /// Raised when a plan operator in the timeline is clicked
    /// </summary>
    public event Action<PlanNodeIdentifier>? PlanNodeSelected;

    /// <summary>
    /// Raised when an individual event marker is clicked (to reveal it in the event grid).
    /// </summary>
    public event Action<EngineEvent>? EventSelected;

    /// <summary>
    /// Raised when "Open Index" is chosen on a scan/seek operator (carries schema/table/index)
    /// </summary>
    public event Action<ExecutionOperatorEvent>? IndexOpenRequested;

    public List<EngineEvent> Events
    {
        get => (List<EngineEvent>)GetValue(EventsProperty);
        set => SetValue(EventsProperty, value);
    }

    public static readonly DependencyProperty EventsProperty =
        DependencyProperty.Register(nameof(Events), typeof(List<EngineEvent>), typeof(EventTimelineControl),
            new PropertyMetadata(new List<EngineEvent>(), OnEventsChanged));

    // Lock/latch/wait events are always captured (the read grouping needs them); these decide only whether their band
    // is shown on the timeline. Off → the band is dropped and its markers (top-level AND read-group members) are
    // skipped, since TimelineRowSet.IndexOf returns -1 for an event whose row isn't active.

    public bool ShowLocks
    {
        get => (bool)GetValue(ShowLocksProperty);
        set => SetValue(ShowLocksProperty, value);
    }

    public static readonly DependencyProperty ShowLocksProperty =
        DependencyProperty.Register(nameof(ShowLocks), typeof(bool), typeof(EventTimelineControl),
            new PropertyMetadata(true, OnRowVisibilityChanged));

    public bool ShowLatches
    {
        get => (bool)GetValue(ShowLatchesProperty);
        set => SetValue(ShowLatchesProperty, value);
    }

    public static readonly DependencyProperty ShowLatchesProperty =
        DependencyProperty.Register(nameof(ShowLatches), typeof(bool), typeof(EventTimelineControl),
            new PropertyMetadata(true, OnRowVisibilityChanged));

    public bool ShowWaits
    {
        get => (bool)GetValue(ShowWaitsProperty);
        set => SetValue(ShowWaitsProperty, value);
    }

    public static readonly DependencyProperty ShowWaitsProperty =
        DependencyProperty.Register(nameof(ShowWaits), typeof(bool), typeof(EventTimelineControl),
            new PropertyMetadata(true, OnRowVisibilityChanged));

    private static void OnRowVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (EventTimelineControl)d;

        control.RebuildRows();

        // Bump the version so the cached static layer is re-recorded with the new set of bands.
        control._eventsVersion++;

        control._skCanvas.Invalidate();
    }

    /// <summary>Resolves each event's display colour on demand (colours aren't stored on the events).</summary>
    public EventColourProvider? ColourProvider
    {
        get => (EventColourProvider?)GetValue(ColourProviderProperty);
        set => SetValue(ColourProviderProperty, value);
    }

    public static readonly DependencyProperty ColourProviderProperty =
        DependencyProperty.Register(nameof(ColourProvider), typeof(EventColourProvider), typeof(EventTimelineControl),
            new PropertyMetadata(null, OnColourProviderChanged));

    private static void OnColourProviderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (EventTimelineControl)d;

        // Colours are baked into the cached static layer, so it must be re-recorded when they change.
        control._eventsVersion++;
        control._skCanvas.Invalidate();
    }

    private static void OnEventsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (EventTimelineControl)d;
        var events = (List<EngineEvent>)e.NewValue;

        // A read is shown as its consolidated group, but its underlying NON-IO members (the latches, waits and locks
        // folded into it) are also surfaced onto their own bands, so e.g. a PAGEIOLATCH wait or a BUF latch is visible
        // in the context of the read it belongs to. The IO members (physical / file reads) stay inside the group. Only
        // the render list gains the members; the audio arrays stay the top-level events so playback isn't flooded.
        control._sortedEvents = [.. ExpandGroupedEvents(events).OrderBy(ev => ev.SequenceId)];
        control._readEventsByTime = [.. events.OfType<ReadEventGroup>().OrderBy(read => read.TimeUs)];
        control._latchEventsByTime = [.. events.OfType<LatchEvent>().OrderBy(latch => latch.TimeUs)];

        control._eventsVersion++;

        control.RebuildRows();
        control.BuildTimes();
        control.BuildOperatorLayout();

        control.StopPlay();
        control.Reset();

        control._skCanvas.Invalidate();
    }

    public bool IsAudioEnabled
    {
        get => (bool)GetValue(IsAudioEnabledProperty);
        set => SetValue(IsAudioEnabledProperty, value);
    }

    public static readonly DependencyProperty IsAudioEnabledProperty =
        DependencyProperty.Register(nameof(IsAudioEnabled), typeof(bool), typeof(EventTimelineControl),
            new PropertyMetadata(false, OnIsAudioEnabledChanged));

#pragma warning disable VSTHRD100 // Avoid async void methods - Required for event and using try/catch for safety
    private static async void OnIsAudioEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue)
        {
            try
            {
                await ((EventTimelineControl)d)._audioPlayer.EnsureInitializedAsync();
            }
            catch
            {
                // No-op
            }
        }
    }
#pragma warning restore VSTHRD100

    public EventTimelineControl()
    {
        Background = new SolidColorBrush(Colors.Transparent);

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _transport = new TimelineTransport();
        _transport.PlayPauseRequested += OnPlayPauseRequested;
        _transport.StepRequested += OnStepRequested;
        _transport.ThreadsToggled += OnThreadsToggled;
        _transport.AudioToggled += OnAudioToggled;

        SetRow(_transport, 0);
        Children.Add(_transport);

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

    private void Reset()
    {
        _zoom = MinZoom;
        _scrollX = 0;
        _selectedNodeId = null;
        _selectedSchema = string.Empty;
        _selectedTable = string.Empty;

        _selectionActivated = false;
        _playheadTime = _minTime;
        _startTime = _playheadTime;
        _endTime = _playheadTime;

        _scopeFromUs = ToUs(_minTime);
        _scopeToUs = ToUs(_playheadTime);
        _playheadUs = _scopeToUs;

        UpdateScrollBar();
    }

    public void Dispose()
    {
        _playTimer.Stop();
        _playTimer.Tick -= OnPlayTimerTick;

        _transport.PlayPauseRequested -= OnPlayPauseRequested;
        _transport.StepRequested -= OnStepRequested;
        _transport.ThreadsToggled -= OnThreadsToggled;
        _transport.AudioToggled -= OnAudioToggled;
        _transport.Dispose();

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

        _rows.Dispose();

        _labelPaint.Dispose();
        _rowBackgroundPaint.Dispose();
        _markerPaint.Dispose();
        _operatorPaint.Dispose();
        _operatorTextPaint.Dispose();
        _playheadPaint.Dispose();
        _playheadFill.Dispose();
        _handlePaint.Dispose();
        _clipDimPaint.Dispose();
        _tickPaint.Dispose();
        _separatorPaint.Dispose();
        _traceLayerPaint.Dispose();
        _flowConnectorPaint.Dispose();
        _outlinePaint.Dispose();
        _readBoundaryPaint.Dispose();
        _readReturnPaint.Dispose();

        _pathBuilder.Dispose();

        _staticLayer?.Dispose();

        _audioPlayer.Dispose();
    }
}