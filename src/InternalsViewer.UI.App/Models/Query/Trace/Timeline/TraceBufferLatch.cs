using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Memory.BufferPool.Enums;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

public readonly struct TraceBufferLatch(TracePoint point,
                                        int block,
                                        ulong address,
                                        string label,
                                        BufferLatchState state,
                                        bool hasInstructions)
{
    public TracePoint Point { get; } = point;

    public int Block { get; } = block;

    public ulong Address { get; } = address;

    public string Label { get; } = label;

    public BufferLatchState State { get; } = state;

    public bool HasInstructions { get; } = hasInstructions;
}
