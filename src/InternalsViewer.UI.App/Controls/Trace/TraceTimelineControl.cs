using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Helpers;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.CallStack;
using InternalsViewer.UI.App.ViewModels.Query.CallStack;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using Windows.UI;

namespace InternalsViewer.UI.App.Controls.Trace;

public sealed partial class TraceTimelineControl : Grid, IPlayheadChart, IDisposable
{
    private const double MinimumRangeSteps = 20;

    private const double FitPadding = 30;

    private const string MemoryModeGroup = "MemoryMode";

    private const string MemoryMenuGroup = "TraceTimelineMemoryMenu";

    public static readonly DependencyProperty TimelineProperty =
        DependencyProperty.Register(nameof(Timeline),
                                    typeof(TraceTimeline),
                                    typeof(TraceTimelineControl),
                                    new PropertyMetadata(null, OnTimelineChanged));

    public TraceTimeline? Timeline
    {
        get => (TraceTimeline?)GetValue(TimelineProperty);
        set => SetValue(TimelineProperty, value);
    }

    public static readonly DependencyProperty FullTimelineProperty =
        DependencyProperty.Register(nameof(FullTimeline),
                                    typeof(TraceTimeline),
                                    typeof(TraceTimelineControl),
                                    new PropertyMetadata(null, OnFullTimelineChanged));

    public TraceTimeline? FullTimeline
    {
        get => (TraceTimeline?)GetValue(FullTimelineProperty);
        set => SetValue(FullTimelineProperty, value);
    }

    public static readonly DependencyProperty AxisProperty =
        DependencyProperty.Register(nameof(Axis),
                                    typeof(TraceTimelineAxis),
                                    typeof(TraceTimelineControl),
                                    new PropertyMetadata(TraceTimelineAxis.Position, OnAxisChanged));

    public TraceTimelineAxis Axis
    {
        get => (TraceTimelineAxis)GetValue(AxisProperty);
        set => SetValue(AxisProperty, value);
    }

    public static readonly DependencyProperty HiddenCategoriesProperty =
        DependencyProperty.Register(nameof(HiddenCategories),
                                    typeof(IReadOnlySet<string>),
                                    typeof(TraceTimelineControl),
                                    new PropertyMetadata(null, OnHiddenCategoriesChanged));

    public IReadOnlySet<string>? HiddenCategories
    {
        get => (IReadOnlySet<string>?)GetValue(HiddenCategoriesProperty);
        set => SetValue(HiddenCategoriesProperty, value);
    }

    public static readonly DependencyProperty PrunedCategoriesProperty =
        DependencyProperty.Register(nameof(PrunedCategories),
                                    typeof(IReadOnlySet<string>),
                                    typeof(TraceTimelineControl),
                                    new PropertyMetadata(null, OnHiddenCategoriesChanged));

    public IReadOnlySet<string>? PrunedCategories
    {
        get => (IReadOnlySet<string>?)GetValue(PrunedCategoriesProperty);
        set => SetValue(PrunedCategoriesProperty, value);
    }

    public static readonly DependencyProperty SelectedNodeProperty =
        DependencyProperty.Register(nameof(SelectedNode),
                                    typeof(CallStackNode),
                                    typeof(TraceTimelineControl),
                                    new PropertyMetadata(null, OnSelectedNodeChanged));

    public CallStackNode? SelectedNode
    {
        get => (CallStackNode?)GetValue(SelectedNodeProperty);
        set => SetValue(SelectedNodeProperty, value);
    }

    public static readonly DependencyProperty ShowMemoryProperty =
        DependencyProperty.Register(nameof(ShowMemory),
                                    typeof(bool),
                                    typeof(TraceTimelineControl),
                                    new PropertyMetadata(false, OnShowMemoryChanged));

    public bool ShowMemory
    {
        get => (bool)GetValue(ShowMemoryProperty);
        set => SetValue(ShowMemoryProperty, value);
    }

    public static readonly DependencyProperty ShowCallStackProperty =
        DependencyProperty.Register(nameof(ShowCallStack),
                                    typeof(bool),
                                    typeof(TraceTimelineControl),
                                    new PropertyMetadata(false, OnShowCallStackChanged));

    public bool ShowCallStack
    {
        get => (bool)GetValue(ShowCallStackProperty);
        set => SetValue(ShowCallStackProperty, value);
    }

    public static readonly DependencyProperty AutoZoomProperty =
        DependencyProperty.Register(nameof(AutoZoom),
                                    typeof(bool),
                                    typeof(TraceTimelineControl),
                                    new PropertyMetadata(false));

    public bool AutoZoom
    {
        get => (bool)GetValue(AutoZoomProperty);
        set => SetValue(AutoZoomProperty, value);
    }

    public static readonly DependencyProperty GrantedMemoryProperty =
        DependencyProperty.Register(nameof(GrantedMemory),
                                    typeof(long),
                                    typeof(TraceTimelineControl),
                                    new PropertyMetadata(0L, OnGrantedMemoryChanged));

    public long GrantedMemory
    {
        get => (long)GetValue(GrantedMemoryProperty);
        set => SetValue(GrantedMemoryProperty, value);
    }

    public static readonly DependencyProperty MemoryModeProperty =
        DependencyProperty.Register(nameof(MemoryMode),
                                    typeof(TraceTimelineMemoryMode),
                                    typeof(TraceTimelineControl),
                                    new PropertyMetadata(TraceTimelineMemoryMode.Allocated, OnMemoryModeChanged));

    public TraceTimelineMemoryMode MemoryMode
    {
        get => (TraceTimelineMemoryMode)GetValue(MemoryModeProperty);
        set => SetValue(MemoryModeProperty, value);
    }

    public static readonly DependencyProperty SelectedCallProperty =
        DependencyProperty.Register(nameof(SelectedCall),
                                    typeof(CallReference),
                                    typeof(TraceTimelineControl),
                                    new PropertyMetadata(null, OnSelectedCallChanged));

    public CallReference? SelectedCall
    {
        get => (CallReference?)GetValue(SelectedCallProperty);
        set => SetValue(SelectedCallProperty, value);
    }

    private readonly SKXamlCanvas _canvas;

    private readonly Canvas _overlay;

    private readonly UserControl _keyboardHost;

    private readonly ScrollBar _horizontalScrollBar;

    private readonly ScrollBar _verticalScrollBar;

    private readonly Popup _toolTip;

    private readonly DropDownButton _memoryModeButton;

    private readonly RadioMenuFlyoutItem _allocatedItem;

    private readonly RadioMenuFlyoutItem _inUseItem;

    private readonly TextBlock _toolTipText;

    private readonly TraceTimelinePaints _paints = new();

    private readonly TraceOperatorsPane _operators;

    private readonly TraceReadsPane _reads;

    private readonly SKPathBuilder _pathBuilder = new();

    private readonly SKPath _playheadTriangle = PlayheadTriangle();

    private readonly Dictionary<int, SKColor> _colours = [];

    private readonly Dictionary<int, string> _labels = [];

    private readonly Dictionary<string, SKColor> _parsedColours = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<FlameHit, MemoryInUseCurve> _inUseCurves = [];

    private TraceTimeline? _timeline;

    private CallStackNode? _highlightedNode;

    private CallStackNode? _root;

    private TraceTimeline? _rooted;

    private TraceTimeline? _visible;

    private TraceTimelineAxis _axis = TraceTimelineAxis.Position;

    private float[] _laneTops = [];

    private uint? _expandedThread;

    private float _focusPadding;

    private double? _playhead;

    private FlameHit? _stackFrame;

    private FlameHit? _reportedStackFrame;

    private FlameHit? _reportedSelected;

    private int _reportedVersion = -1;

    private TraceTimelineAxis _reportedAxis;

    private ulong _selfMaximum;

    private ulong _inUseMaximum;

    private Dictionary<(int Lane, int Depth), RaisedRow> _raisedRows = [];

    private float _contentHeight;

    private double _fitStart;

    private double _fitEnd = 1;

    private double _viewStart;

    private double _viewEnd = 1;

    private double _scrollY;

    private int _version;

    public TraceTimelineControl()
    {
        _operators = new TraceOperatorsPane(this, () => (OperatorsTop, OperatorsHeight));

        _reads = new TraceReadsPane(this, () => (ReadsTop, ReadsHeight));

        Background = new SolidColorBrush(Colors.Transparent);

        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _canvas = new SKXamlCanvas { IgnorePixelScaling = false };
        _canvas.PaintSurface += OnPaintSurface;

        Children.Add(_canvas);

        _overlay = new Canvas { Background = new SolidColorBrush(Colors.Transparent) };

        _keyboardHost = new UserControl
        {
            Content = _overlay,
            IsTabStop = true,
            UseSystemFocusVisuals = false
        };

        Children.Add(_keyboardHost);

        Children.Add(_minimap);

        _horizontalScrollBar = new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            Visibility = Visibility.Collapsed,
            IndicatorMode = ScrollingIndicatorMode.MouseIndicator,
            Minimum = 0
        };

        _horizontalScrollBar.Scroll += OnHorizontalScroll;

        SetRow(_horizontalScrollBar, 1);

        Children.Add(_horizontalScrollBar);

        _verticalScrollBar = new ScrollBar
        {
            Orientation = Orientation.Vertical,
            Visibility = Visibility.Collapsed,
            IndicatorMode = ScrollingIndicatorMode.MouseIndicator,
            Minimum = 0
        };

        _verticalScrollBar.Scroll += OnVerticalScroll;

        SetColumn(_verticalScrollBar, 1);

        Children.Add(_verticalScrollBar);

        _toolTipText = new TextBlock
        {
            Foreground = new SolidColorBrush(Colors.White),
            FontSize = 11,
            Margin = new Thickness(6, 3, 6, 3)
        };

        _toolTip = new Popup
        {
            IsHitTestVisible = false,
            Child = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(235, 30, 30, 30)),
                CornerRadius = new CornerRadius(3),
                IsHitTestVisible = false,
                Child = _toolTipText
            }
        };

        _overlay.Children.Add(_toolTip);

        _allocatedItem = new RadioMenuFlyoutItem
        {
            Text = "Allocated",
            GroupName = MemoryModeGroup,
            Tag = TraceTimelineMemoryMode.Allocated,
            IsChecked = true
        };

        _allocatedItem.Click += OnMemoryModeClick;

        _inUseItem = new RadioMenuFlyoutItem { Text = "In Use", GroupName = MemoryModeGroup, Tag = TraceTimelineMemoryMode.InUse };

        _inUseItem.Click += OnMemoryModeClick;

        var memoryModes = new MenuFlyout();

        memoryModes.Items.Add(_allocatedItem);
        memoryModes.Items.Add(_inUseItem);

        _memoryModeButton = new DropDownButton
        {
            Content = MemoryModeLabel(TraceTimelineMemoryMode.Allocated),
            Flyout = memoryModes,
            FontSize = 12,
            MinHeight = 0,
            Padding = new Thickness(8, 2, 8, 3),
            Margin = new Thickness(LabelPadding * 2, 3, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Visibility = Visibility.Collapsed
        };

        ToolTipService.SetToolTip(_memoryModeButton,
                                  "Allocated raises each call by the memory it allocated itself. In Use raises a surface along "
                                  + "each call showing the memory it has allocated and not yet freed as it runs.");

        Children.Add(_memoryModeButton);

        _overlay.PointerPressed += OnPointerPressed;
        _keyboardHost.ContextRequested += OnContextRequested;

        _keyboardHost.KeyDown += OnKeyDown;
        _overlay.PointerMoved += OnPointerMoved;
        _overlay.PointerReleased += OnPointerReleased;
        _overlay.PointerCaptureLost += OnPointerCaptureLost;
        _overlay.PointerWheelChanged += OnPointerWheelChanged;
        _overlay.PointerExited += OnPointerExited;
        _overlay.SizeChanged += OnOverlaySizeChanged;

        Loaded += OnLoaded;

        ActualThemeChanged += OnActualThemeChanged;
    }

    public event Action<CallStackNode, int>? CallSelected;

    public event Action? SelectionCleared;

    public event Action<CallStackNode?, int>? CallTreeOpenRequested;

    public event Action? CallStackOpenRequested;

    public event Action? CallStackChanged;

    public bool HasCallStack => GetStackPath().Count > 0;

    private double FullStart => _fitStart;

    private double FullEnd => Math.Max(_fitEnd, FullStart + MinimumRange);

    private double MinimumRange => MinimumRangeSteps * (_timeline?.StepOf(_axis) ?? 1);

    private double ViewportHeight => Math.Max(0, _overlay.ActualHeight - ContentTop - BandHeight);

    private float RulerTop => ShowMemory ? MemoryBarHeight : 0;

    private float ContentTop => OperatorsTop + PanesArea;

    private bool ShowsSpikes => ShowMemory && MemoryMode == TraceTimelineMemoryMode.Allocated;

    private bool ShowsMemoryBand => ShowMemory && _axis == TraceTimelineAxis.Position && _visible is { Memory.HasAllocations: true };

    private float BandHeight => ShowsMemoryBand ? MemoryBandHeight : 0;

    public void ZoomToFit()
    {
        _scrollY = 0;

        SetView(FullStart, FullEnd);
    }

    public void ZoomTo(double start, double end)
    {
        var margin = (end - start) * 0.05;

        SetView(start - margin, end + margin);
    }

    public void ScopeTo(CallStackNode node) => SetRoot(node);

    public IReadOnlyList<TraceThreadStack> GetCallStack()
    {
        var path = GetStackPath();

        if (path.Count == 0 || _visible is not { } timeline)
        {
            return [];
        }

        var thread = timeline.Threads[path[0].Lane];

        var calls = new List<TraceStackCall>(path.Count);

        foreach (var hit in path)
        {
            var row = thread.Rows[hit.Depth];

            var instructions = row.Span(TraceTimelineAxis.Instructions, hit.Index);

            calls.Add(new TraceStackCall(row.Span(_axis, hit.Index), instructions.End - instructions.Start));
        }

        return [new TraceThreadStack(thread.ThreadId, calls)];
    }

    public CallReference? GetSelectedCall() => GetCallReference(_selected);

    public void SelectAdjacentCall(bool forward)
    {
        if (_selected is { } hit
            && _visible is { } timeline
            && hit.Lane < timeline.Threads.Count
            && GetAdjacentCall(timeline.Threads[hit.Lane].Rows, hit, forward) is { } next)
        {
            SelectAndShow(next);
        }
    }

    public void StepPlayhead(bool forward, PlayheadStepMode mode)
    {
        switch (mode)
        {
            case PlayheadStepMode.SameCall when _nodeMatches is { } matches:
                StepTo(forward, matches, allRows: true);
                break;

            case PlayheadStepMode.Batch:
                StepToMark(forward, isBatch: true);
                break;

            case PlayheadStepMode.Row:
                StepToMark(forward, isBatch: false);
                break;

            default:
                StepTo(forward, matches: null, allRows: false);
                break;
        }
    }

    public void Dispose()
    {
        StopPlay();

        if (_playTimer is not null)
        {
            _playTimer.Tick -= OnPlayTick;
        }

        Loaded -= OnLoaded;

        ActualThemeChanged -= OnActualThemeChanged;

        _canvas.PaintSurface -= OnPaintSurface;

        _horizontalScrollBar.Scroll -= OnHorizontalScroll;
        _verticalScrollBar.Scroll -= OnVerticalScroll;

        _overlay.PointerPressed -= OnPointerPressed;
        _keyboardHost.ContextRequested -= OnContextRequested;

        _keyboardHost.KeyDown -= OnKeyDown;
        _overlay.PointerMoved -= OnPointerMoved;
        _overlay.PointerReleased -= OnPointerReleased;
        _overlay.PointerCaptureLost -= OnPointerCaptureLost;
        _overlay.PointerWheelChanged -= OnPointerWheelChanged;
        _overlay.PointerExited -= OnPointerExited;
        _overlay.SizeChanged -= OnOverlaySizeChanged;

        _staticLayer?.Dispose();
        _staticLayer = null;

        _landscapeLayer?.Dispose();
        _landscapeLayer = null;

        _minimap.Dispose();

        _allocatedItem.Click -= OnMemoryModeClick;
        _inUseItem.Click -= OnMemoryModeClick;

        _paints.Dispose();

        _pathBuilder.Dispose();

        _playheadTriangle.Dispose();

        _operators.Dispose();

        _timeline = null;
        _rooted = null;
        _visible = null;
        _colours.Clear();
        _labels.Clear();
        _parsedColours.Clear();
    }

    private double OriginOf(TraceTimelineAxis axis) => _timeline?.StartOf(axis) ?? 0;

    private void StepTo(bool forward, bool[]? matches, bool allRows)
    {
        if (_visible is not { } timeline)
        {
            return;
        }

        var from = _playhead ?? _viewStart;

        double? next = null;

        foreach (var thread in timeline.Threads)
        {
            var depth = allRows ? thread.Rows.Count : Math.Min(DepthOf(thread), thread.Rows.Count);

            foreach (var row in thread.Rows.Take(depth))
            {
                next = (forward ? FindStartAfter(row, matches, from, next) : FindStartBefore(row, matches, from, next)) ?? next;
            }
        }

        if (next is { } target)
        {
            PlacePlayhead(target);
        }
    }

    private void StepToMark(bool forward, bool isBatch)
    {
        if (RowReturnMarks is not { } marks)
        {
            return;
        }

        var from = _playhead ?? _viewStart;

        double? next = null;

        foreach (var row in _operators.Rows.Where(r => marks.IsBatch(r.Operator) == isBatch))
        {
            foreach (var lifetime in row.Lifetimes)
            {
                var values = marks.GetMarks(row.Operator, lifetime.Thread, _axis, RowReturnType.PassedUp);

                next = forward ? FindMarkAfter(values, from, next) : FindMarkBefore(values, from, next);
            }
        }

        if (next is { } target)
        {
            PlacePlayhead(target);
        }
    }

    private static double? FindMarkAfter(ReadOnlySpan<double> values, double from, double? best)
    {
        var index = SortedSearch.FirstAfter(values, from);

        if (index >= values.Length)
        {
            return best;
        }

        return best is { } limit && values[index] >= limit ? best : values[index];
    }

    private static double? FindMarkBefore(ReadOnlySpan<double> values, double from, double? best)
    {
        var index = SortedSearch.FirstAtOrAfter(values, from) - 1;

        if (index < 0)
        {
            return best;
        }

        return best is { } limit && values[index] <= limit ? best : values[index];
    }

    private double? FindStartAfter(TraceTimelineRow row, bool[]? matches, double from, double? best)
    {
        var starts = row.Starts(_axis);

        for (var index = row.FirstStartingAfter(_axis, from); index >= 0 && index < row.Count; index++)
        {
            if (best is { } limit && starts[index] >= limit)
            {
                return null;
            }

            if (!Excludes(matches, row.NodeAt(index)))
            {
                return starts[index];
            }
        }

        return null;
    }

    private double? FindStartBefore(TraceTimelineRow row, bool[]? matches, double from, double? best)
    {
        var starts = row.Starts(_axis);

        for (var index = row.FirstStartingFrom(_axis, from, 0) - 1; index >= 0 && index < row.Count; index--)
        {
            if (best is { } limit && starts[index] <= limit)
            {
                return null;
            }

            if (!Excludes(matches, row.NodeAt(index)))
            {
                return starts[index];
            }
        }

        return null;
    }

    private void PlacePlayhead(double target)
    {
        _playhead = target;

        if (target < _viewStart || target > _viewEnd)
        {
            CentreViewOn(target);
        }

        _canvas.Invalidate();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyTheme();

        WatchKeyboard();
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args) => ApplyTheme();

    private void ApplyTheme()
    {
        _paints.Apply(ActualTheme == ElementTheme.Dark);

        Redraw();
    }

    private void Redraw()
    {
        _version++;

        _canvas.Invalidate();
    }

    private void Relayout()
    {
        BuildLayout();

        ClampScroll();

        UpdateScrollBars();

        Redraw();
    }

    private void UpdateFitRange()
    {
        if (_rooted is { Threads.Count: > 0 } rooted)
        {
            _fitStart = rooted.Threads.Min(t => t.StartOf(_axis));

            _fitEnd = rooted.Threads.Max(t => t.EndOf(_axis));

            return;
        }

        _fitStart = _timeline?.StartOf(_axis) ?? 0;

        _fitEnd = _timeline?.EndOf(_axis) ?? 1;
    }

    private void SetView(double start, double end)
    {
        var fullStart = FullStart;

        var fullEnd = FullEnd;

        var fullRange = Math.Max(fullEnd - fullStart, MinimumRange);

        var range = Math.Min(Math.Max(end - start, MinimumRange), fullRange);

        if (range >= fullRange)
        {
            var content = _overlay.ActualWidth - FitPadding * 2;

            var margin = content > 0 ? fullRange * FitPadding / content : 0;

            _viewStart = fullStart - margin;
            _viewEnd = fullStart + fullRange + margin;
        }
        else
        {
            start = Math.Max(fullStart, Math.Min(start, fullEnd - range));

            _viewStart = start;
            _viewEnd = start + range;
        }

        ApplyScroll();
    }

    private void ScrollViewTo(double start) => SetView(start, start + (_viewEnd - _viewStart));

    private void CentreViewOn(double position) => ScrollViewTo(position - (_viewEnd - _viewStart) / 2);

    private void ApplyScroll()
    {
        ClampScroll();

        UpdateScrollBars();

        _canvas.Invalidate();
    }

    private void ClampScroll() => _scrollY = Math.Clamp(_scrollY, 0, Math.Max(0, _contentHeight - ViewportHeight));

    private void UpdateScrollBars()
    {
        var fullRange = FullEnd - FullStart;

        var range = _viewEnd - _viewStart;

        _horizontalScrollBar.Maximum = Math.Max(0, fullRange - range);
        _horizontalScrollBar.ViewportSize = range;
        _horizontalScrollBar.LargeChange = range;
        _horizontalScrollBar.SmallChange = range / 10;
        _horizontalScrollBar.Value = Math.Max(0, _viewStart - FullStart);
        _horizontalScrollBar.Visibility = range < fullRange ? Visibility.Visible : Visibility.Collapsed;

        var viewport = ViewportHeight;

        _verticalScrollBar.Maximum = Math.Max(0, _contentHeight - viewport);
        _verticalScrollBar.ViewportSize = viewport;
        _verticalScrollBar.LargeChange = viewport;
        _verticalScrollBar.SmallChange = _rowHeight * 3;
        _verticalScrollBar.Value = _scrollY;
        _verticalScrollBar.Visibility = ShowsFlameChart && _contentHeight > viewport ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnHorizontalScroll(object sender, ScrollEventArgs e) => ScrollViewTo(FullStart + e.NewValue);

    private void OnVerticalScroll(object sender, ScrollEventArgs e)
    {
        _scrollY = e.NewValue;

        ClampScroll();

        _canvas.Invalidate();
    }

    private void OnOverlaySizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_viewEnd - _viewStart >= FullEnd - FullStart)
        {
            SetView(FullStart, FullEnd);
        }

        Relayout();
    }

    private void BuildLayout()
    {
        var threads = _visible?.Threads ?? [];

        if (_expandedThread is { } expanded && threads.All(t => t.ThreadId != expanded))
        {
            _expandedThread = null;
        }

        var rows = threads.Sum(DepthOf);

        var available = (float)ViewportHeight - threads.Count * (LaneHeaderHeight + LaneGap);

        var maximum = _expandedThread is null ? MaximumRowHeight : FocusedMaximumRowHeight;

        _rowHeight = rows == 0 ? MaximumRowHeight : Math.Clamp(MathF.Floor(available / rows), MinimumRowHeight, maximum);

        _focusPadding = _expandedThread is null ? 0 : Math.Max(0, available - rows * _rowHeight);

        _laneTops = new float[threads.Count];

        var top = 0f;

        for (var lane = 0; lane < threads.Count; lane++)
        {
            _laneTops[lane] = top;

            top += LaneHeight(threads[lane]);
        }

        _contentHeight = top;
    }

    private float LaneHeight(TraceTimelineThread thread)
        => LaneHeaderHeight + DepthOf(thread) * _rowHeight + (thread.ThreadId == _expandedThread ? _focusPadding : 0) + LaneGap;

    private int DepthOf(TraceTimelineThread thread) => IsExpanded(thread) ? thread.Depth : 0;

    private bool IsExpanded(TraceTimelineThread thread) => _expandedThread is not { } expanded || thread.ThreadId == expanded;

    private void ToggleLane(int lane)
    {
        if (_visible is not { } timeline || lane >= timeline.Threads.Count)
        {
            return;
        }

        var thread = timeline.Threads[lane].ThreadId;

        var offset = _laneTops[lane] - _scrollY;

        _expandedThread = _expandedThread == thread ? null : thread;

        _hover = null;

        BuildLayout();

        _scrollY = _laneTops[lane] - offset;

        ClampScroll();

        UpdateScrollBars();

        Redraw();
    }

    private void Reset()
    {
        _colours.Clear();
        _labels.Clear();

        FindSearchMatches();

        FindNodeMatches(_highlightedNode);

        FindCallHighlights();

        _operatorsHeight = null;

        _operators.ClearSelectedOperator();

        _root = null;

        HideToolTip();

        RebuildVisible();

        ZoomToFit();

        StopPlay();

        _playhead = null;
    }

    private void SetRoot(CallStackNode? root)
    {
        if (ReferenceEquals(root, _root))
        {
            return;
        }

        _root = root;

        HideToolTip();

        RebuildVisible();

        ZoomToFit();
    }

    private void RebuildVisible()
    {
        var selectedCall = GetCallReference(_selected) ?? (SelectedCall is { IsSelected: true } reference ? reference : null);

        var stackFrame = GetCallReference(_stackFrame);

        _rooted = null;
        _visible = null;
        _hover = null;
        _selected = null;
        _stackFrame = null;

        ReleaseMinimap();

        if (_timeline is { } timeline)
        {
            _rooted = _root is { } root ? timeline.RootedAt(n => ReferenceEquals(n, root)) : null;

            var source = _rooted ?? timeline;

            var pruned = PrunedNodes(source) is { } hidden
                ? source.Where(n => n < 0 || n >= hidden.Length || !hidden[n])
                : source;

            _visible = HiddenCategories is { Count: > 0 } categories
                ? pruned.Folded(n => n.Frame is not null && categories.Contains(n.Category))
                : pruned;
        }

        _operators.Rebuild(_visible);

        FindSelectedRegions();

        UpdateFitRange();

        BuildLayout();

        _inUseCurves.Clear();

        _inUseMaximum = _visible is { Memory.HasAllocations: true } visible
            ? visible.Memory.PeakInUseDuring(visible.StartOf(TraceTimelineAxis.Position), visible.EndOf(TraceTimelineAxis.Position))
            : 0;

        var selfAllocations = _visible?.SelfAllocations() ?? [];

        _selfMaximum = selfAllocations.Count == 0 ? 0 : selfAllocations.Max(s => s.Bytes);

        _raisedRows = selfAllocations.GroupBy(s => (s.Thread, s.Depth))
                                     .ToDictionary(g => g.Key,
                                                   g => new RaisedRow([.. g.Select(s => s.Index)], [.. g.Select(s => s.Bytes)]));

        _stackFrame = stackFrame is not null && _visible is { } rebuilt ? FindCall(rebuilt, stackFrame.Node, stackFrame.Call) : null;

        FindSelectedCall(selectedCall, bringIntoView: false);

        _version++;
    }

    private void OnMemoryModeClick(object sender, RoutedEventArgs e)
    {
        if (sender is RadioMenuFlyoutItem { Tag: TraceTimelineMemoryMode mode })
        {
            MemoryMode = mode;
        }
    }

    private MenuFlyoutSubItem CreateMemoryMenu()
        => MenuItems.SubMenu("Memory",
                             MemoryItem("Allocated", TraceTimelineMemoryMode.Allocated),
                             MemoryItem("In Use", TraceTimelineMemoryMode.InUse),
                             MemoryItem("None", null));

    private RadioMenuFlyoutItem MemoryItem(string text, TraceTimelineMemoryMode? mode)
    {
        var item = new RadioMenuFlyoutItem
        {
            Text = text,
            GroupName = MemoryMenuGroup,
            IsChecked = mode is { } shown ? ShowMemory && MemoryMode == shown : !ShowMemory
        };

        item.Click += (_, _) => SetMemory(mode);

        return item;
    }

    private void SetMemory(TraceTimelineMemoryMode? mode)
    {
        if (mode is { } shown)
        {
            MemoryMode = shown;
        }

        ShowMemory = mode is not null;
    }

    private static string MemoryModeLabel(TraceTimelineMemoryMode mode)
        => mode == TraceTimelineMemoryMode.InUse ? "Memory: In Use" : "Memory: Allocated";

    private bool[]? PrunedNodes(TraceTimeline timeline)
    {
        if (PrunedCategories is not { Count: > 0 } categories)
        {
            return null;
        }

        var hidden = new bool[timeline.NodeCount];

        var any = false;

        for (var node = 0; node < hidden.Length; node++)
        {
            var parent = timeline.ParentOf(node);

            hidden[node] = (parent >= 0 && hidden[parent])
                           || (timeline.NodeOf(node) is { Frame: not null } callNode && categories.Contains(callNode.Category));

            any |= hidden[node];
        }

        return any ? hidden : null;
    }

    private static FlameHit? FindCall(TraceTimeline timeline, CallStackNode node, int call)
        => timeline.FindCall(node, call) is { } location ? new FlameHit(location.ThreadIndex, location.Depth, location.Index) : null;

    private void FindSelectedCall(CallReference? reference, bool bringIntoView)
    {
        if (reference is null || _visible is not { } visible)
        {
            return;
        }

        var found = IsCall(_selected, reference) ? _selected : FindCall(visible, reference.Node, reference.Call);

        if (found is not { } hit)
        {
            return;
        }

        SetSelected(hit);

        if (bringIntoView)
        {
            BringIntoView(hit);
        }
    }

    private void ReportCallStack()
    {
        if (_stackFrame == _reportedStackFrame
            && _selected == _reportedSelected
            && _version == _reportedVersion
            && _axis == _reportedAxis)
        {
            return;
        }

        _reportedStackFrame = _stackFrame;

        _reportedSelected = _selected;

        _reportedVersion = _version;

        _reportedAxis = _axis;

        DispatcherQueue.TryEnqueue(() => CallStackChanged?.Invoke());
    }

    private CallReference? GetCallReference(FlameHit? hit)
        => hit is { } at && SpanOf(at) is { } span && _visible?.NodeOf(span.Node) is { } node ? new CallReference(node, span.Call, true) : null;

    private bool IsCall(FlameHit? hit, CallReference reference)
        => hit is { } at
           && SpanOf(at) is { } span
           && span.Call == reference.Call
           && ReferenceEquals(_visible?.NodeOf(span.Node), reference.Node);

    private void BringIntoView(FlameHit hit)
    {
        if (SpanOf(hit) is not { } span)
        {
            return;
        }

        if (_visible is { } timeline && !IsExpanded(timeline.Threads[hit.Lane]))
        {
            _expandedThread = timeline.Threads[hit.Lane].ThreadId;

            BuildLayout();

            _version++;
        }

        var top = _laneTops[hit.Lane] + LaneHeaderHeight + hit.Depth * _rowHeight;

        if (top < _scrollY || top + _rowHeight > _scrollY + ViewportHeight)
        {
            _scrollY = top - ViewportHeight / 3;
        }

        if (span.End - span.Start > _viewEnd - _viewStart)
        {
            ZoomTo(span.Start, span.End);
        }
        else if (span.End < _viewStart || span.Start > _viewEnd)
        {
            CentreViewOn((span.Start + span.End) / 2);
        }
        else
        {
            ApplyScroll();
        }
    }

    private static void OnTimelineChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (TraceTimelineControl)d;

        control._timeline = e.NewValue as TraceTimeline;

        control.ClearMark();

        control.Reset();
    }

    private static void OnFullTimelineChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (TraceTimelineControl)d;

        control._markedCalls.Clear();

        control.ReleaseMinimap();

        control._canvas.Invalidate();
    }

    private static void OnAxisChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (TraceTimelineControl)d;

        control._axis = (TraceTimelineAxis)e.NewValue;

        control.UpdateFitRange();

        control.BuildLayout();

        control._version++;

        control.ZoomToFit();

        control.StopPlay();

        control._playhead = null;
    }

    private static void OnHiddenCategoriesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (TraceTimelineControl)d;

        control.HideToolTip();

        control.RebuildVisible();

        control.ApplyScroll();
    }

    private static void OnSelectedNodeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (TraceTimelineControl)d;

        control._highlightedNode = e.NewValue as CallStackNode;

        control.FindNodeMatches(control._highlightedNode);

        control.Redraw();
    }

    private static void OnShowMemoryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (TraceTimelineControl)d;

        control.HideToolTip();

        control._memoryModeButton.Visibility = control.ShowMemory ? Visibility.Visible : Visibility.Collapsed;

        control.Relayout();
    }

    private static void OnGrantedMemoryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((TraceTimelineControl)d).Redraw();

    private static void OnShowCallStackChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((TraceTimelineControl)d).Redraw();

    private static void OnMemoryModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (TraceTimelineControl)d;

        var mode = (TraceTimelineMemoryMode)e.NewValue;

        control._memoryModeButton.Content = MemoryModeLabel(mode);

        control._allocatedItem.IsChecked = mode == TraceTimelineMemoryMode.Allocated;

        control._inUseItem.IsChecked = mode == TraceTimelineMemoryMode.InUse;

        control.HideToolTip();

        control._hoverBlock = null;

        control._operators.Hover = TraceOperatorsHover.None;

        control.Redraw();
    }

    private static void OnSelectedCallChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (TraceTimelineControl)d;

        var reference = e.NewValue as CallReference;

        if (reference is { IsSelected: true })
        {
            control.FindSelectedCall(reference, bringIntoView: true);

            if (!control.IsCall(control._selected, reference) && control._root is not null)
            {
                control.SetRoot(null);

                control.FindSelectedCall(reference, bringIntoView: true);
            }

            if (control.AutoZoom)
            {
                control.ZoomToSelectedCall();
            }
        }

        var highlighted = reference is { IsSelected: true }
            ? control.IsCall(control._selected, reference) ? null : reference.Node
            : control._highlightedNode;

        control.FindNodeMatches(highlighted);

        control.Redraw();
    }
}
