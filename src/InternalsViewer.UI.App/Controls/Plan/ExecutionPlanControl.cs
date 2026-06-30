using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;
using Windows.UI;
using InternalsViewer.Query.Plans;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;

namespace InternalsViewer.UI.App.Controls.Plan;

public sealed class ExecutionPlanControl : Canvas
{
    private const double NodeWidth = 150;
    private const double NodeHeight = 90;
    private const double HorizontalGap = 46;
    private const double VerticalGap = 22;
    private const double CanvasMargin = 24;

    private const double ColumnPitch = NodeWidth + HorizontalGap;
    private const double RowPitch = NodeHeight + VerticalGap;

    private static readonly Color ConnectorColor = Color.FromArgb(255, 185, 185, 185);

    // Flow-line colour for a blocked producer (active but not yet emitting - still consuming its input).
    private static readonly Color BlockedColor = Color.FromArgb(255, 245, 102, 102);

    // Each producer node mapped to the connector that carries its rows to its consumer (parent), and to
    // that connector's arrowhead (recoloured to match the flow line while the producer is active).
    private readonly Dictionary<PlanNode, Polyline> _connectorByProducer = new();
    private readonly Dictionary<PlanNode, Polygon> _arrowByProducer = new();

    // Per-emitting-producer animated data-flow overlay marching along its outgoing connector. Keyed by
    // producer so the set can be reconciled incrementally as the active operators change each tick.
    private readonly Dictionary<PlanNode, FlowOverlay> _flows = new();

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

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += OnPointerCaptureLost;
        ContextRequested += OnContextRequested;
    }

    public ExecutionPlan? Plan
    {
        get => (ExecutionPlan?)GetValue(PlanProperty);
        set => SetValue(PlanProperty, value);
    }

    public static readonly DependencyProperty PlanProperty =
        DependencyProperty.Register(nameof(Plan), typeof(ExecutionPlan), typeof(ExecutionPlanControl),
            new PropertyMetadata(null, OnPlanChanged));

    /// <summary>
    /// The selected node (from a timeline click or an in-plan click). Drives the selection highlight and
    /// brings the node into view; independent of the time-derived active set.
    /// </summary>
    public PlanNode? SelectedNode
    {
        get => (PlanNode?)GetValue(SelectedNodeProperty);
        set => SetValue(SelectedNodeProperty, value);
    }

    public static readonly DependencyProperty SelectedNodeProperty =
        DependencyProperty.Register(nameof(SelectedNode), typeof(PlanNode), typeof(ExecutionPlanControl),
            new PropertyMetadata(null, OnSelectedNodeChanged));

    private static void OnSelectedNodeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ExecutionPlanControl)d;

        // Ignore the echo from our own click-driven selection; only react to external (timeline) changes.
        if (!control._applyingSelection)
        {
            control.SelectByNode((PlanNode?)e.NewValue);
        }
    }

    /// <summary>
    /// The operators whose run-time span contains the playhead. Highlighted as "running now"; a parallel
    /// query lights up several at once.
    /// </summary>
    public IReadOnlyList<PlanNode>? ActiveNodes
    {
        get => (IReadOnlyList<PlanNode>?)GetValue(ActiveNodesProperty);
        set => SetValue(ActiveNodesProperty, value);
    }

    public static readonly DependencyProperty ActiveNodesProperty =
        DependencyProperty.Register(nameof(ActiveNodes), typeof(IReadOnlyList<PlanNode>), typeof(ExecutionPlanControl),
            new PropertyMetadata(null, OnActiveNodesChanged));

    private static void OnActiveNodesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ExecutionPlanControl)d;

        control.UpdateActiveHighlights();
        control.UpdateFlows();
    }

    /// <summary>
    /// The active operators that have started emitting rows (the rest are still consuming / blocked).
    /// Determines each flow line's colour and tooltip: emitting lines are vivid in the operator type
    /// colour and labelled "Streaming"; blocked lines are salmon and labelled "Blocked".
    /// </summary>
    public IReadOnlyList<PlanNode>? EmittingNodes
    {
        get => (IReadOnlyList<PlanNode>?)GetValue(EmittingNodesProperty);
        set => SetValue(EmittingNodesProperty, value);
    }

    public static readonly DependencyProperty EmittingNodesProperty =
        DependencyProperty.Register(nameof(EmittingNodes), typeof(IReadOnlyList<PlanNode>), typeof(ExecutionPlanControl),
            new PropertyMetadata(null, OnEmittingNodesChanged));

    private static void OnEmittingNodesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ExecutionPlanControl)d).UpdateFlows();

    public event EventHandler<PlanNode?>? NodeSelected;

    /// <summary>Raised when "Open Index" is chosen on a data-access node that runs against a named index.</summary>
    public event EventHandler<PlanNode>? IndexOpenRequested;

    private static void OnPlanChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ExecutionPlanControl)d).Rebuild();

    private void Rebuild()
    {
        StopAllFlows();
        Children.Clear();
        _connectorByProducer.Clear();
        _arrowByProducer.Clear();
        _selectedControl = null;

        if (Plan is null || Plan.Root.Count == 0)
        {
            Width = 0;
            Height = 0;
            return;
        }

        var positions = new Dictionary<PlanNode, Point>();
        var leaf = new LeafCursor();

        foreach (var root in Plan.Root)
        {
            AssignPositions(root, depth: 0, positions, leaf);
        }

        var totalCost = Plan.Root.Sum(r => r.EstimatedCost ?? 0);

        foreach (var (node, point) in positions)
        {
            foreach (var child in node.Children)
            {
                var (connector, arrow) = DrawConnector(point, positions[child], RelativeCost(child, totalCost));

                _connectorByProducer[child] = connector;
                _arrowByProducer[child] = arrow;
            }
        }

        foreach (var (node, point) in positions)
        {
            AddNode(node, point, RelativeCost(node, totalCost));
        }

        var maxX = positions.Values.Max(p => p.X);
        var maxY = positions.Values.Max(p => p.Y);

        Width = maxX + NodeWidth + CanvasMargin;
        Height = maxY + NodeHeight + CanvasMargin;

        SelectByNode(SelectedNode);
        UpdateActiveHighlights();
        UpdateFlows();
    }

    private static double AssignPositions(PlanNode node,
                                          int depth,
                                          Dictionary<PlanNode, Point> positions,
                                          LeafCursor leaf)
    {
        var x = CanvasMargin + depth * ColumnPitch;
        double y;

        if (node.Children.Count == 0)
        {
            y = CanvasMargin + leaf.Next * RowPitch;
            leaf.Next++;
        }
        else
        {
            var first = 0d;

            for (var i = 0; i < node.Children.Count; i++)
            {
                var childY = AssignPositions(node.Children[i], depth + 1, positions, leaf);

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
            CostPercent = costFraction,
            Width = NodeWidth,
            Height = NodeHeight
        };

        SetLeft(control, point.X);
        SetTop(control, point.Y);

        Children.Add(control);
    }

    private (Polyline Connector, Polygon Arrow) DrawConnector(Point parent, Point child, double childCostFraction)
    {
        var start = new Point(child.X, child.Y + NodeHeight / 2);
        var end = new Point(parent.X + NodeWidth, parent.Y + NodeHeight / 2);

        var elbowX = (start.X + end.X) / 2;

        var thickness = 1.25 + 4.0 * Math.Clamp(childCostFraction, 0, 1);

        // Scale the arrowhead with the line so a thick "many rows" connector gets a proportionally larger
        // head that still reads as an arrow; it must stay wider than the shaft so the shaft can't poke out.
        var headHalfWidth = Math.Max(4.5, thickness * 1.4);
        var headLength = headHalfWidth * 2.0;

        var brush = new SolidColorBrush(ConnectorColor);

        // Stop the shaft at the arrowhead's base rather than the node edge, so the squared line end is
        // hidden behind the head and doesn't stick out past the (zero-width) tip.
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

        var arrow = new Polygon
        {
            Fill = brush,
            Points =
            {
                new Point(end.X, end.Y),
                new Point(end.X + headLength, end.Y - headHalfWidth),
                new Point(end.X + headLength, end.Y + headHalfWidth)
            }
        };

        Children.Add(arrow);

        return (connector, arrow);
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
        var nodeControl = FindAncestor<PlanNodeControl>(e.OriginalSource as DependencyObject);

        if (nodeControl is not null)
        {
            Select(nodeControl);
            e.Handled = true;
            return;
        }

        BeginPan(e);
    }

    private void OnContextRequested(UIElement sender, ContextRequestedEventArgs e)
    {
        var nodeControl = FindAncestor<PlanNodeControl>(e.OriginalSource as DependencyObject);

        // Only data-access operators (scan/seek/lookup) that run against a named index get the item.
        if (nodeControl?.Node is not { } node ||
            string.IsNullOrEmpty(node.Index) ||
            OperatorClassifier.GetCategory(node) != OperatorCategory.DataAccess)
        {
            return;
        }

        var flyout = new MenuFlyout();
        var openIndex = new MenuFlyoutItem { Text = $"Open Index: {node.Index}" };
        openIndex.Click += (_, _) => IndexOpenRequested?.Invoke(this, node);
        flyout.Items.Add(openIndex);

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

        if (_selectedControl is not null)
        {
            _selectedControl.IsSelected = false;
        }

        _selectedControl = nodeControl;

        if (nodeControl is not null)
        {
            nodeControl.IsSelected = true;

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

    /// <summary>
    /// Highlights every node whose operator is active at the playhead (running now), distinct from the
    /// click selection. A parallel query lights up several at once.
    /// </summary>
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
        var active = ActiveNodes;
        var emitting = EmittingNodes;

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

        // Match the connector's arrowhead to the flow line so the whole connector reads as one colour.
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
            overlay.StrokeDashArray = new DoubleCollection { dashOn, dashOff };

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

            // Restore the arrowhead to the default connector grey now the producer is no longer active.
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
}
