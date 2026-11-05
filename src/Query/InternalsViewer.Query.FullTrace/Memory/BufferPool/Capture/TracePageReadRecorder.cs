using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Memory.BufferPool.Capture;

internal sealed class TracePageReadRecorder(TraceBufferPool pool)
{
    public long Received { get; private set; }

    public long Recorded { get; private set; }

    public TracePosition? FirstPosition => pool.FirstPosition;

    private HashSet<ulong> Frames { get; } = [.. pool.GetFrames()];

    private Dictionary<uint, Run> Open { get; } = [];

    private ChunkedList<TracePageRead> Reads { get; } = [];

    public IEnumerable<WatchRange> GetRanges() => Frames.Select(f => new WatchRange(f, PageData.Size));

    public bool IsFrame(ulong address) => Frames.Contains(GetFrame(address));

    public void Add(WatchHit hit)
    {
        Received++;

        var address = hit.Address;

        var frame = GetFrame(address);

        if (address - frame < (ulong)PageHeader.Size)
        {
            return;
        }

        var end = Math.Min(address + hit.Size, frame + PageData.Size);

        var position = hit.Position;

        var isOpen = Open.TryGetValue(hit.Thread, out var run);

        if (isOpen && run.IsContinuedBy(frame, address, end, position))
        {
            Open[hit.Thread] = run.Extend(address, end, position);

            return;
        }

        if (isOpen)
        {
            Reads.Add(run.ToRead());
        }

        Open[hit.Thread] = new Run(hit.Thread, frame, position, position, address, end);

        Recorded++;
    }

    public TracePageReadList Build()
    {
        foreach (var run in Open.Values)
        {
            Reads.Add(run.ToRead());
        }

        Open.Clear();

        var reads = Reads.ToArray();

        reads.AsSpan().Sort(static (left, right) => left.Position != right.Position ? left.Position.CompareTo(right.Position)
                                                    : left.Thread != right.Thread ? left.Thread.CompareTo(right.Thread)
                                                    : left.Address.CompareTo(right.Address));

        return new TracePageReadList(reads);
    }

    private static ulong GetFrame(ulong address) => address - address % PageData.Size;

    private readonly record struct Run(uint Thread, ulong Frame, TracePosition First, TracePosition Last, ulong Start, ulong End)
    {
        public bool IsContinuedBy(ulong frame, ulong start, ulong end, TracePosition position)
            => frame == Frame && position >= Last && start <= End && end >= Start;

        public Run Extend(ulong start, ulong end, TracePosition position)
            => this with { Last = position, Start = Math.Min(start, Start), End = Math.Max(end, End) };

        public TracePageRead ToRead() => new(First, Thread, Start, (ushort)(End - Start));
    }
}
