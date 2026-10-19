using System;
using System.Collections.Generic;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.UI.App.Helpers;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using Windows.System;

namespace InternalsViewer.UI.App.Controls.Trace;

public sealed partial class TraceTimelineControl
{
    private const double DragThreshold = 4;

    private const double MinimumZoomWidth = 4;

    private const long DoubleClickMs = 300;

    private const double ZoomStep = 1.25;

    private const double WheelPan = 0.1;

    private const double TooltipGap = 12;

    internal const double HitTolerance = 1.5;

    private FlameHit? _hover;

    private FlameHit? _selected;

    private bool _isPressed;

    private bool _isSelecting;

    private bool _isShiftPressed;

    private bool _pressedOperators;

    private bool _isDragging;

    private bool _isPanning;

    private bool _isScrubbing;

    private bool _isStretching;

    private InputSystemCursorShape _cursorShape = InputSystemCursorShape.Arrow;

    private Block? _hoverBlock;

    private Point _stretchPress;

    private float _stretchX;

    private float _stretchY;

    private float _stretchShare;

    private bool _hasStretched;

    private Block _stretchBlock;

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

        LogKeyboard($"press focus={_keyboardHost.Focus(FocusState.Pointer)}");

        if (point.Properties.IsMiddleButtonPressed)
        {
            e.Handled = true;

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

        e.Handled = true;

        var now = Environment.TickCount64;

        var isDoubleClick = now - _lastPressTicks <= DoubleClickMs
                            && Math.Abs(position.X - _lastPressPoint.X) <= DragThreshold
                            && Math.Abs(position.Y - _lastPressPoint.Y) <= DragThreshold;

        _lastPressTicks = now;
        _lastPressPoint = position;

        if (position.Y < OperatorsTop && (IsOnPlayhead(position.X) || BlockAt(position) is null))
        {
            if (isDoubleClick)
            {
                _playhead = null;

                ClearSelections();

                _canvas.Invalidate();

                return;
            }

            _isScrubbing = true;

            MovePlayheadTo(position.X);

            _overlay.CapturePointer(e.Pointer);

            return;
        }

        if (IsOnSplitter(position))
        {
            BeginOperatorsResize(position);

            _overlay.CapturePointer(e.Pointer);

            return;
        }

        if (BlockAt(position) is { } found)
        {
            BeginStretch(found, position);

            _overlay.CapturePointer(e.Pointer);

            return;
        }

        var inOperators = _operators.Contains(position);

        if (inOperators && isDoubleClick)
        {
            if (_operators.GetReadAt(position) is { } read)
            {
                ZoomTo(read.Of(_axis).Start, read.Of(_axis).End);
            }
            else if (_operators.GetOperatorAt(position) is { } operatorHit)
            {
                ZoomToOperator(operatorHit);
            }
            else if (_operators.IsBackground(position))
            {
                ClearSelections();
            }

            return;
        }

        if (HeaderAt(position) is { } lane)
        {
            ToggleLane(lane);

            return;
        }

        if (!inOperators && isDoubleClick)
        {
            if (HitTest(position) is { } hit && SpanOf(hit) is { } span)
            {
                ZoomTo(span.Start, span.End);
            }
            else
            {
                ClearSelections();
            }

            return;
        }

        _isPressed = true;
        _isSelecting = false;
        _pressedOperators = inOperators;
        _isShiftPressed = e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift);
        _pressPoint = position;
        _dragPoint = position;
        _panViewStart = _viewStart;

        _overlay.CapturePointer(e.Pointer);
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(_overlay).Position;

        if (_isPanning)
        {
            _scrollY = _panScrollY - (position.Y - _pressPoint.Y);

            PanTo(position);

            return;
        }

        if (_isScrubbing)
        {
            MovePlayheadTo(position.X);

            return;
        }

        if (_isResizingOperators)
        {
            ResizeOperators(position);

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

            if (!_isSelecting && !_isDragging && Distance(_pressPoint, position) > DragThreshold)
            {
                _isSelecting = _isShiftPressed;

                _isDragging = !_isShiftPressed;

                if (_isDragging)
                {
                    UpdateCursor(InputSystemCursorShape.SizeWestEast);
                }
            }

            if (_isSelecting)
            {
                _canvas.Invalidate();
            }
            else if (_isDragging)
            {
                PanTo(position);
            }

            return;
        }

        UpdateHover(position);
    }

    private void PanTo(Point position)
        => ScrollViewTo(_panViewStart - (position.X - _pressPoint.X) / PixelsPerUnit((int)_overlay.ActualWidth));

    private void UpdateHover(Point position)
    {
        var found = BlockAt(position);

        var operatorsHover = found is null ? _operators.GetHover(position) : TraceOperatorsHover.None;

        UpdateCursor(IsOnSplitter(position)
                         ? InputSystemCursorShape.SizeNorthSouth
                         : found is not null
                             ? InputSystemCursorShape.SizeAll
                             : _operators.IsOnOutputRows(position)
                                 ? InputSystemCursorShape.Cross
                                 : operatorsHover.ReturnStrip is not null || operatorsHover.Read is not null
                                     ? InputSystemCursorShape.Hand
                                     : InputSystemCursorShape.Arrow);

        var hit = found?.Call ?? HitTest(position) ?? GetDurationHit(operatorsHover, position) ?? GetReadHit(operatorsHover.Read);

        if (hit != _hover
            || !Nullable.Equals(found, _hoverBlock)
            || operatorsHover != _operators.Hover)
        {
            _hover = hit;

            _hoverBlock = found;

            _operators.Hover = operatorsHover;

            _canvas.Invalidate();
        }

        if (hit is { } hovered && operatorsHover.Read is null)
        {
            ShowToolTip(hovered, position);
        }
        else if (_operators.GetToolTip(operatorsHover, position) is { } text)
        {
            OpenToolTip(text, position);
        }
        else
        {
            HideToolTip();
        }
    }

    private FlameHit? GetDurationHit(TraceOperatorsHover hover, Point position)
        => hover.ReturnStrip is { } strip && _operators.GetCallDuration(strip, position) is { } duration && _visible is { } timeline
            ? FindCall(timeline, duration.Node, duration.Call)
            : null;

    private FlameHit? GetReadHit(TraceRead? read)
        => read is not null && _visible is { } timeline ? FindCall(timeline, read.Node, read.Call) : null;

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(_overlay).Position;

        if (_isSelecting)
        {
            _dragPoint = position;

            ZoomToSelection();
        }
        else if (_isPressed && !_isDragging && _pressedOperators)
        {
            _operators.Click(position);
        }
        else if (_isPressed && !_isDragging && _isShiftPressed)
        {
            RootAt(HitTest(position));
        }
        else if (_isPressed && !_isDragging)
        {
            Click(HitTest(position));
        }
        else if (_isStretching && !_hasStretched)
        {
            Click(_stretchBlock.Call);
        }

        EndPointer();

        _overlay.ReleasePointerCapture(e.Pointer);
    }

    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e) => EndPointer();

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (_isPressed || _isPanning || _isScrubbing || _isStretching || _isResizingOperators)
        {
            return;
        }

        HideToolTip();

        UpdateCursor(InputSystemCursorShape.Arrow);

        if (_hover is not null || _hoverBlock is not null || _operators.Hover != TraceOperatorsHover.None)
        {
            _hover = null;

            _hoverBlock = null;

            _operators.Hover = TraceOperatorsHover.None;

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
            ScrollViewTo(_viewStart - Math.Sign(delta) * range * WheelPan);
        }
        else if (modifiers.HasFlag(VirtualKeyModifiers.Control))
        {
            _scrollY -= delta;

            ApplyScroll();
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
            ApplyScroll();

            return;
        }

        var scale = PixelsPerUnit((int)_overlay.ActualWidth);

        SetView(_viewStart + left / scale, _viewStart + right / scale);
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        LogKeyboard($"key {e.Key} selected={_selected}");

        if (e.Key is not (VirtualKey.Up or VirtualKey.Down or VirtualKey.Left or VirtualKey.Right)
            || _selected is not { } hit
            || _visible is not { } timeline
            || hit.Lane >= timeline.Threads.Count)
        {
            return;
        }

        e.Handled = true;

        var rows = timeline.Threads[hit.Lane].Rows;

        FlameHit? next = e.Key switch
        {
            VirtualKey.Up => GetCaller(rows, hit),
            VirtualKey.Down => GetStackCallee(hit) ?? GetFirstCallee(rows, hit),
            VirtualKey.Left => GetAdjacentCall(rows, hit, forward: false),
            _ => GetAdjacentCall(rows, hit, forward: true)
        };

        if (next is { } target)
        {
            SelectAndShow(target);
        }
    }

    private void SelectAndShow(FlameHit hit)
    {
        Select(hit);

        BringIntoView(hit);
    }

    private FlameHit? GetCaller(IReadOnlyList<TraceTimelineRow> rows, FlameHit hit)
    {
        if (hit.Depth == 0 || hit.Depth >= rows.Count)
        {
            return null;
        }

        var row = rows[hit.Depth];

        var middle = (row.Starts(_axis)[hit.Index] + row.Ends(_axis)[hit.Index]) / 2;

        var index = rows[hit.Depth - 1].IndexAt(_axis, middle, 0);

        return index < 0 ? null : hit with { Depth = hit.Depth - 1, Index = index };
    }

    private FlameHit? GetFirstCallee(IReadOnlyList<TraceTimelineRow> rows, FlameHit hit)
    {
        if (hit.Depth + 1 >= rows.Count)
        {
            return null;
        }

        var row = rows[hit.Depth];

        var callees = rows[hit.Depth + 1];

        var first = callees.FirstStartingFrom(_axis, row.Starts(_axis)[hit.Index], 0);

        return first < callees.Count && callees.Ends(_axis)[first] <= row.Ends(_axis)[hit.Index]
            ? hit with { Depth = hit.Depth + 1, Index = first }
            : null;
    }

    private static FlameHit? GetAdjacentCall(IReadOnlyList<TraceTimelineRow> rows, FlameHit hit, bool forward)
    {
        var index = forward ? hit.Index + 1 : hit.Index - 1;

        return index >= 0 && index < rows[hit.Depth].Count ? hit with { Index = index } : null;
    }

    private FlameHit? GetStackCallee(FlameHit hit)
    {
        var path = GetStackPath();

        var index = path.IndexOf(hit);

        return index >= 0 && index + 1 < path.Count ? path[index + 1] : null;
    }

    private List<FlameHit> GetStackPath()
    {
        var path = new List<FlameHit>();

        if (_stackFrame is not { } frame
            || _visible is not { } timeline
            || frame.Lane >= timeline.Threads.Count
            || SpanOf(frame) is null)
        {
            return path;
        }

        var rows = timeline.Threads[frame.Lane].Rows;

        for (FlameHit? hit = frame; hit is { } current; hit = GetCaller(rows, current))
        {
            path.Add(current);
        }

        path.Reverse();

        return path;
    }

    private void OnContextRequested(UIElement sender, ContextRequestedEventArgs e)
    {
        if (_timeline is null)
        {
            return;
        }

        HideToolTip();

        var hasPosition = e.TryGetPosition(_overlay, out var position);

        var flyout = hasPosition && _operators.Contains(position)
            ? CreateOperatorsMenu(_operators.GetPlanNode(position))
            : CreateChartMenu(hasPosition ? HitTest(position) : _selected);

        if (hasPosition)
        {
            flyout.ShowAt(_overlay, new FlyoutShowOptions { Position = position });
        }
        else
        {
            flyout.ShowAt(_overlay);
        }

        e.Handled = true;
    }

    private MenuFlyout CreateChartMenu(FlameHit? hit)
    {
        CallStackNode? node = null;

        var call = -1;

        var span = hit is { } target ? SpanOf(target) : null;

        if (span is { } found)
        {
            node = _visible?.NodeOf(found.Node);
            call = found.Call;
        }

        var flyout = new MenuFlyout();

        if (_selected is not null)
        {
            flyout.Items.Add(MenuItems.Create("Zoom to Selection", ZoomToSelectedCall));
        }

        flyout.Items.Add(MenuItems.Create("Zoom to Fit", ZoomToFit));

        if (_selected is not null)
        {
            flyout.Items.Add(MenuItems.Create("Clear Selection", Deselect));
        }

        if (node is not null)
        {
            flyout.Items.Add(MenuItems.Create("Scope", () => SetRoot(node)));
        }

        if (_root is not null)
        {
            flyout.Items.Add(MenuItems.Create("Clear Scope", () => SetRoot(null)));
        }

        flyout.Items.Add(new MenuFlyoutSeparator());

        flyout.Items.Add(MenuItems.Create("Open Call Tree", () => CallTreeOpenRequested?.Invoke(node, call)));

        if (hit is { } opened && span is not null)
        {
            flyout.Items.Add(MenuItems.Create("Open Call Stack", () => OpenCallStack(opened)));
        }

        flyout.Items.Add(new MenuFlyoutSeparator());

        flyout.Items.Add(MenuItems.SubMenu("View", MenuItems.Toggle("Operators", ShowOperators, isChecked => ShowOperators = isChecked)));

        flyout.Items.Add(CreateMemoryMenu());

        return flyout;
    }

    private void OpenCallStack(FlameHit hit)
    {
        if (hit != _selected)
        {
            Select(hit);
        }

        CallStackOpenRequested?.Invoke();
    }

    private void Select(FlameHit? hit)
    {
        SetSelected(hit);

        ClearOutputRowSelection();

        _canvas.Invalidate();

        if (hit is not { } selected || SpanOf(selected) is not { } span || _visible?.NodeOf(span.Node) is not { } node)
        {
            return;
        }

        if (AutoZoom)
        {
            ZoomToSelectedCall();
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

        SetSelected(null);

        _canvas.Invalidate();

        SelectionCleared?.Invoke();
    }

    private void SetSelected(FlameHit? hit)
    {
        _stackFrame = hit is { } selected && GetStackPath().Contains(selected) ? _stackFrame : hit;

        _selected = hit;
    }

    private void ClearSelections()
    {
        _operators.ClearSelectedOperator();

        ClearOutputRowSelection();

        Deselect();
    }

    private void ClearOutputRowSelection()
    {
        if (IsPlaying || _playhead is not { } playhead || _operators.OutputRows?.GetRow(_axis, playhead) is null)
        {
            return;
        }

        _playhead = null;

        _canvas.Invalidate();
    }

    private void EndPointer()
    {
        var invalidate = _isSelecting;

        if (_isDragging)
        {
            UpdateCursor(InputSystemCursorShape.Arrow);
        }

        _isPressed = false;
        _isSelecting = false;
        _isDragging = false;
        _isPanning = false;
        _isScrubbing = false;
        _isStretching = false;
        _isResizingOperators = false;

        if (invalidate)
        {
            _canvas.Invalidate();
        }
    }

    private void MovePlayheadTo(double x)
    {
        var scale = PixelsPerUnit((int)_overlay.ActualWidth);

        _playhead = Math.Clamp(_viewStart + x / scale, Math.Max(_viewStart, FullStart), Math.Min(_viewEnd, FullEnd));

        HideToolTip();

        _canvas.Invalidate();
    }

    private bool IsOnPlayhead(double x)
        => _playhead is { } playhead
           && Math.Abs((playhead - _viewStart) * PixelsPerUnit((int)_overlay.ActualWidth) - x) <= PlayheadHalfWidth + DragThreshold;

    private Block? BlockAt(Point position)
    {
        if (!ShowMemory || position.Y >= _overlay.ActualHeight - BandHeight)
        {
            return null;
        }

        for (var index = _spikes.Count - 1; index >= 0; index--)
        {
            if (Covers(_spikes[index], position))
            {
                return _spikes[index];
            }
        }

        return null;
    }

    private bool Covers(Block block, Point position)
    {
        var (fromX, toX) = SweepRange((float)position.X, block.Left, block.Right, _directionX * block.Extrusion);

        var (fromY, toY) = SweepRange((float)position.Y, block.Top, block.Top + block.Height, _directionY * block.Extrusion);

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

    private void ZoomToSelectedCall()
    {
        if (_selected is not { } selected || SpanOf(selected) is not { } span)
        {
            return;
        }

        BringIntoView(selected);

        ZoomTo(span.Start, span.End);
    }

    private void RootAt(FlameHit? hit)
        => SetRoot(hit is { } at && SpanOf(at) is { } span ? _visible?.NodeOf(span.Node) : null);

    private void Click(FlameHit? hit)
    {
        if (hit is null)
        {
            _operators.ClearSelectedOperator();

            Deselect();
        }
        else if (hit == _selected)
        {
            Deselect();
        }
        else
        {
            Select(hit);
        }
    }

    private void BeginStretch(Block block, Point position)
    {
        _isStretching = true;

        _hasStretched = false;

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

        var angle = MathF.Atan2(y, x);

        var directionX = MathF.Cos(angle);

        var directionY = MathF.Sin(angle);

        var length = Math.Max(MinimumExtrusion, x * directionX + y * directionY);

        var maximum = MaximumLength(Flat(_spikes),
                                    directionX,
                                    directionY,
                                    (int)_overlay.ActualWidth,
                                    (float)_overlay.ActualHeight - BandHeight);

        _extrusionAngle = angle;

        _extrusionLength = Math.Clamp(MinimumExtrusion + (length - MinimumExtrusion) / _stretchShare, MinimumExtrusion, maximum);

        _canvas.Invalidate();
    }

    private void UpdateCursor(InputSystemCursorShape shape)
    {
        if (shape == _cursorShape)
        {
            return;
        }

        _cursorShape = shape;

        ProtectedCursor = InputSystemCursor.Create(shape);
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

    private TraceSpan? SpanOf(FlameHit hit)
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

        var instructions = row.Span(TraceTimelineAxis.Instructions, hit.Index);

        var positions = row.Span(TraceTimelineAxis.Position, hit.Index);

        var node = timeline.NodeOf(instructions.Node);

        var lines = new List<string>
        {
            LabelOf(instructions.Node),
            node?.Category is { Length: > 0 } category ? category : "Unknown",
            instructions.Call >= 0
                ? $"Thread {thread.ThreadId}  ·  Call {instructions.Call + 1:N0}  ·  Depth {hit.Depth}"
                : $"Thread {thread.ThreadId}  ·  Depth {hit.Depth}",
            $"Instructions {instructions.End - instructions.Start:N0}",
            $"Position {Math.Floor(positions.Start - OriginOf(TraceTimelineAxis.Position)):N0} "
            + $"to {Math.Floor(positions.End - OriginOf(TraceTimelineAxis.Position)):N0}"
        };

        var (allocatedBytes, allocations) = timeline.Memory.AllocatedDuring(thread.ThreadId, positions.Start, positions.End);

        if (allocations > 0)
        {
            lines.Add($"Allocated {SizeFormat.Format((long)allocatedBytes)} In {allocations:N0} "
                      + (allocations == 1 ? "Allocation" : "Allocations"));
        }

        if (timeline.Memory.FreedDuring(thread.ThreadId, positions.Start, positions.End) is > 0 and var freed)
        {
            lines.Add($"Freed {SizeFormat.Format((long)freed)}");
        }

        if (allocations > 0)
        {
            var retained = timeline.Memory.RetainedBy(thread.ThreadId, positions.Start, positions.End);

            lines.Add($"Retained At Return {SizeFormat.Format((long)retained)}");
        }

        if (instructions.Flags.HasFlag(TraceSpanFlags.StartUnknown))
        {
            lines.Add("Started Before The Recorded Call");
        }

        if (!instructions.Flags.HasFlag(TraceSpanFlags.Returned))
        {
            lines.Add("Did Not Return");
        }

        OpenToolTip(string.Join(Environment.NewLine, lines), position);
    }

    private void OpenToolTip(string text, Point position)
    {
        _toolTipText.Text = text;

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
