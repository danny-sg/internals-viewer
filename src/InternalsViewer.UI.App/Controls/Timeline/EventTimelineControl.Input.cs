using System;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Plans.Operators;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace InternalsViewer.UI.App.Controls.Timeline;

public sealed partial class EventTimelineControl
{
    // Pointer tolerance (px) around the playhead triangle and range handles for hit-testing.
    private const double HitArea = 7;

    // Two presses within this window count as a double-click.
    private const long DoubleClickMs = 300;

    private enum DragTarget { None, Start, End, Playhead }

    private bool IsOnTriangle(double x, double y)
        => y <= MarkerStripHeight + HitArea && Math.Abs(x - PlayheadX) <= TriangleHalfWidth + HitArea;

    private DragTarget HitTest(double x, double y)
    {
        if (y <= MarkerStripHeight + HitArea)
        {
            // The triangle sits on top and is wider, so a press anywhere on it grabs the playhead.
            if (Math.Abs(x - PlayheadX) <= TriangleHalfWidth)
            {
                return DragTarget.Playhead;
            }

            // The from/to handles are only the short triangles at the bottom of the band, so a press
            // higher in the strip (over the ruler) scrubs rather than grabbing a handle.
            if (y >= MarkerStripHeight - HandleHeight - HitArea)
            {
                var dStart = Math.Abs(x - StartDrawX);
                var dEnd = Math.Abs(x - EndDrawX);

                if (dStart <= HitArea && dStart <= dEnd)
                {
                    return DragTarget.Start;
                }

                if (dEnd <= HitArea)
                {
                    return DragTarget.End;
                }
            }
        }

        return DragTarget.Playhead;
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_sortedEvents.Count == 0)
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

        var isDoubleClick = now - _lastPressTicks <= DoubleClickMs && Math.Abs(position.X - _lastPressX) <= HitArea;

        _lastPressTicks = now;
        _lastPressX = position.X;

        if (isDoubleClick && IsOnTriangle(position.X, position.Y))
        {
            // Reset: drop the selection so the handles re-attach to the playhead (= select all).
            DeactivateSelection();

            _skCanvas.Invalidate();

            return;
        }

        // Scrubbing and the range handles live in the ruler strip only. A press over the rows
        // selects the plan operator under it rather than moving the playhead.
        if (position.Y > MarkerStripHeight)
        {
            SelectOperatorAt(position, isDoubleClick);
            return;
        }

        _overlay.CapturePointer(e.Pointer);

        _dragTarget = HitTest(position.X, position.Y);
        _isDragging = true;

        // Clicking the strip moves the playhead immediately so a plain click scrubs.
        if (_dragTarget == DragTarget.Playhead)
        {
            MovePlayheadToX(position.X);
            _skCanvas.Invalidate();
        }
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

        var canTrace = op.PlanNodeIdentifier.NodeId < 0
                       || op.Name.Equals("Top", StringComparison.OrdinalIgnoreCase)
                       || op.Name.Equals("Concatenation", StringComparison.OrdinalIgnoreCase)
                       || op.Name.Equals("Sort", StringComparison.OrdinalIgnoreCase)
                       || op is { Category: OperatorCategory.DataAccess, TableName.Length: > 0 }
                       || (op.Category == OperatorCategory.Join
                           && (op.Name.Contains("Nested Loops", StringComparison.OrdinalIgnoreCase)
                               || op.Name.Contains("Merge Join", StringComparison.OrdinalIgnoreCase)
                               || (op.Name.Contains("Hash Match", StringComparison.OrdinalIgnoreCase)
                                   && op.LogicalOperator.Contains("Join", StringComparison.OrdinalIgnoreCase))));

        if (canTrace)
        {
            var openTrace = new MenuFlyoutItem { Text = "Trace" };

            openTrace.Click += (_, _) => TraceOpenRequested?.Invoke(op);

            flyout.Items.Add(openTrace);
        }

        flyout.ShowAt(_overlay, new FlyoutShowOptions { Position = position });

        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging)
        {
            UpdateHoverTooltip(e.GetCurrentPoint(_overlay).Position);
            return;
        }

        var x = Math.Clamp(e.GetCurrentPoint(_overlay).Position.X, RowLabelWidth, CanvasWidth);

        var t = XToTime(x);

        switch (_dragTarget)
        {
            case DragTarget.Start:
                _selectionActivated = true;

                _startTime = Math.Min(t, _endTime);

                ConfinePlayheadToSelection();

                EmitScope();

                break;

            case DragTarget.End:
                _selectionActivated = true;

                _endTime = Math.Max(t, _startTime);

                ConfinePlayheadToSelection();

                EmitScope();
                break;

            case DragTarget.Playhead:
                MovePlayheadToX(x);
                break;
        }

        _skCanvas.Invalidate();
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _overlay.ReleasePointerCaptures();
        _isDragging = false;
        _dragTarget = DragTarget.None;
    }

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
            _toolTipText.Text = region.Event.Detail;
        }

        _toolTip.HorizontalOffset = position.X + 12;
        _toolTip.VerticalOffset = position.Y + 12;
        _toolTip.IsOpen = true;
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
        if (_sortedEvents.Count == 0)
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
        _scrollX = RowLabelWidth + (timeAtCursor - _minTime) / _timeRange * ContentWidth - cursorX;

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

    private void MovePlayheadToX(double x)
    {
        var (lo, hi) = ActiveRange;

        _playheadTime = Math.Clamp(XToTime(x), lo, hi);

        SyncHandlesToPlayhead();

        FirePlayhead();
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
