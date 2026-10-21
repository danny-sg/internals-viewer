using InternalsViewer.Query.CallStack;
using System.IO.Compression;
using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.CallStack.Locators;

namespace InternalsViewer.Query.FullTrace.Calls;

public sealed partial class TraceCallStore
{
    private const long CachedColumnBytes = 64L * 1024 * 1024;

    private const long CachedColumns = CachedColumnBytes / sizeof(ulong);

    private TraceCallStore(Dictionary<(ulong Address, ulong Instance), Chunk[]> functions,
                           Dictionary<(ulong Address, ulong Instance), LocatorTable> locators)
    {
        Functions = functions;
        Locators = locators;

        Calls = functions.Values.Sum(f => f.Sum(c => (long)c.Calls));
        Size = functions.Values.Sum(f => f.Sum(c => (long)c.Columns.Length));
        LocatorCount = locators.Values.Sum(l => (long)l.Count);
    }

    public int FunctionCount => Functions.Count;

    public long Calls { get; }

    public long Size { get; }

    public long LocatorCount { get; }

    internal IReadOnlyDictionary<(ulong Address, ulong Instance), Chunk[]> Functions { get; }

    internal IReadOnlyDictionary<(ulong Address, ulong Instance), LocatorTable> Locators { get; }

    private CallStackNode?[] Nodes { get; set; } = [];

    private ChunkCache Columns { get; } = new(CachedColumns);

    public TraceCallList? GetCallList(ulong address, ulong instance)
        => Functions.TryGetValue((address, instance), out var chunks)
            ? new TraceCallList(address, instance, chunks, Columns, Locators.GetValueOrDefault((address, instance)))
            : null;

    public CallStackNode? GetNode(TraceArgumentCall call) => GetNode(call.CallNodeIndex);

    public CallStackNode? GetNode(int callNodeIndex) => callNodeIndex >= 0 && callNodeIndex < Nodes.Length ? Nodes[callNodeIndex] : null;

    public IReadOnlyList<int> GetIncludedCalls(TraceCallList calls, Func<CallStackNode?, bool> include, CancellationToken cancellationToken)
    {
        var included = new List<int>(calls.Count);

        for (var index = 0; index < calls.Count; index++)
        {
            if ((index & 0xFFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (include(GetNode(calls.GetCallNodeIndex(index))))
            {
                included.Add(index);
            }
        }

        return included;
    }

    internal void MapNodes(CallStackNode?[] nodes) => Nodes = nodes;

    internal IEnumerable<(ulong Address, ulong Instance)> FunctionKeys => Functions.Keys;

    internal void AnnotateNodes()
    {
        foreach (var ((address, instance), table) in Locators)
        {
            if (GetCallList(address, instance) is not { } calls)
            {
                continue;
            }

            foreach (var (callIndex, locator) in table.Entries())
            {
                if (locator.ValueType is not (LocatorValueType.Page or LocatorValueType.Row or LocatorValueType.ColumnStoreObject))
                {
                    continue;
                }

                if (callIndex < calls.Count && GetNode(calls.GetCallNodeIndex(callIndex)) is { } node)
                {
                    (node.Locators ??= new LocatorSet()).Add(locator);
                }
            }
        }
    }

    internal static TraceCallColumns Decompress(Chunk chunk)
    {
        var columns = new ulong[chunk.Calls * TraceCallColumns.ValueCount];

        using var stream = new ZLibStream(new MemoryStream(chunk.Columns), CompressionMode.Decompress);

        var bytes = MemoryMarshal.AsBytes(columns.AsSpan());

        var read = stream.ReadAtLeast(bytes, bytes.Length, throwOnEndOfStream: false);

        bytes[read..].Clear();

        return new TraceCallColumns(columns, chunk.Calls);
    }

    internal sealed record Chunk(int Calls, byte[] Columns);

    internal sealed class ChunkCache(long capacity)
    {
        private LinkedList<(Chunk Chunk, TraceCallColumns Columns)> Order { get; } = [];

        private Dictionary<Chunk, LinkedListNode<(Chunk Chunk, TraceCallColumns Columns)>> Entries { get; }
            = new(ReferenceEqualityComparer.Instance);

        private Lock Sync { get; } = new();

        private long Size { get; set; }

        public TraceCallColumns Get(Chunk chunk)
        {
            lock (Sync)
            {
                if (Entries.TryGetValue(chunk, out var cached))
                {
                    Order.Remove(cached);

                    Order.AddFirst(cached);

                    return cached.Value.Columns;
                }
            }

            var columns = Decompress(chunk);

            lock (Sync)
            {
                if (Entries.TryGetValue(chunk, out var added))
                {
                    return added.Value.Columns;
                }

                Entries[chunk] = Order.AddFirst((chunk, columns));

                Size += columns.Values.Length;

                while (Size > capacity && Order.Last is { } oldest && oldest != Order.First)
                {
                    Order.RemoveLast();

                    Entries.Remove(oldest.Value.Chunk);

                    Size -= oldest.Value.Columns.Values.Length;
                }
            }

            return columns;
        }
    }
}
