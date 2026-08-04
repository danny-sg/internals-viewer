using System;
using System.Collections;
using System.Collections.Generic;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.UI.App.Models.Trace;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Controls.AccessSteps;

public sealed partial class AccessStepsControl : UserControl
{
    private const int IndentWidth = 16;

    public static readonly DependencyProperty StepHistoryProperty =
        DependencyProperty.Register(nameof(StepHistory),
                                    typeof(IEnumerable),
                                    typeof(AccessStepsControl),
                                    new PropertyMetadata(null));

    public static readonly DependencyProperty CurrentStepProperty =
        DependencyProperty.Register(nameof(CurrentStep),
                                    typeof(AccessStep),
                                    typeof(AccessStepsControl),
                                    new PropertyMetadata(null, OnCurrentStepChanged));

    public static readonly DependencyProperty NodesProperty =
        DependencyProperty.Register(nameof(Nodes),
                                    typeof(object),
                                    typeof(AccessStepsControl),
                                    new PropertyMetadata(null, OnNodesChanged));

    public static readonly DependencyProperty ShowDetailProperty =
        DependencyProperty.Register(nameof(ShowDetail),
                                    typeof(bool),
                                    typeof(AccessStepsControl),
                                    new PropertyMetadata(false, OnShowDetailChanged));

    public AccessStepsControl()
    {
        InitializeComponent();

        StepsList.ElementPrepared += OnElementPrepared;
        StepsList.ElementIndexChanged += OnElementIndexChanged;
    }

    /// <summary>
    /// What each plan node shows as in the timeline - its name, its colour and how deep it sits in the operator tree
    /// </summary>
    public IReadOnlyDictionary<int, TraceStepNode>? Nodes
    {
        get => (IReadOnlyDictionary<int, TraceStepNode>?)GetValue(NodesProperty);
        set => SetValue(NodesProperty, value);
    }

    public bool ShowDetail
    {
        get => (bool)GetValue(ShowDetailProperty);
        set => SetValue(ShowDetailProperty, value);
    }

    private static void OnShowDetailChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((AccessStepsControl)d).UpdateDetailLayout();
    }

    private bool _isDetailVisible;

    private GridLength _detailHeight = new(180);

    private void UpdateDetailLayout()
    {
        var isVisible = ShowDetail && CurrentStep is not null;

        if (isVisible == _isDetailVisible)
        {
            return;
        }

        _isDetailVisible = isVisible;

        if (isVisible)
        {
            DetailArea.Visibility = Visibility.Visible;
            DetailSplitter.Visibility = Visibility.Visible;

            DetailRow.Height = _detailHeight;

            return;
        }

        if (DetailRow.Height.IsAbsolute && DetailRow.Height.Value > 0)
        {
            _detailHeight = DetailRow.Height;
        }

        DetailArea.Visibility = Visibility.Collapsed;
        DetailSplitter.Visibility = Visibility.Collapsed;

        DetailRow.Height = new GridLength(0);
    }

    private readonly Dictionary<int, SolidColorBrush> _brushes = [];

    private readonly Dictionary<int, SolidColorBrush?> _levelBrushes = [];

    private int _maxDepth;

    private static void OnNodesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (AccessStepsControl)d;

        control._brushes.Clear();
        control._levelBrushes.Clear();

        control._maxDepth = 0;

        if (control.Nodes is { } nodes)
        {
            foreach (var node in nodes.Values)
            {
                if (node.Depth > control._maxDepth)
                {
                    control._maxDepth = node.Depth;
                }
            }
        }
    }

    private SolidColorBrush? LevelBrushFor(int depth)
    {
        var levelsAboveLeaf = _maxDepth - depth;

        if (levelsAboveLeaf <= 0)
        {
            return null;
        }

        if (!_levelBrushes.TryGetValue(levelsAboveLeaf, out var brush))
        {
            var alpha = (byte)System.Math.Min(levelsAboveLeaf * 6, 24);

            brush = new SolidColorBrush(Windows.UI.Color.FromArgb(alpha, 0, 0, 0));

            _levelBrushes[levelsAboveLeaf] = brush;
        }

        return brush;
    }

    private SolidColorBrush? BrushFor(int nodeId)
    {
        if (Nodes is null || !Nodes.TryGetValue(nodeId, out var node))
        {
            return null;
        }

        if (!_brushes.TryGetValue(nodeId, out var brush))
        {
            brush = new SolidColorBrush(node.Colour);

            _brushes[nodeId] = brush;
        }

        return brush;
    }

    private void OnElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        => StyleRow(args.Element as Grid, args.Index);

    private void OnElementIndexChanged(ItemsRepeater sender, ItemsRepeaterElementIndexChangedEventArgs args)
        => StyleRow(args.Element as Grid, args.NewIndex);

    private void StyleRow(Grid? grid, int index)
    {
        if (grid is null
            || StepsList.ItemsSourceView is not { } view
            || index < 0
            || index >= view.Count
            || view.GetAt(index) is not AccessStep step)
        {
            return;
        }

        var showName = index == 0
                       || view.GetAt(index - 1) is not AccessStep newer
                       || newer.NodeId != step.NodeId;

        ApplyNodeStyling(grid, step, applyIndent: true, showName: showName);
    }

    private void ApplyNodeStyling(Grid grid, AccessStep step, bool applyIndent, bool showName)
    {
        var node = Nodes?.GetValueOrDefault(step.NodeId);

        var brush = BrushFor(step.NodeId);

        if (grid.FindName("SourceName") is TextBlock sourceName)
        {
            sourceName.Text = showName ? node?.Name ?? string.Empty : string.Empty;
        }

        if (grid.FindName("SourceBlob") is Border blob)
        {
            blob.Background = brush;

            blob.Visibility = showName && brush is not null ? Visibility.Visible : Visibility.Collapsed;

            if (blob.Parent is StackPanel gutter)
            {
                gutter.Tag = step.NodeId;

                gutter.Tapped -= OnGutterTapped;
                gutter.Tapped += OnGutterTapped;
            }
        }

        UpdateEmitBadge(grid, brush, step is AccessStep.Row { IsReadAhead: true });

        UpdateCompareBadge(grid, step, node);

        if (grid.FindName("ProbeExpandToggle") is ToggleButton toggle)
        {
            toggle.IsChecked = false;
        }

        if (applyIndent)
        {
            grid.Margin = new Thickness(IndentWidth * (node?.Depth ?? 0), 0, 0, 0);

            grid.Background = LevelBrushFor(node?.Depth ?? _maxDepth);
        }

        if (brush is null)
        {
            grid.BorderThickness = new Thickness(0);

            return;
        }

        grid.BorderBrush = brush;
        grid.BorderThickness = new Thickness(3, 0, 0, 0);
    }

    public event EventHandler<int>? NodeActivated;

    private void OnGutterTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (sender is StackPanel { Tag: int nodeId })
        {
            NodeActivated?.Invoke(this, nodeId);
        }
    }

    /// <summary>
    /// Tags a merge comparison with the side it advances, in that side's colour
    /// </summary>
    private void UpdateCompareBadge(Grid grid, AccessStep step, TraceStepNode? node)
    {
        if (grid.FindName("CompareBadge") is not Border badge)
        {
            return;
        }

        var comparison = step switch
        {
            AccessStep.MergeCompare compare => compare.Comparison,
            AccessStep.MergeCompareRun run => run.Comparison,
            MergeCompareSpan span => span.Progress.Direction,
            _ => 0
        };

        var sideNodeId = comparison < 0 ? node?.OuterInputNodeId ?? -1 : node?.InnerInputNodeId ?? -1;

        var brush = sideNodeId >= 0 ? BrushFor(sideNodeId) : null;

        var arrow = grid.FindName("CompareArrow") as TextBlock;

        if (comparison == 0 || brush is null)
        {
            badge.Visibility = Visibility.Collapsed;

            if (arrow is not null)
            {
                arrow.Visibility = Visibility.Collapsed;
            }

            return;
        }

        if (grid.FindName("CompareBadgeText") is TextBlock text)
        {
            text.Text = comparison < 0 ? "Advance Outer" : "Advance Inner";
        }

        badge.Background = brush;
        badge.Visibility = Visibility.Visible;

        if (arrow is not null)
        {
            arrow.Visibility = Visibility.Visible;
        }
    }

    private static void UpdateEmitBadge(Grid grid, SolidColorBrush? sideBrush, bool isReadAhead)
    {
        if (grid.FindName("EmitBadge") is not Border badge)
        {
            return;
        }

        if (grid.FindName("EmitSideText") is TextBlock sideText)
        {
            sideText.Text = isReadAhead ? "(read ahead)" : string.Empty;
            sideText.Visibility = isReadAhead ? Visibility.Visible : Visibility.Collapsed;
        }

        if (sideBrush is not null)
        {
            badge.Tag ??= badge.Background;

            badge.Background = sideBrush;
        }
        else if (badge.Tag is Brush original)
        {
            badge.Background = original;

            badge.Tag = null;
        }
    }

    /// <summary>
    /// The full history of steps taken by the access path
    /// </summary>
    public IEnumerable? StepHistory
    {
        get => (IEnumerable?)GetValue(StepHistoryProperty);
        set => SetValue(StepHistoryProperty, value);
    }

    /// <summary>
    /// The most recently taken step
    /// </summary>
    public AccessStep? CurrentStep
    {
        get => (AccessStep?)GetValue(CurrentStepProperty);
        set => SetValue(CurrentStepProperty, value);
    }

    private static void OnCurrentStepChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (AccessStepsControl)d;

        if (e.NewValue is not null)
        {
            control.StepsScroller.ChangeView(null, 0, null, true);

            control.DispatcherQueue.TryEnqueue(control.StyleDetail);
        }

        control.UpdateDetailLayout();
    }

    private void StyleDetail()
    {
        if (CurrentStep is not { } step)
        {
            return;
        }

        var node = Nodes?.GetValueOrDefault(step.NodeId);

        DetailName.Text = node?.Name ?? string.Empty;

        DetailSubtitle.Text = node?.Subtitle ?? string.Empty;
        DetailSubtitle.Visibility = DetailSubtitle.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        DetailBlob.Background = BrushFor(step.NodeId);

        DetailDescription.Text = TraceStepDescriber.Describe(step, Nodes);

        if (DetailHost.ContentTemplateRoot is Grid grid)
        {
            ApplyNodeStyling(grid, step, applyIndent: false, showName: false);
        }
    }
}
