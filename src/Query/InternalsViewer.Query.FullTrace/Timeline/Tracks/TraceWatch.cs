using System.Runtime.InteropServices;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Timeline.Tracks;

public sealed class TraceWatch
{
    private TraceWatch(TraceTickSeries series, Dictionary<ExecutionOperatorEvent, Dictionary<uint, List<Change>>> changes)
    {
        Series = series;

        Changes = changes;
    }

    public static TraceWatch Empty { get; } = new(TraceTickSeries.Empty, new(ReferenceEqualityComparer.Instance));

    public TraceTickSeries Series { get; }

    private Dictionary<ExecutionOperatorEvent, Dictionary<uint, List<Change>>> Changes { get; }

    public static TraceWatch Build(TraceResult trace, TraceTickDefinition definition)
    {
        if (definition.Length <= 0 || definition.Offset < 0)
        {
            return Empty;
        }

        var sources = new List<TraceTickSource>();

        var changes = new Dictionary<ExecutionOperatorEvent, Dictionary<uint, List<Change>>>(ReferenceEqualityComparer.Instance);

        foreach (var lifetime in trace.Lifetimes)
        {
            if (!definition.MatchesOperator(lifetime.Operator))
            {
                continue;
            }

            foreach (var instance in lifetime.Instances)
            {
                if (definition.MatchesName(GetClassName(trace, lifetime, instance) ?? string.Empty))
                {
                    AddChanges(trace, definition, lifetime, instance, sources, changes);
                }
            }
        }

        return sources.Count == 0 ? Empty : new TraceWatch(TraceTickSeries.Create(trace.Timeline, sources), changes);
    }

    public static string? GetClassName(TraceResult trace, TraceOperatorLifetime lifetime, ulong instance)
        => trace.MemoryHistory.GetBlock(instance, lifetime.Start.Position) is >= 0 and var block
            ? trace.AddressMap.GetClassName(instance, trace.MemoryHistory.Blocks[block].FromPosition, lifetime.End.Position)
            : null;

    public byte[]? GetValue(TraceTick tick) => GetValue(tick.Operator, tick.Thread, tick.Span.From.Position);

    public byte[]? GetValue(ExecutionOperatorEvent operatorEvent, uint thread, TracePosition position)
    {
        if (!Changes.TryGetValue(operatorEvent, out var threads) || !threads.TryGetValue(thread, out var list))
        {
            return null;
        }

        var index = SortedSearch.FirstAfter<Change, TracePosition>(CollectionsMarshal.AsSpan(list), position, static c => c.Position) - 1;

        return index >= 0 ? list[index].Value : null;
    }

    private static void AddChanges(TraceResult trace,
                                   TraceTickDefinition definition,
                                   TraceOperatorLifetime lifetime,
                                   ulong instance,
                                   List<TraceTickSource> sources,
                                   Dictionary<ExecutionOperatorEvent, Dictionary<uint, List<Change>>> changes)
    {
        var found = ReadChanges(trace.MemoryHistory, definition, lifetime, instance + (ulong)definition.Offset);

        var timeline = trace.Timeline;

        for (var index = 0; index < found.Count; index++)
        {
            var (write, value) = found[index];

            var location = (timeline.GetNode(write.CallNodeIndex) is { } node ? timeline.FindCall(node, write.CallIndex) : null)
                           ?? timeline.FindSpan(lifetime.Thread, lifetime.Calls[0]);

            if (location is not { } at)
            {
                continue;
            }

            var from = trace.MemoryHistory.GetPoint(write.Index);

            var to = index + 1 < found.Count ? trace.MemoryHistory.GetPoint(found[index + 1].Write.Index) : lifetime.Destroyed;

            var end = to.Position < from.Position ? from : to;

            sources.Add(new TraceTickSource(at.Thread, at.Row.GetStoreIndex(at.Index), definition.Value, from, end)
            {
                Operator = lifetime.Operator
            });

            if (!changes.TryGetValue(lifetime.Operator, out var threads))
            {
                threads = [];

                changes[lifetime.Operator] = threads;
            }

            if (!threads.TryGetValue(at.Thread.ThreadId, out var list))
            {
                list = [];

                threads[at.Thread.ThreadId] = list;
            }

            list.Add(new Change(from.Position, value));
        }
    }

    private static List<(MemoryHistoryWrite Write, byte[] Value)> ReadChanges(MemoryHistoryStore store,
                                                                              TraceTickDefinition definition,
                                                                              TraceOperatorLifetime lifetime,
                                                                              ulong address)
    {
        var found = new List<(MemoryHistoryWrite Write, byte[] Value)>();

        var block = store.GetBlock(address, lifetime.Start.Position);

        if (block < 0 || address - store.Blocks[block].Address + (ulong)definition.Length > (ulong)store.Blocks[block].Size)
        {
            return found;
        }

        var value = new TraceWatchValue((int)(address - store.Blocks[block].Address), definition.Length);

        foreach (var write in store.GetWrites(block))
        {
            if (write.Position > lifetime.Destroyed.Position)
            {
                break;
            }

            if (value.GetOverlap(write.Offset, write.Length) is not (var from, var to))
            {
                continue;
            }

            store.CopyBytes(write, from - write.Offset, value.GetTarget(from, to));

            if (value.TakeChange() is { } changed)
            {
                found.Add((write, changed));
            }
        }

        return found;
    }

    private readonly record struct Change(TracePosition Position, byte[] Value);
}
