namespace InternalsViewer.Query.FullTrace.Memory.History;

internal sealed class MemoryHistoryReadCounts
{
    public long Received { get; set; }

    public long Recorded { get; set; }

    public long BufferPool { get; set; }

    public long Outside { get; set; }

    public long Unwritten { get; set; }
}
