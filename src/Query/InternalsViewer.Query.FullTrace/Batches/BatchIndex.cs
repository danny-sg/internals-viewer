using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Native;

namespace InternalsViewer.Query.FullTrace.Batches;

public sealed partial class BatchIndex
{
    private BatchIndex(Dictionary<(ulong Address, ulong Instance), BatchTable> tables)
    {
        Tables = tables;

        Count = tables.Values.Sum(t => t.Count);
    }

    public static BatchIndex Empty { get; } = new([]);

    public int Count { get; }

    private const int MaximumLineage = 64;

    private const string ProducerMethod = "BpGetNextBatch";

    private Dictionary<(ulong Address, ulong Instance), BatchTable> Tables { get; }

    private Dictionary<(ulong Address, ulong Instance), Production[]> Productions { get; set; } = [];

    private Dictionary<(ulong Batch, ulong Consumer), Handoff[]> Handoffs => field ??= BuildHandoffs();

    private Dictionary<ulong, ulong> Producers => field ??= BuildProducers();

    public bool HasBatches(ulong address, ulong instance)
        => Tables.ContainsKey((address, instance)) || Productions.ContainsKey((address, instance));

    public bool HasBatch(ulong address, ulong instance, int call)
        => Tables.TryGetValue((address, instance), out var table)
            ? table.HasBatch(call)
            : FindProduction(address, instance, call) is not null;

    public BatchCapture? GetBatch(ulong address, ulong instance, int call)
    {
        if (Tables.TryGetValue((address, instance), out var table))
        {
            return table.GetBatch(call);
        }

        return FindProduction(address, instance, call) is { } production ? CreateBatch(address, production) : null;
    }

    public IReadOnlyList<int> GetCalls(ulong address, ulong instance)
    {
        if (Tables.TryGetValue((address, instance), out var table))
        {
            return table.GetCalls();
        }

        return Productions.TryGetValue((address, instance), out var productions) ? [.. productions.Select(p => p.Call)] : [];
    }

    public IReadOnlyList<BatchCapture> GetBatches(ulong address, ulong instance)
    {
        if (Tables.TryGetValue((address, instance), out var table))
        {
            return [.. table.GetBatches()];
        }

        return Productions.TryGetValue((address, instance), out var productions)
            ? [.. productions.Select(p => CreateBatch(address, p))]
            : [];
    }

    public IReadOnlyList<BatchSummary> GetSummaries(ulong address, ulong instance)
    {
        if (Tables.TryGetValue((address, instance), out var table))
        {
            return [.. Enumerable.Range(0, table.Count).Select(index => GetSummary(table, index))];
        }

        return Productions.TryGetValue((address, instance), out var productions)
            ? [.. productions.Select(p => GetSummary(p.Table, p.Index) with { Call = p.Call })]
            : [];
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

        while (inputs.Count < MaximumLineage
               && FindInput((current.Address, current.Consumer, current.Id), current.Iterator) is { } input)
        {
            current = input.Table.CreateBatch(input.Index);

            inputs.Add(current);
        }

        return new BatchLineage(capture, inputs);
    }

    private static BatchCapture CreateBatch(ulong address, Production production)
        => production.Table.CreateBatch(production.Index) with { Function = address, Call = production.Call };

    private BatchSummary GetSummary(BatchTable table, int index)
    {
        var summary = table.GetSummary(index);

        return FindInput(table.GetHandoff(index), summary.Iterator) is { } input
            ? summary with { InputSelectedCount = input.Table.GetSelectedCount(input.Index) }
            : summary;
    }

    private Production? FindProduction(ulong address, ulong instance, int call)
    {
        if (!Productions.TryGetValue((address, instance), out var productions))
        {
            return null;
        }

        var low = 0;

        var high = productions.Length - 1;

        while (low <= high)
        {
            var middle = low + (high - low) / 2;

            if (productions[middle].Call == call)
            {
                return productions[middle];
            }

            if (productions[middle].Call < call)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return null;
    }

    private Handoff? FindInput((ulong Batch, ulong Consumer, ulong Id) handoff, ulong iterator)
    {
        if (!Handoffs.TryGetValue((handoff.Batch, iterator), out var handoffs))
        {
            return null;
        }

        var low = 0;

        var high = handoffs.Length - 1;

        Handoff? found = null;

        while (low <= high)
        {
            var middle = low + (high - low) / 2;

            if (handoffs[middle].Id < handoff.Id)
            {
                found = handoffs[middle];

                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return found;
    }

    private Dictionary<(ulong Batch, ulong Consumer), Handoff[]> BuildHandoffs()
    {
        var handoffs = new Dictionary<(ulong Batch, ulong Consumer), List<Handoff>>();

        foreach (var table in Tables.Values)
        {
            for (var index = 0; index < table.Count; index++)
            {
                var (batch, consumer, id) = table.GetHandoff(index);

                if (consumer == 0)
                {
                    continue;
                }

                ref var list = ref CollectionsMarshal.GetValueRefOrAddDefault(handoffs, (batch, consumer), out _);

                (list ??= []).Add(new Handoff(id, table, index));
            }
        }

        return handoffs.ToDictionary(h => h.Key, h => h.Value.OrderBy(v => v.Id).ToArray());
    }

    private Dictionary<ulong, ulong> BuildProducers()
    {
        var producers = new Dictionary<ulong, ulong>();

        foreach (var table in Tables.Values)
        {
            for (var index = 0; index < table.Count; index++)
            {
                if (table.GetHandoff(index).Consumer is not 0 and var consumer)
                {
                    producers.TryAdd(consumer, table.Instance);
                }
            }
        }

        return producers;
    }

    internal void AnnotateNodes(TraceCallStore store)
    {
        Productions = BuildProductions(store);

        foreach (var table in Tables.Values)
        {
            Annotate(store, table.Function, table.Instance, table.GetCalls());
        }

        foreach (var ((address, instance), productions) in Productions)
        {
            Annotate(store, address, instance, [.. productions.Select(p => p.Call)]);
        }
    }

    private static void Annotate(TraceCallStore store, ulong address, ulong instance, IReadOnlyList<int> calls)
    {
        if (store.GetCallList(address, instance) is not { } callList)
        {
            return;
        }

        foreach (var call in calls)
        {
            if (call < callList.Count && store.GetNode(callList.GetNodeIndex(call)) is { } node)
            {
                node.HasBatches = true;
            }
        }
    }

    private Dictionary<(ulong Address, ulong Instance), Production[]> BuildProductions(TraceCallStore store)
    {
        var handoffs = new Dictionary<ulong, List<Handoff>>();

        foreach (var table in Tables.Values)
        {
            ref var list = ref CollectionsMarshal.GetValueRefOrAddDefault(handoffs, table.Instance, out _);

            list ??= [];

            for (var index = 0; index < table.Count; index++)
            {
                list.Add(new Handoff(table.GetHandoff(index).Id, table, index));
            }
        }

        foreach (var list in handoffs.Values)
        {
            list.Sort((left, right) => left.Id.CompareTo(right.Id));
        }

        var productions = new Dictionary<(ulong Address, ulong Instance), Production[]>();

        foreach (var (address, instance) in store.FunctionKeys)
        {
            if (!handoffs.TryGetValue(instance, out var list)
                || store.GetCallList(address, instance) is not { Count: > 0 } calls
                || store.GetNode(calls.GetNodeIndex(0))?.Frame?.Resolved?.MethodName != ProducerMethod)
            {
                continue;
            }

            var found = GetProductions(calls, list);

            if (found.Length > 0)
            {
                productions[(address, instance)] = found;
            }
        }

        return productions;
    }

    private static Production[] GetProductions(TraceCallList calls, List<Handoff> handoffs)
    {
        var productions = new List<Production>();

        var next = 0;

        for (var call = 0; call < calls.Count; call++)
        {
            var end = calls.GetEndId(call);

            if (end == 0)
            {
                continue;
            }

            var limit = call + 1 < calls.Count ? calls.GetId(call + 1) : ulong.MaxValue;

            while (next < handoffs.Count && handoffs[next].Id <= end)
            {
                next++;
            }

            if (next < handoffs.Count && handoffs[next].Id < limit)
            {
                productions.Add(new Production(call, handoffs[next].Table, handoffs[next].Index));
            }
        }

        return [.. productions];
    }

    private sealed record Handoff(ulong Id, BatchTable Table, int Index);

    private sealed record Production(int Call, BatchTable Table, int Index);

    internal sealed class Log
    {
        private Dictionary<(ulong Address, ulong Instance), BatchTable.Builder> Builders { get; } = [];

        public unsafe void Add(IntPtr batches,
                               int batchCount,
                               IntPtr vectors,
                               int vectorCount,
                               IntPtr selection,
                               int selectionCount,
                               IntPtr slots,
                               int slotCount,
                               IntPtr deepValues,
                               int deepValueCount,
                               IntPtr data,
                               int dataLength)
            => Add(new ReadOnlySpan<CapturedBatch>((void*)batches, batchCount),
                   new ReadOnlySpan<CapturedVector>((void*)vectors, vectorCount),
                   new ReadOnlySpan<ushort>((void*)selection, selectionCount),
                   new ReadOnlySpan<ulong>((void*)slots, slotCount),
                   new ReadOnlySpan<CapturedDeepValue>((void*)deepValues, deepValueCount),
                   new ReadOnlySpan<byte>((void*)data, dataLength));

        public void Add(ReadOnlySpan<CapturedBatch> batches,
                        ReadOnlySpan<CapturedVector> vectors,
                        ReadOnlySpan<ushort> selection,
                        ReadOnlySpan<ulong> slots,
                        ReadOnlySpan<CapturedDeepValue> deepValues,
                        ReadOnlySpan<byte> data)
        {
            foreach (var batch in batches)
            {
                if (!IsValid(batch, vectors, selection.Length, slots.Length, deepValues, data.Length))
                {
                    continue;
                }

                ref var builder = ref CollectionsMarshal.GetValueRefOrAddDefault(Builders, (batch.Function, batch.Instance), out _);

                builder ??= new BatchTable.Builder(batch.Function, batch.Instance);

                builder.Add(batch, vectors, selection, slots, deepValues, data);
            }
        }

        public BatchIndex Build() => new(Builders.ToDictionary(b => b.Key, b => b.Value.Build()));

        private static bool IsValid(CapturedBatch batch,
                                    ReadOnlySpan<CapturedVector> vectors,
                                    int selectionCount,
                                    int slotCount,
                                    ReadOnlySpan<CapturedDeepValue> deepValues,
                                    int dataLength)
        {
            if ((ulong)batch.FirstSelected + batch.SelectedCount > (ulong)selectionCount
                || (ulong)batch.FirstVector + batch.VectorCount > (ulong)vectors.Length)
            {
                return false;
            }

            foreach (var vector in vectors.Slice((int)batch.FirstVector, (int)batch.VectorCount))
            {
                if ((ulong)vector.FirstSlot + vector.SlotCount > (ulong)slotCount
                    || (ulong)vector.FirstDeepValue + vector.DeepValueCount > (ulong)deepValues.Length)
                {
                    return false;
                }

                foreach (var deep in deepValues.Slice((int)vector.FirstDeepValue, (int)vector.DeepValueCount))
                {
                    if ((ulong)deep.Data + deep.Captured > (ulong)dataLength || deep.Slot >= vector.SlotCount)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
