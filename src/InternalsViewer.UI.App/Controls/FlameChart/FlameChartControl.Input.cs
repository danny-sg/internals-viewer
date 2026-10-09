using System;
using System.Collections.Generic;
using InternalsViewer.Query.CallStack.TimeTravel.Timeline;
using InternalsViewer.UI.App.Helpers;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using Windows.System;

namespace InternalsViewer.UI.App.Controls.FlameChart;

public sealed partial class FlameChartControl
{
    private const double DragThreshold = 4;

    private const double MinimumZoomWidth = 4;

    private const long DoubleClickMs = 300;

    private const double ZoomStep = 1.25;

    private const double WheelPan = 0.1;

    private const double TooltipGap = 12;

    private const double HitTolerance = 1.5;

    private FlameHit? _hover;

    private FlameHit? _selected;

    private bool _isPressed;

    private bool _isSelecting;

    private bool _isPanning;

    private bool _isScrubbing;

    private bool _isStretching;

    private bool _isOverBar;

    private bool _hoverOnPopout;

    private Point _stretchPress;

    private float _stretchX;

    private float _stretchY;

    private float _stretchShare;

    private bool _hasStretched;

    private bool _stretchPopout;

    private PopoutBlock _stretchBlock;

    private Point _pressPoint;

    private Point _dragPoint;

    private double _panViewStart;

    private double _panScrollY;

    private long _lastPressTicks;

    private Point _lastPressPoint;

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_timeline is null)
        {
            return;
        }

        var point = e.GetCurrentPoint(_overlay);

        var position = point.Position;

        HideToolTip();

        if (point.Properties.IsMiddleButtonPressed)
        {
            _isPanning = true;
            _pressPoint = position;
            _panViewStart = _viewStart;
            _panScrollY = _scrollY;

            _overlay.CapturePointer(e.Pointer);

            return;
        }

        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        var now = Environment.TickCount64;

        var isDoubleClick = now - _lastPressTicks <= DoubleClickMs
                            && Math.Abs(position.X - _lastPressPoint.X) <= DragThreshold
                            && Math.Abs(position.Y - _lastPressPoint.Y) <= DragThreshold;

        _lastPressTicks = now;
        _lastPressPoint = position;

        if (position.Y < ContentTop && (IsOnPlayhead(position.X) || BlockAt(position) is null))
        {
            if (isDoubleClick)
            {
                _playhead = null;

                _canvas.Invalidate();

                return;
            }

            _isScrubbing = true;

            MovePlayheadTo(position.X);

            _overlay.CapturePointer(e.Pointer);

            return;
        }

        if (BlockAt(position) is { } found)
        {
            BeginStretch(found.Block, found.IsPopout, position);

            _overlay.CapturePointer(e.Pointer);

            return;
        }

        if (HeaderAt(position) is { } lane)
        {
            ToggleLane(lane);

            return;
        }

        if (isDoubleClick)
        {
            if (HitTest(position) is { } hit && SpanOf(hit) is { } span)
            {
                var margin = (span.End - span.Start) * 0.05;

                SetView(span.Start - margin, span.End + margin);
            }
            else
            {
                ZoomToFit();
            }

            return;
        }

        _isPressed = true;
        _isSelecting = false;
        _pressPoint = position;
        _dragPoint = position;

        _overlay.CapturePointer(e.Pointer);
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(_overlay).Position;

        if (_isPanning)
        {
            var scale = PixelsPerUnit((int)_overlay.ActualWidth);

            var range = _viewEnd - _viewStart;

            _scrollY = _panScrollY - (position.Y - _pressPoint.Y);

            var start = _panViewStart - (position.X - _pressPoint.X) / scale;

            SetView(start, start + range);

            return;
        }

        if (_isScrubbing)
        {
            MovePlayheadTo(position.X);

            return;
        }

        if (_isStretching)
        {
            Stretch(position);

            return;
        }

        if (_isPressed)
        {
            _dragPoint = position;

            if (!_isSelecting && Distance(_pressPoint, position) > DragThreshold)
            {
                _isSelecting = true;
            }

            if (_isSelecting)
            {
                _canvas.Invalidate();
            }

            return;
        }

        var found = BlockAt(position);

        UpdateCursor(found is not null);

        var hit = found is { } block ? block.Block.Source : HitTest(position);

        var onPopout = found?.IsPopout == true;

        if (hit != _hover || onPopout != _hoverOnPopout)
        {
            _hover = hit;

            _hoverOnPopout = onPopout;

            _canvas.Invalidate();
        }

        if (hit is { } hovered)
        {
            ShowToolTip(hovered, position);
        }
        else
        {
            HideToolTip();
        }
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(_overlay).Position;

        if (_isSelecting)
        {
            _dragPoint = position;

            ZoomToSelection();
        }
        else if (_isPressed)
        {
            Click(HitTest(position));
        }
        else if (_isStretching && !_hasStretched)
        {
            Click(_stretchBlock.Source);
        }

        EndPointer();

        _overlay.ReleasePointerCapture(e.Pointer);
    }

    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e) => EndPointer();

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (_isPressed || _isPanning || _isScrubbing || _isStretching)
        {
            return;
        }

        HideToolTip();

        if (_hover is not null)
        {
            _hover = null;

            _canvas.Invalidate();
        }
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (_timeline is null)
        {
            return;
        }

        var point = e.GetCurrentPoint(_overlay);

        var delta = point.Properties.MouseWheelDelta;

        var modifiers = e.KeyModifiers;

        var range = _viewEnd - _viewStart;

        if (point.Properties.IsHorizontalMouseWheel || modifiers.HasFlag(VirtualKeyModifiers.Shift))
        {
            var shift = -Math.Sign(delta) * range * WheelPan;

            SetView(_viewStart + shift, _viewEnd + shift);
        }
        else if (modifiers.HasFlag(VirtualKeyModifiers.Control))
        {
            _scrollY -= delta;

            ClampScroll();

            UpdateScrollBars();

            _canvas.Invalidate();
        }
        else
        {
            var width = Math.Max(_overlay.ActualWidth, 1);

            var anchor = _viewStart + point.Position.X / width * range;

            var factor = delta > 0 ? 1 / ZoomStep : ZoomStep;

            var newRange = range * factor;

            var start = anchor - point.Position.X / width * newRange;

            SetView(start, start + newRange);
        }

        HideToolTip();

        e.Handled = true;
    }

    private void ZoomToSelection()
    {
        var left = Math.Min(_pressPoint.X, _dragPoint.X);

        var right = Math.Max(_pressPoint.X, _dragPoint.X);

        _scrollY += Math.Max(0, Math.Min(_pressPoint.Y, _dragPoint.Y) - ContentTop);

        if (right - left < MinimumZoomWidth)
        {
            ClampScroll();

            UpdateScrollBars();

            _canvas.Invalidate();

            return;
        }

        var scale = PixelsPerUnit((int)_overlay.ActualWidth);

        SetView(_viewStart + left / scale, _viewStart + right / scale);
    }

    private void Select(FlameHit? hit)
    {
        _selected = hit;

        _canvas.Invalidate();

        if (hit is not { } selected || SpanOf(selected) is not { } span || _visible?.NodeOf(span.Node) is not { } node)
        {
            return;
        }

        CallSelected?.Invoke(node, span.Call);
    }

    private void Deselect()
    {
        if (_selected is null)
        {
            SelectionCleared?.Invoke();

            return;
        }

        _selected = null;

        _canvas.Invalidate();

        SelectionCleared?.Invoke();
    }

    private void EndPointer()
    {
        var invalidate = _isSelecting;

        _isPressed = false;
        _isSelecting = false;
        _isPanning = false;
        _isScrubbing = false;
        _isStretching = false;

        if (invalidate)
        {
            _canvas.Invalidate();
        }
    }

    private void MovePlayheadTo(double x)
    {
        var scale = PixelsPerUnit((int)_overlay.ActualWidth);

        _playhead = Math.Clamp(_viewStart + x / scale, _viewStart, _viewEnd);

        HideToolTip();

        _canvas.Invalidate();
    }

    private bool IsOnPlayhead(double x)
        => _playhead is { } playhead
           && Math.Abs((playhead - _viewStart) * PixelsPerUnit((int)_overlay.ActualWidth) - x) <= PlayheadHalfWidth + DragThreshold;

    private (PopoutBlock Block, bool IsPopout)? BlockAt(Point position)
    {
        if (position.Y >= _overlay.ActualHeight - BandHeight)
        {
            return null;
        }

        for (var index = _popoutBlocks.Count - 1; index >= 0; index--)
        {
            var block = _popoutBlocks[DrawIndex(index)];

            if (block.Extrusion > 0 && Covers(block, position))
            {
                return (block, true);
            }
        }

        if (!ShowMemory)
        {
            return null;
        }

        for (var index = _spikes.Count - 1; index >= 0; index--)
        {
            if (Covers(_spikes[index], position))
            {
                return (_spikes[index], false);
            }
        }

        return null;
    }

    private bool Covers(PopoutBlock block, Point position)
    {
        var (fromX, toX) = SweepRange((float)position.X, block.Left, block.Right, _directionX * block.Extrusion);

        var (fromY, toY) = SweepRange((float)position.Y, block.Top, block.Top + BarHeight, _directionY * block.Extrusion);

        return Math.Max(0, Math.Max(fromX, fromY)) <= Math.Min(1, Math.Min(toX, toY));
    }

    private static (float From, float To) SweepRange(float point, float minimum, float maximum, float distance)
    {
        if (distance == 0)
        {
            return point >= minimum && point <= maximum ? (float.MinValue, float.MaxValue) : (1, 0);
        }

        var first = (point - maximum) / distance;

        var second = (point - minimum) / distance;

        return (Math.Min(first, second), Math.Max(first, second));
    }

    private void Click(FlameHit? hit)
    {
        if (hit is null || hit == _selected)
        {
            if (!IsLocked)
            {
                Deselect();
            }
        }
        else
        {
            Select(hit);
        }
    }

    private void BeginStretch(PopoutBlock block, bool popout, Point position)
    {
        _isStretching = true;

        _hasStretched = false;

        _stretchPopout = popout;

        _stretchBlock = block;

        _stretchPress = position;

        _stretchX = _directionX * block.Extrusion;

        _stretchY = _directionY * block.Extrusion;

        _stretchShare = ShareOf(block.Ratio);

        _extrusionAngle = MathF.Atan2(_directionY, _directionX);

        _extrusionLength = _drawnLength;

        HideToolTip();
    }

    private void Stretch(Point position)
    {
        if (!_hasStretched && Distance(_stretchPress, position) <= DragThreshold)
        {
            return;
        }

        _hasStretched = true;

        var x = _stretchX + (float)(position.X - _stretchPress.X);

        var y = _stretchY + (float)(position.Y - _stretchPress.Y);

        var angle = MathF.Atan2(Math.Min(y, 0f), x);

        if (angle > 0)
        {
            angle = -MathF.PI;
        }

        var directionX = MathF.Cos(angle);

        var directionY = MathF.Sin(angle);

        var length = Math.Max(MinimumExtrusion, x * directionX + y * directionY);

        var maximum = MaximumLength(_stretchPopout ? _popoutBlocks : _spikes,
                                    directionX,
                                    directionY,
                                    (int)_overlay.ActualWidth,
                                    (float)_overlay.ActualHeight - BandHeight,
                                    horizontal: _stretchPopout);

        _extrusionAngle = angle;

        _extrusionLength = Math.Clamp(MinimumExtrusion + (length - MinimumExtrusion) / _stretchShare, MinimumExtrusion, maximum);

        _canvas.Invalidate();
    }

    private void UpdateCursor(bool overBar)
    {
        if (overBar == _isOverBar)
        {
            return;
        }

        _isOverBar = overBar;

        ProtectedCursor = InputSystemCursor.Create(overBar ? InputSystemCursorShape.SizeAll : InputSystemCursorShape.Arrow);
    }

    private FlameHit? HitTest(Point position)
    {
        if (_visible is not { } timeline || position.Y < ContentTop || position.Y >= _overlay.ActualHeight - BandHeight)
        {
            return null;
        }

        var contentY = position.Y - ContentTop + _scrollY;

        for (var lane = 0; lane < timeline.Threads.Count; lane++)
        {
            var thread = timeline.Threads[lane];

            var inLane = contentY - _laneTops[lane] - LaneHeaderHeight;

            if (contentY < _laneTops[lane] || contentY >= _laneTops[lane] + LaneHeight(thread))
            {
                continue;
            }

            var depth = (int)Math.Floor(inLane / _rowHeight);

            if (inLane < 0 || depth >= DepthOf(thread))
            {
                return null;
            }

            var scale = PixelsPerUnit((int)_overlay.ActualWidth);

            var index = thread.Rows[depth].IndexAt(_axis, _viewStart + position.X / scale, HitTolerance / scale);

            return index < 0 ? null : new FlameHit(lane, depth, index);
        }

        return null;
    }

    private int? HeaderAt(Point position)
    {
        if (_visible is not { Threads.Count: > 1 } timeline
            || position.Y < ContentTop
            || position.Y >= _overlay.ActualHeight - BandHeight)
        {
            return null;
        }

        var contentY = position.Y - ContentTop + _scrollY;

        for (var lane = 0; lane < timeline.Threads.Count && lane < _laneTops.Length; lane++)
        {
            if (contentY >= _laneTops[lane] && contentY < _laneTops[lane] + LaneHeaderHeight)
            {
                return lane;
            }
        }

        return null;
    }

    private TimeTravelTimelineSpan? SpanOf(FlameHit hit)
    {
        if (_visible is not { } timeline || hit.Lane >= timeline.Threads.Count)
        {
            return null;
        }

        var row = timeline.Threads[hit.Lane].Rows[hit.Depth];

        return hit.Index < row.Count ? row.Span(_axis, hit.Index) : null;
    }

    private void ShowToolTip(FlameHit hit, Point position)
    {
        if (_visible is not { } timeline)
        {
            return;
        }

        var thread = timeline.Threads[hit.Lane];

        var row = thread.Rows[hit.Depth];

        var instructions = row.Span(TimeTravelTimelineAxis.Instructions, hit.Index);

        var positions = row.Span(TimeTravelTimelineAxis.Position, hit.Index);

        var node = timeline.NodeOf(instructions.Node);

        var lines = new List<string>
        {
            LabelOf(instructions.Node),
            node?.Category is { Length: > 0 } category ? category : "Unknown",
            instructions.Call >= 0
                ? $"Thread {thread.ThreadId}  ·  Call {instructions.Call + 1:N0}  ·  Depth {hit.Depth}"
                : $"Thread {thread.ThreadId}  ·  Depth {hit.Depth}",
            $"Instructions {instructions.End - instructions.Start:N0}",
            $"Position {Math.Floor(positions.Start):N0} to {Math.Floor(positions.End):N0}"
        };

        var (allocatedBytes, allocations) = timeline.AllocatedDuring(thread.ThreadId, positions.Start, positions.End);

        if (allocations > 0)
        {
            lines.Add($"Allocated {SizeFormat.Format((long)allocatedBytes)} In {allocations:N0} "
                      + (allocations == 1 ? "Allocation" : "Allocations"));
        }

        if (timeline.FreedDuring(thread.ThreadId, positions.Start, positions.End) is > 0 and var freed)
        {
            lines.Add($"Freed {SizeFormat.Format((long)freed)}");
        }

        if (allocations > 0)
        {
            var retained = timeline.RetainedBy(thread.ThreadId, positions.Start, positions.End);

            lines.Add($"Retained At Return {SizeFormat.Format((long)retained)}");
        }

        if (instructions.Flags.HasFlag(TimeTravelSpanFlags.StartUnknown))
        {
            lines.Add("Started Before The Recorded Call");
        }

        if (!instructions.Flags.HasFlag(TimeTravelSpanFlags.Returned))
        {
            lines.Add("Did Not Return");
        }

        _toolTipText.Text = string.Join(Environment.NewLine, lines);

        _toolTip.IsOpen = true;

        _toolTipText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        var width = _toolTipText.DesiredSize.Width + 12;

        var left = position.X + TooltipGap + width > _overlay.ActualWidth
            ? position.X - TooltipGap - width
            : position.X + TooltipGap;

        _toolTip.HorizontalOffset = Math.Max(0, left);

        _toolTip.VerticalOffset = position.Y + TooltipGap;
    }

    private void HideToolTip() => _toolTip.IsOpen = false;

    private static double Distance(Point a, Point b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    private readonly record struct FlameHit(int Lane, int Depth, int Index);
}
