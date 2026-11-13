using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.Layouts;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using System.Buffers.Binary;

namespace InternalsViewer.Query.FullTrace.Timeline.Tracks;

public sealed class TraceHashTableMap(TraceResult trace)
{
    private const string PartitionGroupClass = "CHashWorkfilePartitionGroup";

    private const string InsertMethod = "Insert";

    private const string LookupMethod = "Lookup";

    private const string HashIteratorClass = "CQScanHash";

    private const int PartitionGroupArgument = 0;

    private const int HashArgument = 2;

    private const int ActivePartitionOffset = 0xA0;

    private const int BucketMaskOffset = 0x54;

    private Dictionary<ExecutionOperatorEvent, Layout?> Layouts { get; } = new(ReferenceEqualityComparer.Instance);

    private EngineLayout.HashIteratorLayout HashIterator => trace.MemoryHistory.Layout.HashIterator;

    public (ulong Group, int Partition, int Bucket)? GetLocation(TraceTick tick)
    {
        if (tick.Node.Frame is not { Function: { ClassName: PartitionGroupClass, MethodName: InsertMethod or LookupMethod } }
            || trace.CallStore.GetCallList(tick.FunctionAddress, tick.InstanceAddress) is not { } list
            || tick.CallIndex >= list.Count)
        {
            return null;
        }

        var call = list[tick.CallIndex];

        if (GetLayout(tick, call) is not { } layout)
        {
            return null;
        }

        var (partition, bucket) = GetLocation((uint)call.GetSlot(HashArgument),
                                              layout.PartitionMask,
                                              layout.PartitionShift,
                                              layout.BucketMask);

        return (call.GetSlot(PartitionGroupArgument), partition, bucket);
    }

    internal static (int Partition, int Bucket) GetLocation(uint hash, uint partitionMask, int partitionShift, uint bucketMask)
        => ((int)((hash & partitionMask) >> partitionShift), (int)(hash & bucketMask));

    private Layout? GetLayout(TraceTick tick, TraceArgumentCall call)
    {
        if (!Layouts.TryGetValue(tick.Operator, out var layout))
        {
            layout = ReadLayout(tick, call);

            Layouts[tick.Operator] = layout;
        }

        return layout;
    }

    private Layout? ReadLayout(TraceTick tick, TraceArgumentCall call)
    {
        var position = tick.Span.From.Position;

        if (ReadValue(call.GetSlot(PartitionGroupArgument) + ActivePartitionOffset, sizeof(ulong), position) is not { } partition
            || ReadValue(partition + BucketMaskOffset, sizeof(uint), position) is not { } bucketMask
            || GetHashIterator(tick) is not { } iterator
            || ReadValue(iterator, HashIterator.PartitionMaskField, position) is not { } partitionMask
            || ReadValue(iterator, HashIterator.PartitionShiftField, position) is not { } partitionShift)
        {
            return null;
        }

        return new Layout((uint)partitionMask, (int)partitionShift, (uint)bucketMask);
    }

    private ulong? GetHashIterator(TraceTick tick)
    {
        foreach (var lifetime in trace.Lifetimes)
        {
            if (!ReferenceEquals(lifetime.Operator, tick.Operator) || lifetime.Thread != tick.Thread)
            {
                continue;
            }

            foreach (var instance in lifetime.Instances)
            {
                if (TraceWatch.GetClassName(trace, lifetime, instance) == HashIteratorClass)
                {
                    return instance;
                }
            }
        }

        return null;
    }

    private ulong? ReadValue(ulong holder, MemoryField field, TracePosition position)
        => ReadValue(holder + (ulong)field.Offset, field.Length, position);

    private ulong? ReadValue(ulong address, int length, TracePosition position)
    {
        var store = trace.MemoryHistory;

        var block = store.GetBlock(address, position);

        if (block < 0)
        {
            return null;
        }

        var offset = (int)(address - store.Blocks[block].Address);

        var image = store.GetImage(block, store.GetWriteCount(block, position) - 1);

        if (offset + length > image.Data.Length || image.Known.AsSpan(offset, length).Contains(false))
        {
            return null;
        }

        return length == sizeof(uint)
            ? BinaryPrimitives.ReadUInt32LittleEndian(image.Data.AsSpan(offset))
            : BinaryPrimitives.ReadUInt64LittleEndian(image.Data.AsSpan(offset));
    }

    private sealed record Layout(uint PartitionMask, int PartitionShift, uint BucketMask);
}
