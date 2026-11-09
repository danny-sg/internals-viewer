using System.Collections.Generic;
using System.Linq;
using System;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Interfaces.Events;
using InternalsViewer.Query.Plans.Joins;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.Query.Plans.Operators;
using InternalsViewer.Query.Plans;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Plan;
using InternalsViewer.UI.App.Models.Query.Plan.Enums;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI;
using Windows.Foundation;
using Windows.UI;

namespace InternalsViewer.UI.App.Controls.Query.Plan;

public sealed class ExecutionPlanControl : Canvas
{
    private const double BaseNodeWidth = 150;
    private const double BaseNodeHeight = 90;
    private const double HorizontalGap = 46;
    private const double VerticalGap = 22;
    private const double CanvasMargin = 24;
    private const double ConnectorThickness = 1.25;
    private const double IteratorLineOffset = 3;
    private const double LanePitch = IteratorLineOffset * 2 + 10;
    private const double IteratorHeadHalfWidth = 4;
    private const double IdleIteratorOpacity = 25;
    private const double CurrentArrowSize = 20;
    private const double CurrentArrowThickness = 3.5;
    private const double CurrentArrowGap = 1;
    private const double CallArrowRotation = -90;
    private const double ReturnArrowRotation = 90;
    private const double CalculatorWidth = 14;
    private const double CalculatorHeight = 17;
    private const double CalculatorGap = 4;
    private const double ExpressionSpacing = 4;
    private const int RenderSettleMilliseconds = 100;

    private readonly Dictionary<PlanNode, IReadOnlyList<PlanNodeAnnotation>> _annotationsByNode = [];

    private readonly List<double> _columnWidths = [];

    private double WidthOf(PlanNode node)
        => BaseNodeWidth + (_annotationsByNode.TryGetValue(node, out var a) && a.Count > 0
                            ? PlanNodeControl.AnnotationColumnWidth
                            : 0);

    private double ColumnWidth(int depth) => depth < _columnWidths.Count ? _columnWidths[depth] : BaseNodeWidth;

    private double GapAfter(int depth)
        => IsFullTrace && depth < ColumnFanOuts.Count
            ? HorizontalGap + Math.Max(ColumnFanOuts[depth] - 1, 0) * LanePitch
            : HorizontalGap;

    private double ColumnX(int depth)
    {
        var x = CanvasMargin;

        for (var d = 0; d < depth; d++)
        {
            x += ColumnWidth(d) + GapAfter(d);
        }

        return x;
    }

    private static readonly Color ConnectorColor = Color.FromArgb(255, 185, 185, 185);

    // Flow-line colour for a blocked producer (active but not yet emitting - still consuming its input).
    private static readonly Color BlockedColor = Color.FromArgb(255, 245, 102, 102);

    // Branch labels (Build / Probe) sit near the child end of a hash join's input connectors.
    private static readonly Color BranchLabelColor = Color.FromArgb(255, 160, 160, 160);

    private static readonly Color IteratorColour = Color.FromArgb(255, 0, 176, 80);

    private static readonly Color CurrentArrowColour = Color.FromArgb(255, 0xF7, 0x63, 0x0C);

    private static readonly Point[] CurrentArrowPoints = [new(10, 2), new(10, 17), new(3, 10), new(10, 17), new(17, 10)];


    public static readonly DependencyProperty PlanProperty =
        DependencyProperty.Register(nameof(Plan), typeof(ExecutionPlan), typeof(ExecutionPlanControl),
            new PropertyMetadata(null, OnPlanChanged));

    public ExecutionPlan? Plan
    {
        get => (ExecutionPlan?)GetValue(PlanProperty);
        set => SetValue(PlanProperty, value);
    }

    public static readonly DependencyProperty AreAnnotationsVisibleProperty =
        DependencyProperty.Register(nameof(AreAnnotationsVisible), typeof(bool), typeof(ExecutionPlanControl),
            new PropertyMetadata(false, OnAnnotationsVisibleChanged));

    public bool AreAnnotationsVisible
    {
        get => (bool)GetValue(AreAnnotationsVisibleProperty);
        set => SetValue(AreAnnotationsVisibleProperty, value);
    }

    public static readonly DependencyProperty SelectedNodeProperty =
        DependencyProperty.Register(nameof(SelectedNode), typeof(PlanNode), typeof(ExecutionPlanControl),
            new PropertyMetadata(null, OnSelectedNodeChanged));

    public PlanNode? SelectedNode
    {
        get => (PlanNode?)GetValue(SelectedNodeProperty);
        set => SetValue(SelectedNodeProperty, value);
    }

    public static readonly DependencyProperty ActiveNodesProperty =
        DependencyProperty.Register(nameof(ActiveNodes), typeof(IReadOnlyList<PlanNode>), typeof(ExecutionPlanControl),
            new PropertyMetadata(null, OnActiveNodesChanged));

    public IReadOnlyList<PlanNode>? ActiveNodes
    {
        get => (IReadOnlyList<PlanNode>?)GetValue(ActiveNodesProperty);
        set => SetValue(ActiveNodesProperty, value);
    }

    public static readonly DependencyProperty EmittingNodesProperty =
        DependencyProperty.Register(nameof(EmittingNodes), typeof(IReadOnlyList<PlanNode>), typeof(ExecutionPlanControl),
            new PropertyMetadata(null, OnEmittingNodesChanged));

    public IReadOnlyList<PlanNode>? EmittingNodes
    {
        get => (IReadOnlyList<PlanNode>?)GetValue(EmittingNodesProperty);
        set => SetValue(EmittingNodesProperty, value);
    }

    public static readonly DependencyProperty IsFullTraceProperty =
        DependencyProperty.Register(nameof(IsFullTrace), typeof(bool), typeof(ExecutionPlanControl),
            new PropertyMetadata(false, OnIsFullTraceChanged));

    public bool IsFullTrace
    {
        get => (bool)GetValue(IsFullTraceProperty);
        set => SetValue(IsFullTraceProperty, value);
    }

    public static readonly DependencyProperty CurrentStepProperty =
        DependencyProperty.Register(nameof(CurrentStep), typeof(PlanStepMarker), typeof(ExecutionPlanControl),
            new PropertyMetadata(null, OnCurrentStepChanged));

    public PlanStepMarker? CurrentStep
    {
        get => (PlanStepMarker?)GetValue(CurrentStepProperty);
        set => SetValue(CurrentStepProperty, value);
    }

    public static readonly DependencyProperty EventsProperty =
        DependencyProperty.Register(nameof(Events), typeof(IReadOnlyList<EngineEvent>), typeof(ExecutionPlanControl),
            new PropertyMetadata(null, OnEventsChanged));

    /// <summary>
    /// The query's events, so each operator can show a mini icicle of its linked call stacks (see
    /// <see cref="OperatorIcicle"/>). Null/empty leaves the nodes compact with no icicle strip.
    /// </summary>
    public IReadOnlyList<EngineEvent>? Events
    {
        get => (IReadOnlyList<EngineEvent>?)GetValue(EventsProperty);
        set => SetValue(EventsProperty, value);
    }

    // Each producer node mapped to the connector that carries its rows to its consumer (parent), and to
    // that connector's arrowhead (recoloured to match the flow line while the producer is active).
    private readonly Dictionary<PlanNode, Polyline> _connectorByProducer = [];
    private readonly Dictionary<PlanNode, Polygon> _arrowByProducer = [];

    // Per-emitting-producer animated data-flow overlay marching along its outgoing connector. Keyed by
    // producer so the set can be reconciled incrementally as the active operators change each tick.
    private readonly Dictionary<PlanNode, FlowOverlay> _flows = [];

    // The plan's operators as a tree, so each node's icicle can be weighted by the events its whole subtree drove.
    // Rebuilt with the diagram rather than per node, which would invert the parent links once for every operator.
    private OperatorHierarchy _hierarchy = OperatorHierarchy.Build([]);

    private ScrollViewer? _scrollViewer;
    private PlanNodeControl? _selectedControl;

    // Guards against reacting to the SelectedNode change we raise ourselves from an in-plan click.
    private bool _applyingSelection;

    private bool _isPanning;
    private Point _panOrigin;
    private double _panOriginHorizontalOffset;
    private double _panOriginVerticalOffset;

    public ExecutionPlanControl()
    {
        Background = new SolidColorBrush(Colors.Transparent);

        CurrentArrow.RenderTransform = CurrentArrowTransform;

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += OnPointerCaptureLost;
        ContextRequested += OnContextRequested;
        DoubleTapped += OnDoubleTapped;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public event EventHandler<PlanNode?>? NodeSelected;

    public event EventHandler<PlanNode>? IndexOpenRequested;

    public event EventHandler<PlanNode>? SimulationOpenRequested;

    public event EventHandler<PlanNode>? CallTreeOpenRequested;

    public event EventHandler<PlanNode>? PropertiesOpenRequested;

    public event EventHandler<PlanNode>? CounterToggleRequested;

    public event EventHandler<PlanPage>? PageOpenRequested;

    public Func<PlanNode, bool> IsCounterShown { get; set; } = static n => n.IsStatement;

    private double RowPitch => BaseNodeHeight + VerticalGap;

    private Dictionary<PlanNode, IteratorConnector> IteratorConnectors { get; } = [];

    private List<int> ColumnFanOuts { get; } = [];

    private Dictionary<PlanNode, PlanNodeControl> NodeControls { get; } = [];

    private PlanActivity Activity { get; set; } = PlanActivity.Empty;

    private double RenderScale { get; set; } = 1;

    private double Zoom { get; set; } = 1;

    private XamlRoot? ScaleRoot { get; set; }

    private Polyline CurrentArrow { get; } = CreateCurrentArrow();

    private CompositeTransform CurrentArrowTransform { get; } = new() { CenterX = CurrentArrowSize / 2, CenterY = CurrentArrowSize / 2 };

    private DispatcherQueueTimer RenderSettleTimer => field ??= CreateRenderSettleTimer();

    private Dictionary<PlanNode, EvaluationCalculator> Calculators { get; } = [];

    public void ShowActivity(PlanActivity activity)
    {
        Activity = activity;

        foreach (var (node, connector) in IteratorConnectors)
        {
            if (IsOnPath(node, activity.Requested))
            {
                connector.Request.Light();
            }
            else
            {
                connector.Request.Fade();
            }

            if (IsOnPath(node, activity.Returned))
            {
                connector.Response.Light();
            }
            else
            {
                connector.Response.Fade();
            }
        }

        foreach (var (node, control) in NodeControls)
        {
            control.ShowParts(activity.Parts.Where(p => ReferenceEquals(p.Node, node) && p.CalculatorNode is null));

            control.ShowCounters(activity.Counters.Where(c => ReferenceEquals(c.Node, node)));

            control.SetGreyed(IsGreyed(node, activity));

            control.ShowPage(activity.Pages.FirstOrDefault(p => ReferenceEquals(p.Node, node)));

            control.ShowWatches(activity.Watches.Where(w => ReferenceEquals(w.Node, node)));
        }

        ShowEvaluations(activity);
    }

    public void ScheduleRenderRefresh()
    {
        RenderSettleTimer.Stop();

        RenderSettleTimer.Start();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        DetachScale();

        _scrollViewer = FindAncestor<ScrollViewer>(this);

        ScaleRoot = XamlRoot;

        _scrollViewer?.ViewChanged += OnViewChanged;

        ScaleRoot?.Changed += OnXamlRootChanged;

        UpdateRenderScale();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            DetachScale();
        }
    }

    private void DetachScale()
    {
        RenderSettleTimer.Stop();

        _scrollViewer?.ViewChanged -= OnViewChanged;

        ScaleRoot?.Changed -= OnXamlRootChanged;

        ScaleRoot = null;
    }

    private void OnViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        ScheduleRenderRefresh();

        if (!e.IsIntermediate)
        {
            UpdateRenderScale();
        }
    }

    private void OnXamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => UpdateRenderScale();

    private void UpdateRenderScale(bool isSettled = false)
    {
        var zoom = _scrollViewer?.ZoomFactor ?? 1;

        var scale = (ScaleRoot?.RasterizationScale ?? 1) * zoom;

        var isUnchanged = Math.Abs(scale - RenderScale) < 0.01 && Math.Abs(zoom - Zoom) < 0.01;

        if (isUnchanged && !isSettled)
        {
            return;
        }

        RenderScale = scale;

        Zoom = zoom;

        foreach (var control in NodeControls.Values)
        {
            control.SetRenderScale(scale, zoom);
        }

        if (!isUnchanged)
        {
            foreach (var calculator in Calculators.Values)
            {
                calculator.Idle.Source = CreateCalculatorSource(isInactive: true);

                calculator.Lit.Source = CreateCalculatorSource(isInactive: false);
            }
        }

        SnapCalculators();

        UpdateCurrentArrow();
    }

    private void ClearIteratorConnectors()
    {
        foreach (var connector in IteratorConnectors.Values)
        {
            connector.Request.Stop();

            connector.Response.Stop();
        }

        IteratorConnectors.Clear();

        foreach (var calculator in Calculators.Values)
        {
            calculator.Highlight.Stop();
        }

        Calculators.Clear();
    }

    private void Rebuild()
    {
        StopAllFlows();

        ClearIteratorConnectors();

        Children.Clear();

        _connectorByProducer.Clear();
        _arrowByProducer.Clear();

        NodeControls.Clear();

        _selectedControl = null;

        if (Plan is null || Plan.Root.Count == 0)
        {
            Width = 0;
            Height = 0;
            return;
        }

        _hierarchy = OperatorHierarchy.Build(Events ?? []);

        MeasureColumns();

        var positions = new Dictionary<PlanNode, Point>();
        var leaf = new LeafCursor();

        foreach (var root in Plan.Root)
        {
            AssignPositions(root, depth: 0, positions, leaf);
        }

        var totalCost = Plan.Root.Sum(r => r.EstimatedCost ?? 0);

        foreach (var (node, point) in positions)
        {
            var children = GetChildren(node);

            for (var lane = 0; lane < children.Count; lane++)
            {
                var child = children[lane];

                var (connector, arrow) = IsFullTrace
                    ? DrawIteratorConnector(point, positions[child], node, child, lane)
                    : DrawConnector(point, positions[child], RelativeCost(GetOperator(child), totalCost), node, child);

                _connectorByProducer[child] = connector;
                _arrowByProducer[child] = arrow;
            }
        }

        foreach (var probe in BitmapProbeResolver.Resolve(Plan))
        {
            if (positions.TryGetValue(probe.Creator, out var creator) && positions.TryGetValue(probe.Probe, out var target))
            {
                DrawBitmapConnector(creator, target, probe);
            }
        }

        foreach (var (node, point) in positions)
        {
            AddNode(node, point, RelativeCost(node, totalCost));
        }

        AddCalculators();

        Children.Add(CurrentArrow);

        UpdateCurrentArrow();

        var maxX = positions.Max(p => p.Value.X + WidthOf(p.Key));
        var maxY = positions.Values.Max(p => p.Y);

        Width = maxX + CanvasMargin;
        Height = maxY + BaseNodeHeight + CanvasMargin;

        SelectByNode(SelectedNode);

        UpdateActiveHighlights();

        foreach (var (node, connector) in IteratorConnectors)
        {
            if (IsOnPath(node, Activity.Requested))
            {
                connector.Request.Light();
            }
        }

        foreach (var (node, control) in NodeControls)
        {
            control.ShowParts(Activity.Parts.Where(p => p.IsRunning && ReferenceEquals(p.Node, node) && p.CalculatorNode is null));

            control.ShowCounters(Activity.Counters.Where(c => ReferenceEquals(c.Node, node)));

            control.SetGreyed(IsGreyed(node, Activity));

            control.ShowPage(Activity.Pages.FirstOrDefault(p => ReferenceEquals(p.Node, node)));

            control.ShowWatches(Activity.Watches.Where(w => ReferenceEquals(w.Node, node)));
        }

        UpdateFlows();

        ShowEvaluations(Activity);

        ScheduleRenderRefresh();
    }

    private double AssignPositions(PlanNode node,
                                   int depth,
                                   Dictionary<PlanNode, Point> positions,
                                   LeafCursor leaf)
    {
        var x = ColumnX(depth);

        double y;

        var children = GetChildren(node);

        if (children.Count == 0)
        {
            y = CanvasMargin + leaf.Next * RowPitch;
            leaf.Next++;
        }
        else
        {
            var first = 0d;

            for (var i = 0; i < children.Count; i++)
            {
                var childY = AssignPositions(children[i], depth + 1, positions, leaf);

                if (i == 0)
                {
                    first = childY;
                }
            }

            y = first;
        }

        positions[node] = new Point(x, y);

        return y;
    }

    private void AddNode(PlanNode node, Point point, double costFraction)
    {
        var control = new PlanNodeControl
        {
            Node = node,
            CostPercent = IsFullTrace ? -1 : costFraction,
            AreAnnotationsVisible = AreAnnotationsVisible,
            Width = WidthOf(node),
            Height = BaseNodeHeight
        };

        SetLeft(control, point.X);
        SetTop(control, point.Y);

        Children.Add(control);

        NodeControls[node] = control;

        control.PageOpenRequested += OnNodePageOpenRequested;

        control.SetRenderScale(RenderScale, Zoom);
    }

    private void OnNodePageOpenRequested(object? sender, PlanPage page) => PageOpenRequested?.Invoke(this, page);

    private (Polyline Connector, Polygon Arrow) DrawConnector(Point parent, Point child, double childCostFraction,
                                                             PlanNode parentNode, PlanNode childNode)
    {
        var start = new Point(child.X, child.Y + BaseNodeHeight / 2);
        var end = new Point(parent.X + WidthOf(parentNode), parent.Y + BaseNodeHeight / 2);

        var elbowX = (start.X + end.X) / 2;

        var thickness = ConnectorThickness + 4.0 * Math.Clamp(childCostFraction, 0, 1);

        // Scale the arrowhead with the line so a thick "many rows" connector gets a proportionally larger
        // head that still reads as an arrow; it must stay wider than the shaft so the shaft can't poke out.
        var headHalfWidth = Math.Max(4.5, thickness * 1.4);
        var headLength = headHalfWidth * 2.0;

        var brush = new SolidColorBrush(ConnectorColor);

        // Stop the shaft at the (last) arrowhead's base rather than the node edge, so the squared line end
        // is hidden behind the head and doesn't stick out past the (zero-width) tip.
        var shaftEnd = new Point(end.X + headLength, end.Y);

        var connector = new Polyline
        {
            Stroke = brush,
            StrokeThickness = thickness,
            StrokeLineJoin = PenLineJoin.Miter,
            Points =
            {
                start,
                new Point(elbowX, start.Y),
                new Point(elbowX, end.Y),
                shaftEnd
            }
        };

        Children.Add(connector);

        var arrow = MakeArrowhead(end.X, end.Y, headLength, headHalfWidth, brush);
        Children.Add(arrow);

        // Hash join inputs are labelled Build (the blocking side) / Probe (the streaming side) near the
        // child end of the line.
        if (OperatorClassifier.IsHashJoin(parentNode))
        {
            var label = OperatorClassifier.RoleOf(parentNode, GetOperator(childNode)) == InputRole.Blocking ? "Build" : "Probe";
            AddBranchLabel(label, elbowX, start.Y);
        }

        return (connector, arrow);
    }

    private (Polyline Connector, Polygon Arrow) DrawIteratorConnector(Point parent,
                                                                     Point child,
                                                                     PlanNode parentNode,
                                                                     PlanNode childNode,
                                                                     int lane)
    {
        const double offset = IteratorLineOffset;

        const double halfWidth = IteratorHeadHalfWidth;

        const double headLength = IteratorHeadHalfWidth * 2;

        var lanes = parentNode.Children.Count;

        var laneSpacing = lanes > 1 ? Math.Min(LanePitch, (BaseNodeHeight / 2 - offset * 2) / (lanes - 1)) : 0;

        var childX = child.X;

        var childY = child.Y + BaseNodeHeight / 2;

        var parentX = parent.X + WidthOf(parentNode);

        var parentY = parent.Y + BaseNodeHeight / 2 + lane * laneSpacing;

        var elbowX = (childX + parentX) / 2 + ((lanes - 1) / 2.0 - lane) * LanePitch;

        var request = AddIteratorLine([new Point(parentX, parentY - offset),
                                       new Point(elbowX + offset, parentY - offset),
                                       new Point(elbowX + offset, childY - offset),
                                       new Point(childX - headLength, childY - offset)],
                                      [new Point(childX, childY - offset),
                                       new Point(childX - headLength, childY - offset - halfWidth),
                                       new Point(childX - headLength, childY - offset + halfWidth)],
                                      isDotted: true);

        var response = AddIteratorLine([new Point(childX, childY + offset),
                                        new Point(elbowX - offset, childY + offset),
                                        new Point(elbowX - offset, parentY + offset),
                                        new Point(parentX + headLength, parentY + offset)],
                                       [new Point(parentX, parentY + offset),
                                        new Point(parentX + headLength, parentY + offset - halfWidth),
                                        new Point(parentX + headLength, parentY + offset + halfWidth)],
                                       isDotted: false);

        var middleX = parentY == childY ? (parentX + childX) / 2 : (elbowX + childX) / 2;

        IteratorConnectors[childNode] = new IteratorConnector(request.Glow, response.Glow, middleX);

        if (OperatorClassifier.IsHashJoin(parentNode))
        {
            var label = OperatorClassifier.RoleOf(parentNode, parentNode.Children[lane]) == InputRole.Blocking ? "Build" : "Probe";

            AddBranchLabel(label, elbowX + offset, childY - offset);
        }

        return (response.Line, response.Head);
    }

    private (Polyline Line, Polygon Head, PlanHighlight Glow) AddIteratorLine(Point[] points, Point[] head, bool isDotted)
    {
        var line = CreateIteratorLine(points, ConnectorColor, isDotted);

        var arrow = CreateIteratorHead(head, ConnectorColor);

        line.Opacity = IdleIteratorOpacity;

        arrow.Opacity = IdleIteratorOpacity;

        var glowLine = CreateIteratorLine(points, IteratorColour, isDotted);

        var glowHead = CreateIteratorHead(head, IteratorColour);

        Children.Add(line);

        Children.Add(arrow);

        Children.Add(glowLine);

        Children.Add(glowHead);

        return (line, arrow, new PlanHighlight(glowLine, glowHead));
    }

    private static Polyline CreateIteratorLine(Point[] points, Color colour, bool isDotted)
    {
        var line = new Polyline
        {
            Stroke = new SolidColorBrush(colour),
            StrokeThickness = ConnectorThickness,
            StrokeLineJoin = PenLineJoin.Miter,
            IsHitTestVisible = false
        };

        if (isDotted)
        {
            line.StrokeDashArray = [2, 2];
        }

        foreach (var point in points)
        {
            line.Points.Add(point);
        }

        return line;
    }

    private static Polygon CreateIteratorHead(Point[] points, Color colour)
    {
        var head = new Polygon
        {
            Fill = new SolidColorBrush(colour),
            IsHitTestVisible = false
        };

        foreach (var point in points)
        {
            head.Points.Add(point);
        }

        return head;
    }

    private void DrawBitmapConnector(Point creator, Point probe, BitmapProbe bitmapProbe)
    {
        const double headLength = 6;
        const double headHalfWidth = 4;

        var startX = creator.X + BaseNodeWidth / 2;

        var startY = probe.Y > creator.Y ? creator.Y + BaseNodeHeight : creator.Y;

        var laneY = probe.Y - VerticalGap / 2;

        var endX = probe.X + BaseNodeWidth / 2;

        var endY = probe.Y + PlanNodeControl.IconTop;

        var brush = new SolidColorBrush(PlanNodeAnnotations.Bitmap);

        var connector = new Polyline
        {
            Stroke = brush,
            StrokeThickness = 2,
            StrokeDashArray = [1, 1],
            StrokeLineJoin = PenLineJoin.Miter,
            Points =
            {
                new Point(startX, startY),
                new Point(startX, laneY),
                new Point(endX, laneY),
                new Point(endX, endY - headLength)
            }
        };

        ToolTipService.SetToolTip(connector, bitmapProbe.Bitmap);

        Children.Add(connector);

        Children.Add(new Polygon
        {
            Fill = brush,
            Points =
            {
                new Point(endX, endY),
                new Point(endX - headHalfWidth, endY - headLength),
                new Point(endX + headHalfWidth, endY - headLength)
            }
        });
    }

    // A left-pointing arrowhead with its tip at (tipX, y); the base sits headLength to the right.
    private static Polygon MakeArrowhead(double tipX, double y, double headLength, double headHalfWidth, Brush fill)
        => new()
        {
            Fill = fill,
            Points =
            {
                new Point(tipX, y),
                new Point(tipX + headLength, y - headHalfWidth),
                new Point(tipX + headLength, y + headHalfWidth)
            }
        };

    private void AddBranchLabel(string text, double lineX, double y)
    {
        var label = new TextBlock
        {
            Text = text,
            FontSize = 10,
            Foreground = new SolidColorBrush(BranchLabelColor)
        };

        // Just to the right of the vertical connector segment, above the line.
        SetLeft(label, lineX + 3);
        SetTop(label, y - 17);

        Children.Add(label);
    }

    private static double RelativeCost(PlanNode node, double totalCost)
    {
        if (totalCost <= 0 || node.EstimatedCost is not { } subtree)
        {
            return -1;
        }

        var childCost = node.Children.Sum(c => c.EstimatedCost ?? 0);
        var ownCost = Math.Max(0, subtree - childCost);

        return ownCost / totalCost;
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var nodeControl = FindOperatorControl(e.OriginalSource);

        if (nodeControl is not null)
        {
            Select(nodeControl);
            e.Handled = true;
            return;
        }

        BeginPan(e);
    }

    private void OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        var nodeControl = FindOperatorControl(e.OriginalSource);

        if (nodeControl?.Node is { } node)
        {
            Select(nodeControl);

            PropertiesOpenRequested?.Invoke(this, node);

            e.Handled = true;
        }
    }

    private void OnContextRequested(UIElement sender, ContextRequestedEventArgs e)
    {
        var nodeControl = FindOperatorControl(e.OriginalSource);

        if (nodeControl?.Node is { } node)
        {
            var flyout = new MenuFlyout();

            if (!string.IsNullOrEmpty(node.Index) &&
                OperatorClassifier.GetCategory(node) == OperatorCategory.DataAccess)
            {
                var openIndex = new MenuFlyoutItem { Text = $"Open Index: {node.Index}" };

                openIndex.Click += (_, _) => IndexOpenRequested?.Invoke(this, node);

                flyout.Items.Add(openIndex);
            }

            var canSimulate = node.IsStatement
                           || OperatorClassifier.IsTop(node)
                           || OperatorClassifier.IsConcatenation(node)
                           || OperatorClassifier.IsSort(node)
                           || (!string.IsNullOrEmpty(node.Table) && OperatorClassifier.GetCategory(node) == OperatorCategory.DataAccess)
                           || CorrelatedJoinResolver.Resolve(node) is not null
                           || MergeJoinResolver.Resolve(node) is not null
                           || HashJoinResolver.Resolve(node) is not null;

            if (canSimulate)
            {
                var openSimulation = new MenuFlyoutItem { Text = "Simulate" };

                openSimulation.Click += (_, _) => SimulationOpenRequested?.Invoke(this, node);

                flyout.Items.Add(openSimulation);
            }

            var openCallTree = new MenuFlyoutItem { Text = "Call Tree" };

            openCallTree.Click += (_, _) => CallTreeOpenRequested?.Invoke(this, node);

            flyout.Items.Add(openCallTree);

            var openProperties = new MenuFlyoutItem { Text = $"Properties" };

            openProperties.Click += (_, _) => PropertiesOpenRequested?.Invoke(this, node);

            flyout.Items.Add(openProperties);

            if (IsFullTrace)
            {
                flyout.Items.Add(new MenuFlyoutSeparator());

                flyout.Items.Add(MenuItems.Toggle("Show Counter", IsCounterShown(node), _ => CounterToggleRequested?.Invoke(this, node)));
            }

            if (e.TryGetPosition(this, out var position))
            {
                flyout.ShowAt(this, new FlyoutShowOptions { Position = position });
            }
            else
            {
                flyout.ShowAt(nodeControl);
            }

            e.Handled = true;
        }
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isPanning || _scrollViewer is null)
        {
            return;
        }

        var position = e.GetCurrentPoint(_scrollViewer).Position;

        _scrollViewer.ChangeView(_panOriginHorizontalOffset - (position.X - _panOrigin.X),
                                 _panOriginVerticalOffset - (position.Y - _panOrigin.Y),
                                 zoomFactor: null,
                                 disableAnimation: true);

        e.Handled = true;
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e) => EndPan(e);

    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e) => EndPan(e);

    private void BeginPan(PointerRoutedEventArgs e)
    {
        _scrollViewer ??= FindAncestor<ScrollViewer>(this);

        if (_scrollViewer is null || !CapturePointer(e.Pointer))
        {
            return;
        }

        _isPanning = true;

        _panOrigin = e.GetCurrentPoint(_scrollViewer).Position;
        _panOriginHorizontalOffset = _scrollViewer.HorizontalOffset;
        _panOriginVerticalOffset = _scrollViewer.VerticalOffset;

        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);

        e.Handled = true;
    }

    private void EndPan(PointerRoutedEventArgs e)
    {
        if (!_isPanning)
        {
            return;
        }

        _isPanning = false;
        ReleasePointerCapture(e.Pointer);
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
    }

    private void Select(PlanNodeControl nodeControl)
    {
        if (ApplySelection(nodeControl, bringIntoView: false))
        {
            NodeSelected?.Invoke(this, nodeControl.Node);
        }
    }

    private void SelectByNode(PlanNode? node)
    {
        if (node is null)
        {
            ApplySelection(null, bringIntoView: false);
            return;
        }

        foreach (var child in Children)
        {
            if (child is PlanNodeControl nodeControl && ReferenceEquals(nodeControl.Node, node))
            {
                ApplySelection(nodeControl, bringIntoView: true);
                return;
            }
        }

        ApplySelection(null, bringIntoView: false);
    }

    private bool ApplySelection(PlanNodeControl? nodeControl, bool bringIntoView)
    {
        if (_selectedControl == nodeControl)
        {
            return false;
        }

        _selectedControl?.IsSelected = false;

        _selectedControl = nodeControl;

        if (nodeControl is not null)
        {
            nodeControl.IsSelected = !IsFullTrace;

            if (bringIntoView)
            {
                nodeControl.StartBringIntoView();
            }
        }

        _applyingSelection = true;
        SelectedNode = nodeControl?.Node;
        _applyingSelection = false;

        return true;
    }

    private bool IsGreyed(PlanNode node, PlanActivity activity)
        => IsFullTrace && !node.IsStatement && activity.HasActivePart;

    private void ShowEvaluations(PlanActivity activity)
    {
        foreach (var (node, calculator) in Calculators)
        {
            SetLit(calculator.Highlight, activity.Parts.Any(p => ReferenceEquals(p.CalculatorNode, node)));
        }
    }

    private void AddCalculators()
    {
        if (!IsFullTrace || Plan is null)
        {
            return;
        }

        foreach (var pulls in ExpressionPullResolver.Resolve(Plan).Where(p => p.OutputValues.Count > 0).GroupBy(p => p.Puller))
        {
            if (NodeControls.ContainsKey(pulls.Key))
            {
                Calculators[pulls.Key] = AddCalculator(pulls.Key, pulls.SelectMany(p => p.OutputValues).Distinct());
            }
        }

        foreach (var node in NodeControls.Keys.Where(n => OperatorClassifier.IsComputeScalar(n) && n.IsBatchMode))
        {
            Calculators[node] = AddCalculator(node, node.DefinedValues);
        }
    }

    private EvaluationCalculator AddCalculator(PlanNode puller, IEnumerable<DefinedValueInfo> values)
    {
        var idle = AddCalculatorImage(puller, isInactive: true);

        var lit = AddCalculatorImage(puller, isInactive: false);

        lit.IsHitTestVisible = false;

        ToolTipService.SetToolTip(idle, CreateExpressionList(values));

        return new EvaluationCalculator(idle, lit, new PlanHighlight(lit));
    }

    private Image AddCalculatorImage(PlanNode puller, bool isInactive)
    {
        var image = new Image
        {
            Width = CalculatorWidth,
            Height = CalculatorHeight,
            Stretch = Stretch.Fill,
            Source = CreateCalculatorSource(isInactive)
        };

        var iconLeft = GetLeft(NodeControls[puller]) + BaseNodeWidth / 2 - PlanNodeControl.IconHalfWidth;

        SetLeft(image, iconLeft - CalculatorGap - CalculatorWidth);

        SetTop(image, GetTop(NodeControls[puller]) + PlanNodeControl.IconCentre - CalculatorHeight / 2);

        Children.Add(image);

        return image;
    }

    private StackPanel CreateExpressionList(IEnumerable<DefinedValueInfo> values)
    {
        var list = new StackPanel { Spacing = ExpressionSpacing };

        foreach (var value in values)
        {
            list.Children.Add(new PredicateTextBox
            {
                Text = value.GetText(Plan?.Expressions),
                HasBackground = false,
                TextPadding = new Thickness(0)
            });
        }

        return list;
    }

    private DispatcherQueueTimer CreateRenderSettleTimer()
    {
        var timer = DispatcherQueue.CreateTimer();

        timer.Interval = TimeSpan.FromMilliseconds(RenderSettleMilliseconds);

        timer.IsRepeating = false;

        timer.Tick += OnRenderSettled;

        return timer;
    }

    private void OnRenderSettled(DispatcherQueueTimer sender, object args) => UpdateRenderScale(isSettled: true);

    private void SnapCalculators()
    {
        if (!IsLoaded || ScaleRoot is not { } root)
        {
            return;
        }

        foreach (var calculator in Calculators.Values)
        {
            SnapCalculator(calculator.Idle, root.RasterizationScale);

            SnapCalculator(calculator.Lit, root.RasterizationScale);
        }
    }

    private void SnapCalculator(Image image, double rasterizationScale)
    {
        if (image.RenderTransform is not CompositeTransform snap)
        {
            snap = new CompositeTransform();

            image.RenderTransform = snap;
        }

        var origin = TransformToVisual(null).TransformPoint(new Point(GetLeft(image), GetTop(image)));

        var pixelX = origin.X * rasterizationScale;

        var pixelY = origin.Y * rasterizationScale;

        snap.ScaleX = Math.Max(1, Math.Round(CalculatorWidth * RenderScale)) / (CalculatorWidth * RenderScale);

        snap.ScaleY = Math.Max(1, Math.Round(CalculatorHeight * RenderScale)) / (CalculatorHeight * RenderScale);

        snap.TranslateX = (Math.Round(pixelX) - pixelX) / RenderScale;

        snap.TranslateY = (Math.Round(pixelY) - pixelY) / RenderScale;
    }

    private SvgImageSource CreateCalculatorSource(bool isInactive)
    {
        var source = new SvgImageSource
        {
            RasterizePixelWidth = Math.Max(1, Math.Round(CalculatorWidth * RenderScale)),
            RasterizePixelHeight = Math.Max(1, Math.Round(CalculatorHeight * RenderScale))
        };

        if (PlanIconParts.GetCalculatorIcon(isInactive) is { } svg)
        {
            _ = PlanIconParts.LoadAsync(source, svg);
        }

        return source;
    }

    private void UpdateCurrentArrow()
    {
        if (CurrentStep is not { } step || !NodeControls.TryGetValue(step.Node, out var control))
        {
            CurrentArrow.Visibility = Visibility.Collapsed;

            return;
        }

        var scale = Math.Min(1 / Zoom, (CanvasMargin + PlanNodeControl.IconTop - CurrentArrowGap) / CurrentArrowSize);

        CurrentArrowTransform.ScaleX = scale;

        CurrentArrowTransform.ScaleY = scale;

        var line = step.Placement == PlanStepPlacement.Operator ? null : IteratorConnectors.GetValueOrDefault(step.Node);

        var lineRotation = step.Placement == PlanStepPlacement.Call ? CallArrowRotation : ReturnArrowRotation;

        CurrentArrowTransform.Rotation = line is null ? 0 : lineRotation;

        var aboveIcon = GetTop(control) + PlanNodeControl.IconTop - CurrentArrowGap - CurrentArrowSize * scale / 2;

        var centreX = line?.MiddleX ?? GetLeft(control) + BaseNodeWidth / 2;

        SetLeft(CurrentArrow, centreX - CurrentArrowSize / 2);

        SetTop(CurrentArrow, aboveIcon - CurrentArrowSize / 2);

        CurrentArrow.Visibility = Visibility.Visible;
    }

    private void UpdateActiveHighlights()
    {
        var active = ActiveNodes;

        foreach (var child in Children)
        {
            if (child is PlanNodeControl { Node: { } node } nodeControl)
            {
                nodeControl.IsActive = active is not null && active.Contains(node);
            }
        }
    }

    /// <summary>
    /// Reconciles the marching data-flow overlays with the active set: one line per active producer that
    /// has an outgoing connector, coloured by whether it is emitting (streaming, in its operator type
    /// colour) or still consuming (blocked, salmon). Updated incrementally so unchanged overlays keep
    /// animating without restarting, since the sets change on every playhead tick; an overlay is rebuilt
    /// only when its producer's streaming/blocked state flips.
    /// </summary>
    private void UpdateFlows()
    {
        if (IsFullTrace)
        {
            StopAllFlows();

            return;
        }

        var active = WithAdapters(ActiveNodes);
        var emitting = WithAdapters(EmittingNodes);

        // Tear down overlays whose producer is no longer active (or lost its connector), or whose
        // streaming/blocked state changed - those are rebuilt below with the new colour and tooltip.
        foreach (var node in _flows.Keys.ToList())
        {
            var stillActive = active is not null && active.Contains(node) && _connectorByProducer.ContainsKey(node);
            var isEmitting = emitting is not null && emitting.Contains(node);

            if (!stillActive || _flows[node].IsEmitting != isEmitting)
            {
                RemoveFlow(node);
            }
        }

        if (active is null)
        {
            return;
        }

        // Start an overlay for each active producer that carries rows to a consumer.
        foreach (var node in active)
        {
            if (!_flows.ContainsKey(node) && _connectorByProducer.TryGetValue(node, out var connector))
            {
                AddFlow(node, connector, emitting is not null && emitting.Contains(node));
            }
        }
    }

    /// <summary>
    /// Draws an overlay along the producer's outgoing connector. A streaming (non-blocking) producer reads
    /// as a vivid line in its operator type colour with the dashes marching toward the consumer
    /// ("Streaming"); a blocked one (consuming, not yet emitting) reads as a static salmon line ("Blocked")
    /// - nothing is flowing yet, so it isn't animated.
    /// </summary>
    private void AddFlow(PlanNode producer, Polyline connector, bool isEmitting)
    {
        var colour = isEmitting
            ? EventColourProvider.GetOperatorColour(producer).ToWindowsColor()
            : BlockedColor;

        // Match the connector's arrowhead(s) to the flow line so the whole connector reads as one colour.
        if (_arrowByProducer.TryGetValue(producer, out var arrow))
        {
            arrow.Fill = new SolidColorBrush(colour);
        }

        var overlay = new Polyline
        {
            Stroke = new SolidColorBrush(colour),
            StrokeThickness = 2.5,
            StrokeLineJoin = PenLineJoin.Round
        };

        ToolTipService.SetToolTip(overlay, isEmitting ? "Streaming" : "Blocked");

        foreach (var point in connector.Points)
        {
            overlay.Points.Add(point);
        }

        Children.Add(overlay);

        // Only a streaming producer animates - march the dashes toward the consumer. A blocked producer
        // stays a static coloured line.
        Storyboard? storyboard = null;

        if (isEmitting)
        {
            const double dashOn = 4;
            const double dashOff = 4;
            const double period = dashOn + dashOff;

            overlay.StrokeDashCap = PenLineCap.Round;
            overlay.StrokeDashArray = [dashOn, dashOff];

            var animation = new DoubleAnimation
            {
                From = 0,
                To = -period,
                Duration = new Duration(TimeSpan.FromSeconds(0.7)),
                RepeatBehavior = RepeatBehavior.Forever,
                EnableDependentAnimation = true
            };

            Storyboard.SetTarget(animation, overlay);
            Storyboard.SetTargetProperty(animation, "StrokeDashOffset");

            storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            storyboard.Begin();
        }

        _flows[producer] = new FlowOverlay(overlay, storyboard, isEmitting);
    }

    private void RemoveFlow(PlanNode producer)
    {
        if (_flows.Remove(producer, out var flow))
        {
            flow.Storyboard?.Stop();
            Children.Remove(flow.Overlay);

            // Restore the arrowhead(s) to the default connector grey now the producer is no longer active.
            if (_arrowByProducer.TryGetValue(producer, out var arrow))
            {
                arrow.Fill = new SolidColorBrush(ConnectorColor);
            }
        }
    }

    private void StopAllFlows()
    {
        foreach (var flow in _flows.Values)
        {
            flow.Storyboard?.Stop();
            Children.Remove(flow.Overlay);
        }

        _flows.Clear();
    }

    private static Polyline CreateCurrentArrow()
    {
        var arrow = new Polyline
        {
            Width = CurrentArrowSize,
            Height = CurrentArrowSize,
            Stroke = new SolidColorBrush(CurrentArrowColour),
            StrokeThickness = CurrentArrowThickness,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            IsHitTestVisible = false
        };

        foreach (var point in CurrentArrowPoints)
        {
            arrow.Points.Add(point);
        }

        return arrow;
    }

    private static void SetLit(PlanHighlight highlight, bool isLit)
    {
        if (isLit)
        {
            highlight.Light();
        }
        else
        {
            highlight.Fade();
        }
    }

    private bool IsOnPath(PlanNode node, IReadOnlyList<PlanNode> active)
        => active.Contains(node) || (OperatorClassifier.IsComputeScalar(node) && GetChildren(node).Any(c => IsOnPath(c, active)));

    private List<PlanNode> GetChildren(PlanNode node)
        => node.IsAdapter ? [SkipDeferred(node.Children[0])] : [.. node.Children.Select(GetShownChild)];

    private PlanNode GetShownChild(PlanNode child)
    {
        if (child.Adapter is { } adapter)
        {
            return adapter;
        }

        return IsDeferred(child) ? GetShownChild(child.Children[0]) : child;
    }

    private PlanNode SkipDeferred(PlanNode node) => IsDeferred(node) ? GetShownChild(node.Children[0]) : node;

    private bool IsDeferred(PlanNode node)
        => IsFullTrace && OperatorClassifier.IsComputeScalar(node) && !node.IsBatchMode && node.Children.Count == 1;

    private static PlanNode GetOperator(PlanNode node) => node.IsAdapter ? node.Children[0] : node;

    private static List<PlanNode>? WithAdapters(IReadOnlyList<PlanNode>? nodes)
        => nodes is null ? null : [.. nodes, .. nodes.Select(n => n.Adapter).OfType<PlanNode>()];

    private static PlanNodeControl? FindOperatorControl(object source)
        => FindAncestor<PlanNodeControl>(source as DependencyObject) is { Node.IsAdapter: false } control ? control : null;

    private static T? FindAncestor<T>(DependencyObject? start) where T : DependencyObject
    {
        for (var current = start; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is T match)
            {
                return match;
            }
        }

        return null;
    }

    private sealed class LeafCursor
    {
        public int Next { get; set; }
    }

    private readonly record struct FlowOverlay(Polyline Overlay, Storyboard? Storyboard, bool IsEmitting);

    private sealed record IteratorConnector(PlanHighlight Request, PlanHighlight Response, double MiddleX);

    private sealed record EvaluationCalculator(Image Idle, Image Lit, PlanHighlight Highlight);

    private static void OnSelectedNodeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ExecutionPlanControl)d;

        // Ignore the echo from our own click-driven selection; only react to external (timeline) changes.
        if (!control._applyingSelection)
        {
            control.SelectByNode((PlanNode?)e.NewValue);
        }
    }

    private static void OnCurrentStepChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ExecutionPlanControl)d).UpdateCurrentArrow();

    private static void OnActiveNodesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ExecutionPlanControl)d;

        control.UpdateActiveHighlights();
        control.UpdateFlows();
    }

    private static void OnEmittingNodesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ExecutionPlanControl)d).UpdateFlows();

    // The icicles (and the node height that makes room for them) depend on the events, so rebuild when they arrive.
    private static void OnEventsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ExecutionPlanControl)d).Rebuild();

    private static void OnPlanChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ExecutionPlanControl)d).Rebuild();

    private static void OnAnnotationsVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ExecutionPlanControl)d).Rebuild();

    private static void OnIsFullTraceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ExecutionPlanControl)d).Rebuild();

    /// <summary>
    /// Widens a depth column only where a node in it carries badges, so a plan with none keeps its original pitch
    /// </summary>
    private void MeasureColumns()
    {
        _annotationsByNode.Clear();

        _columnWidths.Clear();

        ColumnFanOuts.Clear();

        if (Plan is null)
        {
            return;
        }

        foreach (var root in Plan.Root)
        {
            MeasureNode(root, 0);
        }
    }

    private void MeasureNode(PlanNode node, int depth)
    {
        var annotations = AreAnnotationsVisible ? PlanNodeAnnotations.For(node) : [];

        _annotationsByNode[node] = annotations;

        while (_columnWidths.Count <= depth)
        {
            _columnWidths.Add(BaseNodeWidth);

            ColumnFanOuts.Add(0);
        }

        ColumnFanOuts[depth] = Math.Max(ColumnFanOuts[depth], node.Children.Count);

        if (annotations.Count > 0)
        {
            _columnWidths[depth] = BaseNodeWidth + PlanNodeControl.AnnotationColumnWidth;
        }

        foreach (var child in GetChildren(node))
        {
            MeasureNode(child, depth + 1);
        }
    }
}
