using System;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Plans.Operators;
using InternalsViewer.UI.App.Models.Query.Timeline.Enums;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace InternalsViewer.UI.App.Controls.Query.Timeline;

public sealed partial class EventTimelineControl
{
    private const double DoubleClickDistance = 7;

    // Two presses within this window count as a double-click.
    private const long DoubleClickMs = 300;

    // Gap between the pointer and the tooltip, on whichever side the tooltip is shown.
    private const double TooltipGap = 12;

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_definition.Events.Count == 0)
        {
            return;
        }

        HideTooltip();

        var point = e.GetCurrentPoint(_overlay);

        // Right-click is reserved for the context menu (ContextRequested)
        if (point.Properties.IsRightButtonPressed)
        {
            return;
        }

        var position = point.Position;

        var now = Environment.TickCount64;

        var isDoubleClick = now - _lastPressTicks <= DoubleClickMs && Math.Abs(position.X - _lastPressX) <= DoubleClickDistance;

        _lastPressTicks = now;
        _lastPressX = position.X;

        SelectOperatorAt(position, isDoubleClick);
    }

    private void SelectOperatorAt(Windows.Foundation.Point position, bool isDoubleClick)
    {
        var hit = HitTestRegion(position.X, position.Y);

        if (hit is null)
        {
            // Clicking empty space clears the selected operator's row-flow overlay.
            ClearOperatorSelection();
            return;
        }

        if (hit.Value.Event is ExecutionOperatorEvent { PlanNodeIdentifier: { } node } op)
        {
            // Track the selection so its row-flow path (child→parent, lit while emitting) is drawn, and the object it
            // accesses so locks on the same table highlight with it.
            _selection.Select(node.NodeId, op.SchemaName, op.TableName);

            _skCanvas.Invalidate();

            PlanNodeSelected?.Invoke(node);
        }
        else
        {
            // A point marker (read/lock/wait/log): reveal that event in the event grid, or open its page on a
            // double click.
            ClearOperatorSelection();

            if (isDoubleClick)
            {
                EventDoubleClicked?.Invoke(hit.Value.Event);
            }
            else
            {
                EventSelected?.Invoke(hit.Value.Event);
            }
        }
    }

    private void ClearOperatorSelection()
    {
        if (_selection.NodeId is null)
        {
            return;
        }

        _selection.Clear();
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

        if (hit?.Event is not ExecutionOperatorEvent op || op.PlanNodeIdentifier is null)
        {
            return;
        }

        var flyout = new MenuFlyout();

        var openPlan = new MenuFlyoutItem { Text = "Execution Plan" };

        openPlan.Click += (_, _) => ExecutionPlanRequested?.Invoke(op);

        flyout.Items.Add(openPlan);

        // Only data-access operators (scan/seek/lookup) that run against a named index get the item
        if (op is { Category: OperatorCategory.DataAccess, IndexName.Length: > 0 })
        {
            var openIndex = new MenuFlyoutItem { Text = $"Open Index: {op.IndexName}" };

            openIndex.Click += (_, _) => IndexOpenRequested?.Invoke(op);

            flyout.Items.Add(openIndex);
        }

        var canSimulate = op.PlanNodeIdentifier.NodeId < 0
                       || op.Name.Equals("Top", StringComparison.OrdinalIgnoreCase)
                       || op.Name.Equals("Concatenation", StringComparison.OrdinalIgnoreCase)
                       || op.Name.Equals("Sort", StringComparison.OrdinalIgnoreCase)
                       || op is { Category: OperatorCategory.DataAccess, TableName.Length: > 0 }
                       || (op.Category == OperatorCategory.Join
                           && (op.Name.Contains("Nested Loops", StringComparison.OrdinalIgnoreCase)
                               || op.Name.Contains("Merge Join", StringComparison.OrdinalIgnoreCase)
                               || (op.Name.Contains("Hash Match", StringComparison.OrdinalIgnoreCase)
                                   && op.LogicalOperator.Contains("Join", StringComparison.OrdinalIgnoreCase))));

        if (canSimulate)
        {
            var openSimulation = new MenuFlyoutItem { Text = "Simulate" };

            openSimulation.Click += (_, _) => SimulationOpenRequested?.Invoke(op);

            flyout.Items.Add(openSimulation);
        }

        flyout.ShowAt(_overlay, new FlyoutShowOptions { Position = position });

        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e) => UpdateHoverTooltip(e.GetCurrentPoint(_overlay).Position);

    private void OnPointerExited(object sender, PointerRoutedEventArgs e) => HideTooltip();

    private void HideTooltip()
    {
        _toolTip.IsOpen = false;
        _hoverEvent = null;
        _hoverLabel = null;
    }

    /// <summary>
    /// Shows a pointer-following tooltip with the name of the event under the pointer
    /// </summary>
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
            _toolTipText.Text = WithStructure(region.Event);
        }

        PositionTooltip(position);

        _toolTip.IsOpen = true;
    }

    /// <summary>
    /// Places the tooltip below and right of the pointer, flipping it to the left when it would run past the right edge
    /// </summary>
    /// <remarks>
    /// The popup is offset within the overlay rather than laid out by it, so nothing else keeps it inside the control.
    /// Its child is measured here because a popup that has never been open has no desired size of its own yet.
    /// </remarks>
    private string WithStructure(EngineEvent engineEvent)
    {
        if (ResolveStructure is not { } resolve)
        {
            return engineEvent.Detail;
        }

        var page = engineEvent switch
        {
            PageEngineEvent { PageAddress: { } address } => address,
            ReadEventGroup { PageAddress: { } address } => address,
            _ => (PageAddress?)null
        };

        return page is { } value && resolve(value) is { Length: > 0 } structure
               ? engineEvent.Detail + Environment.NewLine + structure
               : engineEvent.Detail;
    }

    private void PositionTooltip(Windows.Foundation.Point position)
    {
        var x = position.X + TooltipGap;

        if (_toolTip.Child is FrameworkElement content)
        {
            content.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));

            if (x + content.DesiredSize.Width > _overlay.ActualWidth)
            {
                x = Math.Max(0, position.X - TooltipGap - content.DesiredSize.Width);
            }
        }

        _toolTip.HorizontalOffset = x;
        _toolTip.VerticalOffset = position.Y + TooltipGap;
    }

    private (EngineEvent Event, string? Label)? HitTestRegion(double x, double y)
    {
        var pointX = (float)x;
        var pointY = (float)y;

        HitRegion? best = null;

        var bestWidth = float.MaxValue;
        var bestDistance = float.MaxValue;

        for (var i = _hitRegions.Count - 1; i >= 0; i--)
        {
            var region = _hitRegions[i];

            if (!region.Bounds.Contains(pointX, pointY))
            {
                continue;
            }

            var width = region.Bounds.Width;

            var distance = Math.Abs(region.Bounds.MidX - pointX);

            if (width < bestWidth || (width == bestWidth && distance < bestDistance))
            {
                best = region;

                bestWidth = width;
                bestDistance = distance;
            }
        }

        return best is { } hit ? (hit.Event, hit.Label) : null;
    }

    private void OnOverlaySizeChanged(object sender, SizeChangedEventArgs e)
    {
        ClampScroll();

        UpdateScrollBar();

        _skCanvas.Invalidate();
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (_definition.Events.Count == 0)
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
        _scrollX = BandLabelWidth + (timeAtCursor - _minTime) / _timeRange * ContentWidth - cursorX;

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

    private void MovePlayheadToTime(double time)
    {
        var (lo, hi) = ActiveRange;

        _playheadTime = Math.Clamp(time, lo, hi);

        SyncHandlesToPlayhead();

        FirePlayhead();
    }

    private void MoveSelectionHandle(TimelineRulerHandle handle, double time)
    {
        _selectionActivated = true;

        if (handle == TimelineRulerHandle.Start)
        {
            _startTime = Math.Min(time, _endTime);
        }
        else
        {
            _endTime = Math.Max(time, _startTime);
        }

        ConfinePlayheadToSelection();

        EmitScope();
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

    /// <summary>
    /// Double-click reset to deactivate the selection and snap the handles to the playhead
    /// </summary>
    private void DeactivateSelection()
    {
        _selectionActivated = false;
        SyncHandlesToPlayhead();
        EmitScope();
    }
}
