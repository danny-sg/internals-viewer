using System;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using Microsoft.UI.Xaml.Controls;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Query.Trace.Timeline;

public sealed partial class TraceTimelineControl : ITraceTimelineHost
{
    TraceTimelineAxis ITraceTimelineHost.Axis => _axis;

    double ITraceTimelineHost.ViewStart => _viewStart;

    double ITraceTimelineHost.ViewEnd => _viewEnd;

    int ITraceTimelineHost.Width => (int)_overlay.ActualWidth;

    double ITraceTimelineHost.Scale => PixelsPerUnit((int)_overlay.ActualWidth);

    double? ITraceTimelineHost.Playhead => _playhead;

    bool ITraceTimelineHost.ShowCallStack => ShowCallStack;

    bool[]? ITraceTimelineHost.MemoryScope => GetMemoryScope();

    TraceTimelinePaints ITraceTimelineHost.Paints => _paints;

    SKPathBuilder ITraceTimelineHost.PathBuilder => _pathBuilder;

    double ITraceTimelineHost.GetOrigin(TraceTimelineAxis axis) => OriginOf(axis);

    SKColor ITraceTimelineHost.GetColour(int callNodeIndex) => GetPaneColour(callNodeIndex);

    string ITraceTimelineHost.GetLabel(int callNodeIndex) => LabelOf(callNodeIndex);

    void ITraceTimelineHost.Invalidate() => _canvas.Invalidate();

    void ITraceTimelineHost.Post(Action action) => DispatcherQueue.TryEnqueue(() => action());

    void ITraceTimelineHost.ShowMemoryProgress(bool isVisible)
    {
        _memoryProgress.IsActive = isVisible;

        _memoryProgress.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;

        if (isVisible)
        {
            Canvas.SetLeft(_memoryProgress, (_overlay.ActualWidth - MemoryProgressSize) / 2);

            Canvas.SetTop(_memoryProgress, MemoryTop + (MemoryHeight - MemoryProgressSize) / 2);
        }
    }

    void ITraceTimelineHost.OnMemoryLayoutChanged() => UpdateScrollBars();

    void ITraceTimelineHost.MovePlayhead(double position)
    {
        RememberLocation();

        StopPlay();

        PlacePlayhead(position);
    }

    void ITraceTimelineHost.ClickCall(CallStackNode node, int callIndex)
    {
        if (_visible is not { } timeline || FindCall(timeline, node, callIndex) is not { } hit)
        {
            return;
        }

        Click(hit);

        if (_selected is { } selected)
        {
            BringIntoView(selected);
        }
    }

    void ITraceTimelineHost.OpenCallTree(CallStackNode node, int callIndex) => CallTreeOpenRequested?.Invoke(node, callIndex);

    void ITraceTimelineHost.SelectMemoryWrite(int block, TracePosition? position) => MemoryWriteSelected?.Invoke(block, position);

    void ITraceTimelineHost.ShowMemory(int block, ulong address, TracePosition position)
        => MemoryBlockSelected?.Invoke(block, address, position);

    void ITraceTimelineHost.OnSelectedOperatorChanged(ExecutionOperatorEvent? selected)
    {
        FindSelectedRegions();

        _canvas.Invalidate();

        if (selected?.PlanNodeIdentifier is { } identifier)
        {
            PlanNodeSelected?.Invoke(identifier);
        }
    }
}
