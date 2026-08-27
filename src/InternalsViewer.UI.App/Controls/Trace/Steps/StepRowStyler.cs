using System;
using System.Collections.Generic;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.UI.App.Models.Query.Trace;
using InternalsViewer.UI.App.Models.Query.Trace.Steps;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Controls.Trace.Steps;

public sealed class StepRowStyler
{
    private const int IndentWidth = 16;

    private readonly Dictionary<int, SolidColorBrush> _brushes = [];

    private readonly Dictionary<int, SolidColorBrush?> _levelBrushes = [];

    private int _maxDepth;

    private IReadOnlyDictionary<int, TraceStepNode>? _nodes;

    public Action<int>? NodeActivated { get; set; }

    public TraceBlobPalette? Palette { get; set; }

    public void SetNodes(IReadOnlyDictionary<int, TraceStepNode>? nodes)
    {
        _nodes = nodes;

        _brushes.Clear();
        _levelBrushes.Clear();

        _maxDepth = 0;

        if (nodes is { } values)
        {
            foreach (var node in values.Values)
            {
                if (node.Depth > _maxDepth)
                {
                    _maxDepth = node.Depth;
                }
            }
        }
    }

    public SolidColorBrush? BrushFor(int nodeId)
    {
        if (_nodes is null || !_nodes.TryGetValue(nodeId, out var node))
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

    public void ApplyNodeStyling(Grid grid, AccessStep step, bool showName)
    {
        var node = NodeFor(step.NodeId);

        var brush = BrushFor(step.NodeId);

        if (grid.FindName("SourceGutter") is StepSourceGutter gutter)
        {
            gutter.NodeName = showName ? node?.Name ?? string.Empty : string.Empty;

            gutter.BlobBrush = BlobBrushFor(step.NodeId);

            gutter.IsBlobVisible = showName && brush is not null;

            gutter.NodeId = step.NodeId;

            gutter.Tapped -= OnGutterTapped;
            gutter.Tapped += OnGutterTapped;
        }

        UpdateEmitBadge(grid, brush, step is AccessStep.Row { IsReadAhead: true });

        UpdateCompareBadge(grid, step, node);

        if (grid.FindName("ProbeExpandToggle") is ToggleButton toggle)
        {
            toggle.IsChecked = false;
        }

        grid.Margin = new Thickness(IndentWidth * (node?.Depth ?? 0), 0, 0, 0);

        grid.Background = LevelBrushFor(node?.Depth ?? _maxDepth);

        if (brush is null)
        {
            grid.BorderThickness = new Thickness(0);

            return;
        }

        grid.BorderBrush = brush;
        grid.BorderThickness = new Thickness(3, 0, 0, 0);
    }

    private Brush? BlobBrushFor(int nodeId)
        => NodeFor(nodeId) is { } node && Palette is { } palette ? palette.For(nodeId, node.Colour) : BrushFor(nodeId);

    private TraceStepNode? NodeFor(int nodeId) => _nodes?.GetValueOrDefault(nodeId);

    private SolidColorBrush? LevelBrushFor(int depth)
    {
        var levelsAboveLeaf = _maxDepth - depth;

        if (levelsAboveLeaf <= 0)
        {
            return null;
        }

        if (!_levelBrushes.TryGetValue(levelsAboveLeaf, out var brush))
        {
            var alpha = (byte)Math.Min(levelsAboveLeaf * 6, 24);

            brush = new SolidColorBrush(Windows.UI.Color.FromArgb(alpha, 0, 0, 0));

            _levelBrushes[levelsAboveLeaf] = brush;
        }

        return brush;
    }

    private void OnGutterTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is StepSourceGutter gutter)
        {
            NodeActivated?.Invoke(gutter.NodeId);
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

            arrow?.Visibility = Visibility.Collapsed;

            return;
        }

        if (grid.FindName("CompareBadgeText") is TextBlock text)
        {
            text.Text = comparison < 0 ? "Advance Outer" : "Advance Inner";
        }

        badge.Background = brush;
        badge.Visibility = Visibility.Visible;

        arrow?.Visibility = Visibility.Visible;
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
}
