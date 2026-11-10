using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Memory.BufferPool.Capture;

internal sealed class TracePageReadRecorder(TraceBufferPool pool)
{
    public long ReceivedCount { get; private set; }

    public long CapturedCount { get; private set; }

    public TracePosition? FirstPosition => pool.FirstPosition;

    private HashSet<ulong> Frames { get; } = [.. pool.GetFrames()];

    private Dictionary<uint, Run> OpenBodies { get; } = [];

    private Dictionary<uint, Run> OpenHeaders { get; } = [];

    private ChunkedList<TracePageRead> Reads { get; } = [];

    public IEnumerable<WatchRange> GetRanges() => Frames.Select(f => new WatchRange(f, PageData.Size));

    public bool IsFrame(ulong address) => Frames.Contains(GetFrame(address));

    public void Add(WatchHit hit)
    {
        ReceivedCount++;

        var frame = GetFrame(hit.Address);

        var body = frame + (ulong)PageHeader.Size;

        var end = Math.Min(hit.Address + hit.Size, frame + (ulong)PageData.Size);

        AddRun(OpenHeaders, hit, frame, hit.Address, Math.Min(end, body));

        if (AddRun(OpenBodies, hit, frame, Math.Max(hit.Address, body), end) && OpenHeaders.Remove(hit.Thread, out var header))
        {
            Reads.Add(header.ToRead());
        }
    }

    public TracePageReadList Build()
    {
        foreach (var run in OpenBodies.Values.Concat(OpenHeaders.Values))
        {
            Reads.Add(run.ToRead());
        }

        OpenBodies.Clear();

        OpenHeaders.Clear();

        var reads = Reads.ToArray();

        reads.AsSpan().Sort(static (left, right) => left.Position != right.Position ? left.Position.CompareTo(right.Position)
                                                    : left.Thread != right.Thread ? left.Thread.CompareTo(right.Thread)
                                                    : left.Address.CompareTo(right.Address));

        return new TracePageReadList(reads);
    }

    private bool AddRun(Dictionary<uint, Run> runs, WatchHit hit, ulong frame, ulong address, ulong end)
    {
        if (address >= end)
        {
            return false;
        }

        var position = hit.Position;

        var isOpen = runs.TryGetValue(hit.Thread, out var run);

        if (isOpen && run.IsContinuedBy(frame, address, end, position))
        {
            runs[hit.Thread] = run.Extend(address, end, position);

            return true;
        }

        if (isOpen)
        {
            Reads.Add(run.ToRead());
        }

        runs[hit.Thread] = new Run(hit.Thread, frame, position, position, address, end);

        CapturedCount++;

        return true;
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
