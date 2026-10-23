using InternalsViewer.Query.CallStack;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.CallStack.Locators;

namespace InternalsViewer.Query.FullTrace.Calls;

public sealed partial class TraceCallStore
{
    private const long CachedColumnBytes = 16L * 1024 * 1024;

    private const long CachedColumns = CachedColumnBytes / sizeof(ulong);

    private TraceCallStore(Dictionary<(ulong FunctionAddress, ulong InstanceAddress), Chunk[]> functions,
                           Dictionary<(ulong FunctionAddress, ulong InstanceAddress), LocatorTable> locators,
                           Locator[] uniqueLocators)
    {
        Functions = functions;
        Locators = locators;
        UniqueLocators = uniqueLocators;
        Lists = functions.ToDictionary(f => f.Key,
                                       f => new TraceCallList(f.Key.FunctionAddress,
                                                              f.Key.InstanceAddress,
                                                              f.Value,
                                                              Columns,
                                                              locators.GetValueOrDefault(f.Key)));

        Calls = functions.Values.Sum(f => f.Sum(c => (long)c.Calls));
        Size = functions.Values.Sum(f => f.Sum(c => (long)c.Columns.Length));
        LocatorCount = locators.Values.Sum(l => (long)l.Count);
    }

    public int FunctionCount => Functions.Count;

    public long Calls { get; }

    public long Size { get; }

    public long LocatorCount { get; }

    public int UniqueLocatorCount => UniqueLocators.Length;

    internal long LocatorTableBytes => Locators.Values.Sum(t => t.Bytes);

    internal long UniqueLocatorBytes
        => UniqueLocators.Length * (long)Unsafe.SizeOf<Locator>()
           + UniqueLocators.Sum(l => l.Text is { } text ? 22L + 2L * text.Length : 0);

    internal IReadOnlyDictionary<(ulong FunctionAddress, ulong InstanceAddress), Chunk[]> Functions { get; }

    internal IReadOnlyDictionary<(ulong FunctionAddress, ulong InstanceAddress), LocatorTable> Locators { get; }

    private Locator[] UniqueLocators { get; }

    private Dictionary<(ulong FunctionAddress, ulong InstanceAddress), TraceCallList> Lists { get; }

    private CallStackNode?[] Nodes { get; set; } = [];

    private ChunkCache Columns { get; } = new(CachedColumns);

    public TraceCallList? GetCallList(ulong functionAddress, ulong instanceAddress)
        => Lists.GetValueOrDefault((functionAddress, instanceAddress));

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

    internal void ReleaseColumns() => Columns.Clear();

    internal IEnumerable<(ulong FunctionAddress, ulong InstanceAddress)> FunctionKeys => Functions.Keys;

    internal void AnnotateNodes()
    {
        foreach (var ((functionAddress, instanceAddress), table) in Locators)
        {
            if (GetCallList(functionAddress, instanceAddress) is not { } calls)
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
        ulong[] buffer = [];

        return Decompress(chunk, ref buffer);
    }

    internal static TraceCallColumns Decompress(Chunk chunk, ref ulong[] buffer)
    {
        var length = chunk.Calls * TraceCallColumns.ValueCount;

        if (buffer.Length < length)
        {
            buffer = new ulong[length];
        }

        using var stream = new ZLibStream(new MemoryStream(chunk.Columns), CompressionMode.Decompress);

        var bytes = MemoryMarshal.AsBytes(buffer.AsSpan(0, length));

        var read = stream.ReadAtLeast(bytes, bytes.Length, throwOnEndOfStream: false);

        bytes[read..].Clear();

        TraceCallColumns.Decode(buffer.AsSpan(0, length), chunk.Calls);

        return new TraceCallColumns(buffer, chunk.Calls);
    }

    internal sealed record Chunk(int Calls, byte[] Columns);

    internal sealed class ColumnBuffer
    {
        private ulong[] Values { get; set; } = [];

        public TraceCallColumns Decompress(Chunk chunk)
        {
            var values = Values;

            var columns = TraceCallStore.Decompress(chunk, ref values);

            Values = values;

            return columns;
        }
    }

    internal sealed class ChunkCache(long capacity)
    {
        private LinkedList<(Chunk Chunk, TraceCallColumns Columns)> Order { get; } = [];

        private Dictionary<Chunk, LinkedListNode<(Chunk Chunk, TraceCallColumns Columns)>> Entries { get; }
            = new(ReferenceEqualityComparer.Instance);

        private List<ulong[]> Pool { get; } = [];

        private Lock Sync { get; } = new();

        private long Size { get; set; }

        private long Pooled { get; set; }

        public T Read<T>(Chunk chunk, int callIndex, Func<TraceCallColumns, int, T> read)
            => Read(chunk, callIndex, read, static (columns, index, state) => state(columns, index));

        public T Read<TState, T>(Chunk chunk, int callIndex, TState state, Func<TraceCallColumns, int, TState, T> read)
        {
            lock (Sync)
            {
                if (Entries.TryGetValue(chunk, out var cached))
                {
                    Order.Remove(cached);

                    Order.AddFirst(cached);

                    return read(cached.Value.Columns, callIndex, state);
                }
            }

            var buffer = Rent(chunk.Calls * TraceCallColumns.ValueCount);

            var columns = Decompress(chunk, ref buffer);

            lock (Sync)
            {
                if (Entries.TryGetValue(chunk, out var added))
                {
                    Return(buffer);

                    return read(added.Value.Columns, callIndex, state);
                }

                Entries[chunk] = Order.AddFirst((chunk, columns));

                Size += buffer.Length;

                while (Size > capacity && Order.Last is { } oldest && oldest != Order.First)
                {
                    Order.RemoveLast();

                    Entries.Remove(oldest.Value.Chunk);

                    Size -= oldest.Value.Columns.Values.Length;

                    Return(oldest.Value.Columns.Values);
                }

                return read(columns, callIndex, state);
            }
        }

        public void Clear()
        {
            lock (Sync)
            {
                Order.Clear();

                Entries.Clear();

                Pool.Clear();

                Size = 0;

                Pooled = 0;
            }
        }

        private ulong[] Rent(int length)
        {
            lock (Sync)
            {
                var best = -1;

                for (var index = 0; index < Pool.Count; index++)
                {
                    if (Pool[index].Length >= length && (best < 0 || Pool[index].Length < Pool[best].Length))
                    {
                        best = index;
                    }
                }

                if (best >= 0)
                {
                    var pooled = Pool[best];

                    Pool.RemoveAt(best);

                    Pooled -= pooled.Length;

                    return pooled;
                }
            }

            return new ulong[length];
        }

        private void Return(ulong[] values)
        {
            Pool.Add(values);

            Pooled += values.Length;

            while (Pooled > capacity / 2 && Pool.Count > 0)
            {
                var smallest = 0;

                for (var index = 1; index < Pool.Count; index++)
                {
                    if (Pool[index].Length < Pool[smallest].Length)
                    {
                        smallest = index;
                    }
                }

                Pooled -= Pool[smallest].Length;

                Pool.RemoveAt(smallest);
            }
        }
    }
}
