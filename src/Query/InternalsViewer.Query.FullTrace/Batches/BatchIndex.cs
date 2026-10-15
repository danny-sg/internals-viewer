using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Batches;

public sealed partial class BatchIndex
{
    private const int MaximumLineage = 64;

    private const string ProducerMethod = "BpGetNextBatch";

    internal BatchIndex(IEnumerable<BatchCapture> captures)
    {
        Captures = [.. captures];

        Batches = Captures.GroupBy(c => (c.Function, c.Instance)).ToDictionary(g => g.Key, g => g.OrderBy(c => c.Call).ToArray());

        Handoffs = Captures.Where(c => c.Consumer != 0)
                           .GroupBy(c => (c.Address, c.Consumer))
                           .ToDictionary(g => g.Key, g => g.OrderBy(c => c.Id).ToArray());

        foreach (var capture in Batches.Values.SelectMany(b => b).Where(c => c.Consumer != 0))
        {
            Producers.TryAdd(capture.Consumer, capture.Instance);
        }
    }

    public static BatchIndex Empty { get; } = new([]);

    public int Count => Captures.Length;

    private BatchCapture[] Captures { get; }

    private Dictionary<(ulong Function, ulong Instance), BatchCapture[]> Batches { get; }

    private Dictionary<(ulong Batch, ulong Consumer), BatchCapture[]> Handoffs { get; }

    private Dictionary<ulong, ulong> Producers { get; } = [];

    public bool HasBatches(ulong address, ulong instance) => Batches.ContainsKey((address, instance));

    public bool HasBatch(ulong address, ulong instance, int call) => GetBatch(address, instance, call) is not null;

    public IReadOnlyList<BatchCapture> GetBatches(ulong address, ulong instance) => Batches.GetValueOrDefault((address, instance)) ?? [];

    public BatchCapture? GetBatch(ulong address, ulong instance, int call)
    {
        var batches = GetBatches(address, instance);

        var index = SortedSearch.FirstAtOrAfter(batches, call, b => b.Call);

        return index < batches.Count && batches[index].Call == call ? batches[index] : null;
    }

    public BatchCapture? GetInput(BatchCapture capture)
    {
        if (!Handoffs.TryGetValue((capture.Address, capture.Iterator), out var handoffs))
        {
            return null;
        }

        var index = SortedSearch.FirstAtOrAfter(handoffs, capture.Id, h => h.Id) - 1;

        return index < 0 ? null : handoffs[index];
    }

    public ulong? GetProducer(ulong consumer) => Producers.TryGetValue(consumer, out var producer) ? producer : null;

    public BatchLineage? GetLineage(ulong address, ulong instance, int call)
    {
        if (GetBatch(address, instance, call) is not { } capture)
        {
            return null;
        }

        var inputs = new List<BatchCapture>();

        var current = capture;

        while (inputs.Count < MaximumLineage && GetInput(current) is { } input)
        {
            current = input;

            inputs.Add(input);
        }

        return new BatchLineage(capture, inputs);
    }

    internal void AnnotateNodes(TraceCallStore store)
    {
        var produced = Captures.GroupBy(c => c.Instance).ToDictionary(g => g.Key, g => g.OrderBy(c => c.Id).ToArray());

        foreach (var (address, instance) in store.FunctionKeys)
        {
            if (!produced.TryGetValue(instance, out var handoffs)
                || store.GetCallList(address, instance) is not { Count: > 0 } calls
                || store.GetNode(calls.GetNodeIndex(0))?.Frame?.Resolved?.MethodName != ProducerMethod)
            {
                continue;
            }

            var productions = GetProductions(address, calls, handoffs);

            if (productions.Length > 0)
            {
                Batches[(address, instance)] = productions;
            }
        }

        foreach (var ((address, instance), batches) in Batches)
        {
            if (store.GetCallList(address, instance) is not { } calls)
            {
                continue;
            }

            foreach (var batch in batches)
            {
                if (batch.Call < calls.Count && store.GetNode(calls.GetNodeIndex(batch.Call)) is { } node)
                {
                    node.HasBatches = true;
                }
            }
        }
    }

    private static BatchCapture[] GetProductions(ulong address, TraceCallList calls, BatchCapture[] handoffs)
    {
        var productions = new List<BatchCapture>();

        var next = 0;

        for (var call = 0; call < calls.Count; call++)
        {
            var end = calls.GetEndId(call);

            if (end == 0)
            {
                continue;
            }

            var limit = call + 1 < calls.Count ? calls.GetId(call + 1) : ulong.MaxValue;

            while (next < handoffs.Length && handoffs[next].Id <= end)
            {
                next++;
            }

            if (next < handoffs.Length && handoffs[next].Id < limit)
            {
                productions.Add(handoffs[next] with { Function = address, Call = call });
            }
        }

        return [.. productions];
    }
}
