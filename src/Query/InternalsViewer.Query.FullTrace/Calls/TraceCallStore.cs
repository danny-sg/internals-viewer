using InternalsViewer.Query.CallStack;
using System.IO.Compression;
using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.CallStack.Locators;

namespace InternalsViewer.Query.FullTrace.Calls;

public sealed partial class TraceCallStore
{
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

    public TraceCallList? GetCallList(ulong address, ulong instance)
        => Functions.TryGetValue((address, instance), out var chunks)
            ? new TraceCallList(address, instance, chunks, Locators.GetValueOrDefault((address, instance)))
            : null;

    public CallStackNode? GetNode(TraceArgumentCall call) => GetNode(call.Node);

    public CallStackNode? GetNode(int node) => node >= 0 && node < Nodes.Length ? Nodes[node] : null;

    public IReadOnlyList<int> GetIncludedCalls(TraceCallList calls, Func<CallStackNode?, bool> include, CancellationToken cancellationToken)
    {
        var included = new List<int>(calls.Count);

        for (var index = 0; index < calls.Count; index++)
        {
            if ((index & 0xFFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (include(GetNode(calls.GetNodeIndex(index))))
            {
                included.Add(index);
            }
        }

        return included;
    }

    internal void MapNodes(CallStackNode?[] nodes) => Nodes = nodes;

    internal IEnumerable<(ulong Address, ulong Instance)> FunctionKeys => Functions.Keys;

    internal IEnumerable<(ulong Sequence, int Node, ulong Self)> GetSelves(ulong address, ulong instance)
    {
        if (!Functions.TryGetValue((address, instance), out var chunks))
        {
            yield break;
        }

        foreach (var chunk in chunks)
        {
            var columns = Decompress(chunk);

            for (var call = 0; call < chunk.Calls; call++)
            {
                yield return (columns[call],
                              (int)columns[TraceArgumentCall.NodeIndex * chunk.Calls + call],
                              columns[TraceArgumentCall.IntegerOffset * chunk.Calls + call]);
            }
        }
    }

    internal void AnnotateNodes()
    {
        foreach (var ((address, instance), table) in Locators)
        {
            if (GetCallList(address, instance) is not { } calls)
            {
                continue;
            }

            foreach (var (call, locator) in table.Entries())
            {
                if (locator.ValueType is not (LocatorValueType.Page or LocatorValueType.Row or LocatorValueType.ColumnStoreObject))
                {
                    continue;
                }

                if (call < calls.Count && GetNode(calls.GetNodeIndex(call)) is { } node)
                {
                    (node.Locators ??= new LocatorSet()).Add(locator);
                }
            }
        }
    }

    internal static ulong[] Decompress(Chunk chunk)
    {
        var columns = new ulong[chunk.Calls * TraceArgumentCall.ValueCount];

        using var stream = new ZLibStream(new MemoryStream(chunk.Columns), CompressionMode.Decompress);

        var bytes = MemoryMarshal.AsBytes(columns.AsSpan());

        var read = stream.ReadAtLeast(bytes, bytes.Length, throwOnEndOfStream: false);

        bytes[read..].Clear();

        return columns;
    }

    internal sealed record Chunk(int Calls, byte[] Columns);
}
