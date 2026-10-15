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

    private Dictionary<(ulong Address, ulong Instance), BatchTable> Tables { get; }

    private Dictionary<(ulong Batch, ulong Consumer), Handoff[]> Handoffs => field ??= BuildHandoffs();

    public bool HasBatches(ulong address, ulong instance) => Tables.ContainsKey((address, instance));

    public bool HasBatch(ulong address, ulong instance, int call)
        => Tables.TryGetValue((address, instance), out var table) && table.HasBatch(call);

    public BatchCapture? GetBatch(ulong address, ulong instance, int call)
        => Tables.TryGetValue((address, instance), out var table) ? table.GetBatch(call) : null;

    public IReadOnlyList<int> GetCalls(ulong address, ulong instance)
        => Tables.TryGetValue((address, instance), out var table) ? table.GetCalls() : [];

    public IReadOnlyList<BatchCapture> GetBatches(ulong address, ulong instance)
        => Tables.TryGetValue((address, instance), out var table) ? [.. table.GetBatches()] : [];

    public IReadOnlyList<BatchSummary> GetSummaries(ulong address, ulong instance)
    {
        if (!Tables.TryGetValue((address, instance), out var table))
        {
            return [];
        }

        return [.. table.GetSummaries()
                        .Select((s, index) => FindInput(table.GetHandoff(index), s.Iterator) is { } input
                                    ? s with { InputSelectedCount = input.Table.GetSelectedCount(input.Index) }
                                    : s)];
    }

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

    internal void AnnotateNodes(TraceCallStore store)
    {
        foreach (var table in Tables.Values)
        {
            if (store.GetCallList(table.Function, table.Instance) is not { } calls)
            {
                continue;
            }

            foreach (var call in table.GetCalls())
            {
                if (call < calls.Count && store.GetNode(calls.GetNodeIndex(call)) is { } node)
                {
                    node.HasBatches = true;
                }
            }
        }
    }

    private sealed record Handoff(ulong Id, BatchTable Table, int Index);

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
