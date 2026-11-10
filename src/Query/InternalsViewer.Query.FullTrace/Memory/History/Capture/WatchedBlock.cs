namespace InternalsViewer.Query.FullTrace.Memory.History.Capture;

internal sealed class WatchedBlock(ulong iterator,
                                   ulong address,
                                   ulong size,
                                   bool isIterator,
                                   bool isInline,
                                   bool isClerkPage,
                                   uint thread,
                                   TracePosition fromPosition,
                                   TracePosition toPosition,
                                   bool isBuffer = false,
                                   bool isStack = false,
                                   int callNodeIndex = -1,
                                   bool isArray = false)
{
    public ulong Iterator { get; } = iterator;

    public ulong Address { get; } = address;

    public ulong Size { get; } = size;

    public bool IsIterator { get; } = isIterator;

    public bool IsInline { get; } = isInline;

    public bool IsClerkPage { get; } = isClerkPage;

    public bool IsBuffer { get; } = isBuffer;

    public bool IsStack { get; } = isStack;

    public bool IsArray { get; } = isArray;

    public int CallNodeIndex { get; } = callNodeIndex;

    public List<(TracePosition From, TracePosition To)> Instances { get; } = [];

    public uint Thread { get; } = thread;

    public TracePosition FromPosition { get; } = fromPosition;

    public TracePosition ToPosition { get; set; } = toPosition;

    public bool IsBufferPool { get; set; }

    public byte[]? Image { get; set; }

    public bool[]? Known { get; set; }

    public bool Contains(ulong address, TracePosition position)
        => address >= Address && address - Address < Size && FromPosition <= position && position < ToPosition;

    public void AddInstance(TracePosition from, TracePosition to)
    {
        Instances.Add((from, to));

        if (ToPosition < to)
        {
            ToPosition = to;
        }
    }
}
