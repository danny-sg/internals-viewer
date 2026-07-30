using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI;
using InternalsViewer.UI.App.Controls.Timeline.Renderers;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using SkiaSharp.Views.Windows;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Parsing.Plans;

namespace InternalsViewer.UI.App.Controls.Timeline;

/// <summary>
/// Interactive timeline of a query's engine events
/// </summary>
public sealed partial class EventTimelineControl : Grid, IDisposable
{
    // Wider marker for sparse rows (see TimelineRowSet.IsSparse) so their few ticks stay easy to see.
    private const float SparseMarkerWidth = 4f;

    // Default (dense-row) width of a point-event tick.
    private const float MarkerWidth = 1f;
    
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

    private readonly TimelineRowSet _rows = new();

    private bool _showThreads;

    private readonly TimelineAudioPlayer _audioPlayer = new();
    private readonly TimelineTransport _transport;
    private readonly SKXamlCanvas _skCanvas;
    private readonly Canvas _overlay;
    private readonly ScrollBar _scrollBar;
    private readonly Popup _toolTip;
    private readonly TextBlock _toolTipText;

    private readonly List<HitRegion> _hitRegions = [];
    private EngineEvent? _hoverEvent;
    private string? _hoverLabel;

    // The selected operator and the object it accesses; mutated in place so the renderers handed a reference to it at
    // construction always read the current selection.
    private readonly CurrentSelection _selection = new();

    // Shared paint palette and the carved-out lane renderers that draw from it; disposed with the control.
    private readonly RenderResource _renderResource = new();
    private readonly LockRenderer _lockRenderer;
    private readonly MarkerRenderer _markerRenderer;
    private readonly TraceRenderer _traceRenderer;
    private readonly OperatorRenderer _operatorRenderer;
    private readonly TimelineRenderer _timelineRenderer;
    private readonly OverlayRenderer _overlayRenderer;

    private List<EngineEvent> _sortedEvents = [];

    // Pre-filtered and TimeUs-sorted arrays of read groups / LatchEvents; built whenever _sortedEvents
    // changes. Used by PlayAudioForCurrentPosition so the per-frame audio sweep is O(log n + hits) via
    // binary search rather than O(all events).
    private ReadEventGroup[] _readEventsByTime = [];

    private LatchEvent[] _latchEventsByTime = [];

    private FileEvent[] _fileReadEventsByTime = [];

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
    /// Raised when an individual event marker is double clicked (e.g. to open the event's page)
    /// </summary>
    public event Action<EngineEvent>? EventDoubleClicked;

    /// <summary>
    /// Raised when "Open Index" is chosen on a scan/seek operator (carries schema/table/index)
    /// </summary>
    public event Action<ExecutionOperatorEvent>? IndexOpenRequested;

    public event Action<ExecutionOperatorEvent>? ExecutionPlanRequested;

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

        control._sortedEvents = [.. ExpandGroupedEvents(events).OrderBy(ev => ev.SequenceId)];
        control._readEventsByTime = [.. events.OfType<ReadEventGroup>().OrderBy(read => read.TimeUs)];
        control._latchEventsByTime = [.. events.OfType<LatchEvent>().OrderBy(latch => latch.TimeUs)];
        control._fileReadEventsByTime = [.. EnumerateFileReads(events).OrderBy(read => read.TimeUs)];

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
    /// <remarks>
    /// The single place audio is built, whether it was switched on from the transport or set programmatically - the
    /// transport toggle routes through the property rather than initialising alongside it.
    /// </remarks>
    private static async void OnIsAudioEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (!(bool)e.NewValue)
        {
            return;
        }

        var control = (EventTimelineControl)d;

        control._transport.SetAudioLoading(true);

        try
        {
            await control._audioPlayer.EnsureInitializedAsync();
        }
        catch
        {
            // No-op - Audio is non-critical
        }
        finally
        {
            control._transport.SetAudioLoading(false);
        }
    }
#pragma warning restore VSTHRD100

    public EventTimelineControl()
    {
        _lockRenderer = new LockRenderer(_renderResource, _selection, _hitRegions);
        _markerRenderer = new MarkerRenderer(_renderResource, _selection, _hitRegions);
        _traceRenderer = new TraceRenderer(_renderResource, _selection);
        _operatorRenderer = new OperatorRenderer(_renderResource, _selection, _hitRegions);
        _timelineRenderer = new TimelineRenderer(_renderResource);
        _overlayRenderer = new OverlayRenderer(_renderResource);

        Background = new SolidColorBrush(Colors.Transparent);

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _transport = new TimelineTransport();

        _transport.PlayPauseRequested += OnPlayPauseRequested;
        _transport.StepRequested += OnStepRequested;
        _transport.PlaySpeedChanged += OnPlaySpeedChanged;
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
        _selection.Clear();

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
        _transport.PlaySpeedChanged -= OnPlaySpeedChanged;
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

        _rows.Dispose();

        _lockRenderer.Dispose();
        _operatorRenderer.Dispose();
        _timelineRenderer.Dispose();
        _overlayRenderer.Dispose();
        _renderResource.Dispose();

        _staticLayer?.Dispose();

        _audioPlayer.Dispose();

        _hitRegions.Clear();
        _hoverEvent = null;
        _selection.Clear();

        _sortedEvents = [];
        _readEventsByTime = [];
        _latchEventsByTime = [];
        _fileReadEventsByTime = [];
        _times = [];
        _orderedOperators = [];
    }
}