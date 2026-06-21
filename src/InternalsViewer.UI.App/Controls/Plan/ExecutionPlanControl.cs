using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Replay.Plans;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;

namespace InternalsViewer.UI.App.Controls.Plan;

public sealed class ExecutionPlanControl : Canvas
{
    private const double NodeWidth = 150;
    private const double NodeHeight = 100;
    private const double HorizontalGap = 46;
    private const double VerticalGap = 22;
    private const double Margin = 24;

    private const double ColumnPitch = NodeWidth + HorizontalGap;
    private const double RowPitch = NodeHeight + VerticalGap;

    private static readonly Color ConnectorColor = Color.FromArgb(255, 140, 140, 140);

    private ScrollViewer? _scrollViewer;
    private PlanNodeControl? _selectedControl;

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
    }

    public ExecutionPlan? Plan
    {
        get => (ExecutionPlan?)GetValue(PlanProperty);
        set => SetValue(PlanProperty, value);
    }

    public static readonly DependencyProperty PlanProperty =
        DependencyProperty.Register(nameof(Plan), typeof(ExecutionPlan), typeof(ExecutionPlanControl),
            new PropertyMetadata(null, OnPlanChanged));

    public PlanNode? SelectedNode
    {
        get => (PlanNode?)GetValue(SelectedNodeProperty);
        private set => SetValue(SelectedNodeProperty, value);
    }

    public static readonly DependencyProperty SelectedNodeProperty =
        DependencyProperty.Register(nameof(SelectedNode), typeof(PlanNode), typeof(ExecutionPlanControl),
            new PropertyMetadata(null));

    public PlanNode? ActiveNode
    {
        get => (PlanNode?)GetValue(ActiveNodeProperty);
        set => SetValue(ActiveNodeProperty, value);
    }

    public static readonly DependencyProperty ActiveNodeProperty =
        DependencyProperty.Register(nameof(ActiveNode), typeof(PlanNode), typeof(ExecutionPlanControl),
            new PropertyMetadata(null, OnActiveNodeChanged));

    private static void OnActiveNodeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ExecutionPlanControl)d).SelectByNode((PlanNode?)e.NewValue);

    public event EventHandler<PlanNode?>? NodeSelected;

    private static void OnPlanChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ExecutionPlanControl)d).Rebuild();

    private void Rebuild()
    {
        Children.Clear();
        _selectedControl = null;
        SelectedNode = null;

        if (Plan is null || Plan.Roots.Count == 0)
        {
            Width = 0;
            Height = 0;
            return;
        }

        var positions = new Dictionary<PlanNode, Point>();
        var leaf = new LeafCursor();

        foreach (var root in Plan.Roots)
        {
            AssignPositions(root, depth: 0, positions, leaf);
        }

        var totalCost = Plan.Roots.Sum(r => r.EstimatedCost ?? 0);

        foreach (var (node, point) in positions)
        {
            foreach (var child in node.Children)
            {
                DrawConnector(point, positions[child], RelativeCost(child, totalCost));
            }
        }

        foreach (var (node, point) in positions)
        {
            AddNode(node, point, RelativeCost(node, totalCost));
        }

        var maxX = positions.Values.Max(p => p.X);
        var maxY = positions.Values.Max(p => p.Y);

        Width = maxX + NodeWidth + Margin;
        Height = maxY + NodeHeight + Margin;

        SelectByNode(ActiveNode);
    }

    private static double AssignPositions(PlanNode node,
                                          int depth,
                                          Dictionary<PlanNode, Point> positions,
                                          LeafCursor leaf)
    {
        var x = Margin + depth * ColumnPitch;
        double y;

        if (node.Children.Count == 0)
        {
            y = Margin + leaf.Next * RowPitch;
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

    private void DrawConnector(Point parent, Point child, double childCostFraction)
    {
        var start = new Point(child.X, child.Y + NodeHeight / 2);
        var end = new Point(parent.X + NodeWidth, parent.Y + NodeHeight / 2);

        var elbowX = (start.X + end.X) / 2;

        var thickness = 1.25 + 4.0 * Math.Clamp(childCostFraction, 0, 1);

        var brush = new SolidColorBrush(ConnectorColor);

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
                end
            }
        };

        Children.Add(connector);

        const double headLength = 9;
        const double headHalfWidth = 4.5;

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

        SelectedNode = nodeControl?.Node;
        return true;
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
}
