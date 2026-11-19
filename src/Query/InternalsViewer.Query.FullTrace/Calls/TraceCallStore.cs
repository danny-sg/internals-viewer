using InternalsViewer.Query.FullTrace.Calls.Tree;
using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Locators.Enums;

namespace InternalsViewer.Query.FullTrace.Calls;

public sealed partial class TraceCallStore
{
    internal const int BlockCallCount = 1024;

    private const long CachedColumnBytes = 16L * 1024 * 1024;

    private const long CachedColumns = CachedColumnBytes / sizeof(ulong);

    private TraceCallStore(Dictionary<(ulong FunctionAddress, ulong InstanceAddress), Chunk[]> functions,
                           TraceLocatorSet locators,
                           TraceNodeMap nodes)
    {
        Chunks = functions;
        Locators = locators;
        Nodes = nodes;
        Lists = functions.ToDictionary(f => f.Key,
                                       f => new TraceCallList(f.Key.FunctionAddress,
                                                              f.Key.InstanceAddress,
                                                              f.Value,
                                                              Columns,
                                                              locators.GetTable(f.Key.FunctionAddress, f.Key.InstanceAddress)));

        CallCount = functions.Values.Sum(f => f.Sum(c => (long)c.CallCount));
        CompressedSize = functions.Values.Sum(f => f.Sum(c => (long)c.Columns.Length));
    }

    public int FunctionCount => Chunks.Count;

    public long CallCount { get; }

    public long CompressedSize { get; }

    internal IReadOnlyDictionary<(ulong FunctionAddress, ulong InstanceAddress), Chunk[]> Chunks { get; }

    internal TraceLocatorSet Locators { get; }

    private Dictionary<(ulong FunctionAddress, ulong InstanceAddress), TraceCallList> Lists { get; }

    private TraceNodeMap Nodes { get; }

    private ChunkCache Columns { get; } = new(CachedColumns);

    public TraceCallList? GetCallList(ulong functionAddress, ulong instanceAddress)
        => Lists.GetValueOrDefault((functionAddress, instanceAddress));

    public CallTreeNode? GetNode(TraceArgumentCall call) => GetNode(call.ReplayNodeIndex);

    public CallTreeNode? GetNode(int replayNodeIndex) => Nodes.GetNode(replayNodeIndex);

    public CallTreeNode? GetFirstNode(TraceCallList calls) => GetNode(calls.GetReplayNodeIndex(0));

    public IEnumerable<TraceCallList> GetCallLists()
    {
        foreach (var (functionAddress, instanceAddress) in Chunks.Keys)
        {
            if (GetCallList(functionAddress, instanceAddress) is { Count: > 0 } list)
            {
                yield return list;
            }
        }
    }

    public IReadOnlyList<int> GetIncludedCallIndexes(TraceCallList calls, Func<CallTreeNode?, bool> include, CancellationToken cancellationToken)
    {
        var included = new List<int>(calls.Count);

        for (var index = 0; index < calls.Count; index++)
        {
            if ((index & 0xFFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (include(GetNode(calls.GetReplayNodeIndex(index))))
            {
                included.Add(index);
            }
        }

        return included;
    }

    internal void ClearColumnCache() => Columns.Clear();

    internal void AddNodeLocators()
    {
        foreach (var ((functionAddress, instanceAddress), table) in Locators.Tables)
        {
            if (GetCallList(functionAddress, instanceAddress) is not { } calls)
            {
                continue;
            }

            foreach (var (callIndex, locator) in table.GetEntries())
            {
                if (locator.ValueType is not (LocatorValueType.Page or LocatorValueType.Row or LocatorValueType.ColumnStoreObject))
                {
                    continue;
                }

                if (callIndex < calls.Count && GetNode(calls.GetReplayNodeIndex(callIndex)) is { } node)
                {
                    (node.Locators ??= new LocatorSet()).Add(locator);
                }
            }
        }
    }

    internal static TraceCallColumnSet Decompress(Chunk chunk, ref ulong[] buffer)
    {
        var length = chunk.Length;

        if (buffer.Length < length)
        {
            buffer = new ulong[length];
        }

        var bytes = MemoryMarshal.AsBytes(buffer.AsSpan(0, length));

        var read = ZLibCompression.Open(chunk.Columns).Read(bytes);

        bytes[read..].Clear();

        TraceCallColumnSet.Decode(buffer.AsSpan(0, length), chunk.CallCount);

        return new TraceCallColumnSet(buffer, chunk.CallCount, chunk.StackSlotCount);
    }

    internal sealed record Chunk(int CallCount, int StackSlotCount, byte[] Columns)
    {
        public int Length => CallCount * (TraceCallColumnSet.ValueCount + StackSlotCount);
    }

    internal sealed class ColumnBuffer
    {
        private ulong[] Values { get; set; } = [];

        public TraceCallColumnSet DecompressBuffer(Chunk chunk)
        {
            var values = Values;

            var columns = Decompress(chunk, ref values);

            Values = values;

            return columns;
        }
    }

    internal sealed class ChunkCache(long capacity)
    {
        private LinkedList<(Chunk Chunk, TraceCallColumnSet Columns)> Order { get; } = [];

        private Dictionary<Chunk, LinkedListNode<(Chunk Chunk, TraceCallColumnSet Columns)>> Entries { get; }
            = new(ReferenceEqualityComparer.Instance);

        private List<ulong[]> Pool { get; } = [];

        private Stack<LinkedListNode<(Chunk Chunk, TraceCallColumnSet Columns)>> FreeNodes { get; } = [];

        private Lock Sync { get; } = new();

        private long Size { get; set; }

        private long Pooled { get; set; }

        public T Read<T>(Chunk chunk, int callIndex, Func<TraceCallColumnSet, int, T> read)
            => Read(chunk, callIndex, read, static (columns, index, state) => state(columns, index));

        public T Read<TState, T>(Chunk chunk, int callIndex, TState state, Func<TraceCallColumnSet, int, TState, T> read)
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

            var buffer = Rent(chunk.Length);

            var columns = Decompress(chunk, ref buffer);

            lock (Sync)
            {
                if (Entries.TryGetValue(chunk, out var added))
                {
                    Return(buffer);

                    return read(added.Value.Columns, callIndex, state);
                }

                var node = FreeNodes.TryPop(out var free) ? free : new LinkedListNode<(Chunk Chunk, TraceCallColumnSet Columns)>(default);

                node.Value = (chunk, columns);

                Order.AddFirst(node);

                Entries[chunk] = node;

                Size += buffer.Length;

                while (Size > capacity && Order.Last is { } oldest && oldest != Order.First)
                {
                    Order.RemoveLast();

                    Entries.Remove(oldest.Value.Chunk);

                    Size -= oldest.Value.Columns.Values.Length;

                    Return(oldest.Value.Columns.Values);

                    oldest.Value = default;

                    FreeNodes.Push(oldest);
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

                FreeNodes.Clear();

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
