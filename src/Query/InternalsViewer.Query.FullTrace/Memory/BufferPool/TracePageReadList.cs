using System.Runtime.CompilerServices;

namespace InternalsViewer.Query.FullTrace.Memory.BufferPool;

public sealed partial class TracePageReadList
{
    internal TracePageReadList(TracePageRead[] reads)
    {
        Reads = reads;
    }

    public static TracePageReadList Empty { get; } = new([]);

    public int Count => Reads.Length;

    public long Bytes => Reads.Length * (long)Unsafe.SizeOf<TracePageRead>();

    private TracePageRead[] Reads { get; }

    public TracePageRead this[int index] => Reads[index];
}
