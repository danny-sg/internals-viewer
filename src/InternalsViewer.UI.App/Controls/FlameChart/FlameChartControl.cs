using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.TimeTravel.Memory;
using InternalsViewer.Query.CallStack.TimeTravel.Timeline;
using InternalsViewer.UI.App.Models.Query.CallStack;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using Windows.UI;

namespace InternalsViewer.UI.App.Controls.FlameChart;

public sealed partial class FlameChartControl : Grid, IDisposable
{
    private const double MinimumRangeSteps = 20;

    private const double FitPadding = 30;

    private const string MemoryModeGroup = "MemoryMode";

    public static readonly DependencyProperty TimelineProperty =
        DependencyProperty.Register(nameof(Timeline),
                                    typeof(TimeTravelTimeline),
                                    typeof(FlameChartControl),
                                    new PropertyMetadata(null, OnTimelineChanged));

    public TimeTravelTimeline? Timeline
    {
        get => (TimeTravelTimeline?)GetValue(TimelineProperty);
        set => SetValue(TimelineProperty, value);
    }

    public static readonly DependencyProperty AxisProperty =
        DependencyProperty.Register(nameof(Axis),
                                    typeof(TimeTravelTimelineAxis),
                                    typeof(FlameChartControl),
                                    new PropertyMetadata(TimeTravelTimelineAxis.Position, OnAxisChanged));

    public TimeTravelTimelineAxis Axis
    {
        get => (TimeTravelTimelineAxis)GetValue(AxisProperty);
        set => SetValue(AxisProperty, value);
    }

    public static readonly DependencyProperty HiddenCategoriesProperty =
        DependencyProperty.Register(nameof(HiddenCategories),
                                    typeof(IReadOnlySet<string>),
                                    typeof(FlameChartControl),
                                    new PropertyMetadata(null, OnHiddenCategoriesChanged));

    public IReadOnlySet<string>? HiddenCategories
    {
        get => (IReadOnlySet<string>?)GetValue(HiddenCategoriesProperty);
        set => SetValue(HiddenCategoriesProperty, value);
    }

    public static readonly DependencyProperty RootNodeProperty =
        DependencyProperty.Register(nameof(RootNode),
                                    typeof(CallStackNode),
                                    typeof(FlameChartControl),
                                    new PropertyMetadata(null, OnRootNodeChanged));

    public CallStackNode? RootNode
    {
        get => (CallStackNode?)GetValue(RootNodeProperty);
        set => SetValue(RootNodeProperty, value);
    }

    public static readonly DependencyProperty IsLockedProperty =
        DependencyProperty.Register(nameof(IsLocked),
                                    typeof(bool),
                                    typeof(FlameChartControl),
                                    new PropertyMetadata(false, OnIsLockedChanged));

    public bool IsLocked
    {
        get => (bool)GetValue(IsLockedProperty);
        set => SetValue(IsLockedProperty, value);
    }

    public static readonly DependencyProperty ShowMemoryProperty =
        DependencyProperty.Register(nameof(ShowMemory),
                                    typeof(bool),
                                    typeof(FlameChartControl),
                                    new PropertyMetadata(false, OnShowMemoryChanged));

    public bool ShowMemory
    {
        get => (bool)GetValue(ShowMemoryProperty);
        set => SetValue(ShowMemoryProperty, value);
    }

    public static readonly DependencyProperty GrantedMemoryProperty =
        DependencyProperty.Register(nameof(GrantedMemory),
                                    typeof(long),
                                    typeof(FlameChartControl),
                                    new PropertyMetadata(0L, OnGrantedMemoryChanged));

    public long GrantedMemory
    {
        get => (long)GetValue(GrantedMemoryProperty);
        set => SetValue(GrantedMemoryProperty, value);
    }

    public static readonly DependencyProperty MemoryModeProperty =
        DependencyProperty.Register(nameof(MemoryMode),
                                    typeof(FlameChartMemoryMode),
                                    typeof(FlameChartControl),
                                    new PropertyMetadata(FlameChartMemoryMode.Allocated, OnMemoryModeChanged));

    public FlameChartMemoryMode MemoryMode
    {
        get => (FlameChartMemoryMode)GetValue(MemoryModeProperty);
        set => SetValue(MemoryModeProperty, value);
    }

    public static readonly DependencyProperty SelectedCallProperty =
        DependencyProperty.Register(nameof(SelectedCall),
                                    typeof(CallReference),
                                    typeof(FlameChartControl),
                                    new PropertyMetadata(null, OnSelectedCallChanged));

    public CallReference? SelectedCall
    {
        get => (CallReference?)GetValue(SelectedCallProperty);
        set => SetValue(SelectedCallProperty, value);
    }

    private readonly SKXamlCanvas _canvas;

    private readonly Canvas _overlay;

    private readonly ScrollBar _horizontalScrollBar;

    private readonly ScrollBar _verticalScrollBar;

    private readonly Popup _toolTip;

    private readonly DropDownButton _memoryModeButton;

    private readonly RadioMenuFlyoutItem _allocatedItem;

    private readonly RadioMenuFlyoutItem _inUseItem;

    private readonly TextBlock _toolTipText;

    private readonly FlameChartPaints _paints = new();

    private readonly SKPathBuilder _pathBuilder = new();

    private readonly SKPath _playheadTriangle = PlayheadTriangle();

    private readonly Dictionary<int, SKColor> _colours = [];

    private readonly Dictionary<int, string> _labels = [];

    private readonly Dictionary<string, SKColor> _parsedColours = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<FlameHit, TimeTravelInUseCurve> _inUseCurves = [];

    private TimeTravelTimeline? _timeline;

    private CallStackNode? _root;

    private TimeTravelTimeline? _rooted;

    private TimeTravelTimeline? _visible;

    private TimeTravelTimelineAxis _axis = TimeTravelTimelineAxis.Position;

    private float[] _laneTops = [];

    private uint? _expandedThread;

    private float _focusPadding;

    private double? _playhead;

    private ulong _memoryMaximum;

    private IReadOnlyList<TimeTravelSelfAllocation> _selfAllocations = [];

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

    public FlameChartControl()
    {
        Background = new SolidColorBrush(Colors.Transparent);

        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _canvas = new SKXamlCanvas { IgnorePixelScaling = true };
        _canvas.PaintSurface += OnPaintSurface;

        Children.Add(_canvas);

        _overlay = new Canvas { Background = new SolidColorBrush(Colors.Transparent) };

        Children.Add(_overlay);

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
            Tag = FlameChartMemoryMode.Allocated,
            IsChecked = true
        };

        _allocatedItem.Click += OnMemoryModeClick;

        _inUseItem = new RadioMenuFlyoutItem { Text = "In Use", GroupName = MemoryModeGroup, Tag = FlameChartMemoryMode.InUse };

        _inUseItem.Click += OnMemoryModeClick;

        var memoryModes = new MenuFlyout();

        memoryModes.Items.Add(_allocatedItem);
        memoryModes.Items.Add(_inUseItem);

        _memoryModeButton = new DropDownButton
        {
            Content = MemoryModeLabel(FlameChartMemoryMode.Allocated),
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

    private double FullStart => _fitStart;

    private double FullEnd => Math.Max(_fitEnd, FullStart + MinimumRange);

    private double MinimumRange => MinimumRangeSteps * (_timeline?.StepOf(_axis) ?? 1);

    private double ViewportHeight => Math.Max(0, _overlay.ActualHeight - ContentTop - BandHeight);

    private float RulerTop => ShowMemory ? MemoryBarHeight : 0;

    private float ContentTop => OperatorsTop + OperatorsArea;

    private bool ShowsSpikes => ShowMemory && MemoryMode == FlameChartMemoryMode.Allocated;

    private bool ShowsMemoryBand => ShowMemory && _axis == TimeTravelTimelineAxis.Position && _visible is { HasAllocations: true };

    private float BandHeight => ShowsMemoryBand ? MemoryBandHeight : 0;

    public void ZoomToFit()
    {
        _scrollY = 0;

        SetView(FullStart, FullEnd);
    }

    public void StepPlayhead(bool forward)
    {
        if (_visible is not { } timeline)
        {
            return;
        }

        var from = _playhead ?? _viewStart;

        double? next = null;

        foreach (var thread in timeline.Threads)
        {
            for (var depth = 0; depth < DepthOf(thread) && depth < thread.Rows.Count; depth++)
            {
                var row = thread.Rows[depth];

                var index = forward ? row.FirstStartingAfter(_axis, from) : row.FirstStartingFrom(_axis, from, 0) - 1;

                if (index < 0 || index >= row.Count)
                {
                    continue;
                }

                var start = row.Starts(_axis)[index];

                if (next is not { } best || (forward ? start < best : start > best))
                {
                    next = start;
                }
            }
        }

        if (next is not { } target)
        {
            return;
        }

        _playhead = target;

        if (target < _viewStart || target > _viewEnd)
        {
            var range = _viewEnd - _viewStart;

            SetView(target - range / 2, target + range / 2);
        }

        _canvas.Invalidate();
    }

    public void Dispose()
    {
        Loaded -= OnLoaded;

        ActualThemeChanged -= OnActualThemeChanged;

        _canvas.PaintSurface -= OnPaintSurface;

        _horizontalScrollBar.Scroll -= OnHorizontalScroll;
        _verticalScrollBar.Scroll -= OnVerticalScroll;

        _overlay.PointerPressed -= OnPointerPressed;
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

        _allocatedItem.Click -= OnMemoryModeClick;
        _inUseItem.Click -= OnMemoryModeClick;

        _paints.Dispose();

        _pathBuilder.Dispose();

        _playheadTriangle.Dispose();

        _operatorShape.Dispose();

        foreach (var gradient in _operatorGradients.Values)
        {
            gradient.Dispose();
        }

        _operatorGradients.Clear();

        _timeline = null;
        _rooted = null;
        _visible = null;
        _colours.Clear();
        _labels.Clear();
        _parsedColours.Clear();
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => ApplyTheme();

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
        _verticalScrollBar.Visibility = _contentHeight > viewport ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnHorizontalScroll(object sender, ScrollEventArgs e)
    {
        var range = _viewEnd - _viewStart;

        SetView(FullStart + e.NewValue, FullStart + e.NewValue + range);
    }

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

    private float LaneHeight(TimeTravelTimelineThread thread)
        => LaneHeaderHeight + DepthOf(thread) * _rowHeight + (thread.ThreadId == _expandedThread ? _focusPadding : 0) + LaneGap;

    private int DepthOf(TimeTravelTimelineThread thread) => IsExpanded(thread) ? thread.Depth : 0;

    private bool IsExpanded(TimeTravelTimelineThread thread) => _expandedThread is not { } expanded || thread.ThreadId == expanded;

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

    private void Reset()
    {
        _colours.Clear();
        _labels.Clear();

        _operatorsHeight = null;

        _selectedOperator = null;

        _root = RootNode;

        HideToolTip();

        RebuildVisible();

        ZoomToFit();

        _playhead = null;
    }

    private void RebuildVisible()
    {
        _rooted = null;
        _visible = null;
        _hover = null;
        _selected = null;

        if (_timeline is { } timeline)
        {
            _rooted = _root is { } root ? RootedAt(timeline, root) : null;

            var source = _rooted ?? timeline;

            _visible = HiddenNodes(source) is { } hidden ? source.Where(n => n < 0 || n >= hidden.Length || !hidden[n]) : source;
        }

        _operatorRows = OperatorRowsOf(_visible);

        _operatorDetails.Clear();

        _hoverOperator = null;

        if (_selectedOperator is { } selected && !_operatorRows.Any(r => ReferenceEquals(r.Operator, selected)))
        {
            _selectedOperator = null;
        }

        FindSelectedRegions();

        UpdateFitRange();

        BuildLayout();

        _memoryMaximum = MemoryMaximum(_visible);

        _inUseCurves.Clear();

        _inUseMaximum = _visible is { HasAllocations: true } memory
            ? memory.PeakInUseDuring(memory.StartOf(TimeTravelTimelineAxis.Position), memory.EndOf(TimeTravelTimelineAxis.Position))
            : 0;

        _selfAllocations = _visible?.SelfAllocations() ?? [];

        _selfMaximum = _selfAllocations.Count == 0 ? 0 : _selfAllocations.Max(s => s.Bytes);

        _raisedRows = _selfAllocations.GroupBy(s => (s.Thread, s.Depth))
                                      .ToDictionary(g => g.Key,
                                                    g => new RaisedRow([.. g.Select(s => s.Index)], [.. g.Select(s => s.Bytes)]));

        FindSelectedCall(bringIntoView: false);

        _version++;
    }

    private void OnMemoryModeClick(object sender, RoutedEventArgs e)
    {
        if (sender is RadioMenuFlyoutItem { Tag: FlameChartMemoryMode mode })
        {
            MemoryMode = mode;
        }
    }

    private static string MemoryModeLabel(FlameChartMemoryMode mode)
        => mode == FlameChartMemoryMode.InUse ? "Memory: In Use" : "Memory: Allocated";

    private static ulong MemoryMaximum(TimeTravelTimeline? timeline)
    {
        if (timeline is not { HasAllocations: true })
        {
            return 0;
        }

        ulong maximum = 0;

        foreach (var thread in timeline.Threads.Where(t => t.Rows.Count > 0))
        {
            var row = thread.Rows[0];

            var starts = row.Starts(TimeTravelTimelineAxis.Position);

            var ends = row.Ends(TimeTravelTimelineAxis.Position);

            for (var index = 0; index < row.Count; index++)
            {
                maximum = Math.Max(maximum, timeline.AllocatedDuring(thread.ThreadId, starts[index], ends[index]).Bytes);
            }
        }

        return maximum;
    }

    private static TimeTravelTimeline? RootedAt(TimeTravelTimeline timeline, CallStackNode root)
        => timeline.RootedAt(n => ReferenceEquals(n, root))
           ?? (root.Frame is { } frame ? timeline.RootedAt(n => ReferenceEquals(n.Frame, frame)) : null);

    private bool[]? HiddenNodes(TimeTravelTimeline timeline)
    {
        if (HiddenCategories is not { Count: > 0 } categories)
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

    private static bool[]? Matches(TimeTravelTimeline timeline, Func<CallStackNode, bool> predicate)
    {
        var matches = new bool[timeline.NodeCount];

        var any = false;

        for (var node = 0; node < matches.Length; node++)
        {
            if (timeline.NodeOf(node) is { } callNode && predicate(callNode))
            {
                matches[node] = true;

                any = true;
            }
        }

        return any ? matches : null;
    }

    private void FindSelectedCall(bool bringIntoView)
    {
        if (SelectedCall is not { } reference
            || _timeline is not { } timeline
            || _visible is not { } visible
            || Matches(timeline, n => ReferenceEquals(n, reference.Node)) is not { } nodes)
        {
            return;
        }

        if (_selected is { } current
            && SpanOf(current) is { } span
            && span.Call == reference.Call
            && span.Node < nodes.Length
            && nodes[span.Node])
        {
            if (bringIntoView)
            {
                BringIntoView(current);
            }

            return;
        }

        for (var lane = 0; lane < visible.Threads.Count; lane++)
        {
            var rows = visible.Threads[lane].Rows;

            for (var depth = 0; depth < rows.Count; depth++)
            {
                var row = rows[depth];

                for (var index = 0; index < row.Count; index++)
                {
                    var node = row.NodeAt(index);

                    if (row.CallAt(index) != reference.Call || node < 0 || node >= nodes.Length || !nodes[node])
                    {
                        continue;
                    }

                    _selected = new FlameHit(lane, depth, index);

                    if (bringIntoView)
                    {
                        BringIntoView(_selected.Value);
                    }

                    return;
                }
            }
        }
    }

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

        var range = _viewEnd - _viewStart;

        if (span.End - span.Start > range)
        {
            var margin = (span.End - span.Start) * 0.05;

            SetView(span.Start - margin, span.End + margin);
        }
        else if (span.End < _viewStart || span.Start > _viewEnd)
        {
            var start = (span.Start + span.End) / 2 - range / 2;

            SetView(start, start + range);
        }
        else
        {
            ClampScroll();

            UpdateScrollBars();

            _canvas.Invalidate();
        }
    }

    private static void OnTimelineChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FlameChartControl)d;

        control._timeline = e.NewValue as TimeTravelTimeline;

        control.Reset();
    }

    private static void OnAxisChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FlameChartControl)d;

        control._axis = (TimeTravelTimelineAxis)e.NewValue;

        control.UpdateFitRange();

        control.BuildLayout();

        control._version++;

        control.ZoomToFit();

        control._playhead = null;
    }

    private static void OnHiddenCategoriesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FlameChartControl)d;

        control.HideToolTip();

        control.RebuildVisible();

        control.ClampScroll();

        control.UpdateScrollBars();

        control._canvas.Invalidate();
    }

    private static void OnRootNodeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FlameChartControl)d;

        if (!control.IsLocked)
        {
            control.SetRoot(e.NewValue as CallStackNode);
        }
    }

    private static void OnShowMemoryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FlameChartControl)d;

        control.HideToolTip();

        control._memoryModeButton.Visibility = control.ShowMemory ? Visibility.Visible : Visibility.Collapsed;

        control.Relayout();
    }

    private static void OnGrantedMemoryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((FlameChartControl)d).Redraw();

    private static void OnMemoryModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FlameChartControl)d;

        var mode = (FlameChartMemoryMode)e.NewValue;

        control._memoryModeButton.Content = MemoryModeLabel(mode);

        control._allocatedItem.IsChecked = mode == FlameChartMemoryMode.Allocated;

        control._inUseItem.IsChecked = mode == FlameChartMemoryMode.InUse;

        control.HideToolTip();

        control._hoverBlock = null;

        control._hoverOperator = null;

        control.Redraw();
    }

    private static void OnIsLockedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FlameChartControl)d;

        if (e.NewValue is false)
        {
            control.SetRoot(control.RootNode);
        }
    }

    private static void OnSelectedCallChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FlameChartControl)d;

        if (e.NewValue is null)
        {
            control._selected = null;
        }
        else
        {
            control.FindSelectedCall(bringIntoView: true);
        }

        control._canvas.Invalidate();
    }
}
