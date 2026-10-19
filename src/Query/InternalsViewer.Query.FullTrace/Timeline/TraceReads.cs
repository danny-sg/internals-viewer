using System.Runtime.InteropServices;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Timeline.Enums;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed class TraceReads
{
    private const string Module = "sqlmin";

    private const string BufferPool = "BPool";

    private const string GetMethod = "Get";

    private const string ReadAheadMethod = "ReadAhead";

    private const int PageCountSlot = 3;

    private TraceReads(TraceRead[] reads)
    {
        Reads = reads;

        var grouped = new Dictionary<ExecutionOperatorEvent, Dictionary<uint, List<TraceRead>>>(ReferenceEqualityComparer.Instance);

        foreach (var read in reads)
        {
            if (read.Operator is not { } operatorEvent)
            {
                continue;
            }

            ref var threads = ref CollectionsMarshal.GetValueRefOrAddDefault(grouped, operatorEvent, out _);

            threads ??= [];

            ref var list = ref CollectionsMarshal.GetValueRefOrAddDefault(threads, read.Thread, out _);

            (list ??= []).Add(read);
        }

        foreach (var (operatorEvent, threads) in grouped)
        {
            ByOperator[operatorEvent] = threads.ToDictionary(t => t.Key, t => new TraceReadIndex([.. t.Value]));
        }
    }

    public static TraceReads Empty { get; } = new([]);

    public int Count => Reads.Length;

    public IReadOnlyList<TraceRead> All => Reads;

    private TraceRead[] Reads { get; }

    private Dictionary<ExecutionOperatorEvent, Dictionary<uint, TraceReadIndex>> ByOperator { get; } = new(ReferenceEqualityComparer.Instance);

    public TraceReadIndex? GetReads(ExecutionOperatorEvent operatorEvent, uint thread)
        => ByOperator.TryGetValue(operatorEvent, out var threads) ? threads.GetValueOrDefault(thread) : null;

    internal static TraceReads Build(TraceTimeline timeline, TraceCallStore calls)
    {
        var reads = new List<TraceRead>();

        var froms = new List<TracePosition>();

        foreach (var (address, instance) in calls.FunctionKeys)
        {
            if (calls.GetCallList(address, instance) is not { Count: > 0 } list
                || GetReadType(calls.GetNode(list.GetNodeIndex(0))?.Frame) is not { } type)
            {
                continue;
            }

            for (var index = 0; index < list.Count; index++)
            {
                var call = list[index];

                if (GetPage(call) is not { } page || timeline.FindCall(address, instance, index) is not { } location)
                {
                    continue;
                }

                var pageCount = type == TraceReadType.ReadAhead && call.IntegerSlots.Length > PageCountSlot
                    ? (int)call.IntegerSlots[PageCountSlot]
                    : 1;

                reads.Add(new TraceRead(type,
                                        page,
                                        pageCount,
                                        call.ThreadId,
                                        location.Node,
                                        location.Call,
                                        location.Span(TraceTimelineAxis.Position),
                                        location.Span(TraceTimelineAxis.Instructions)));

                froms.Add(call.FromPosition);
            }
        }

        var operators = GetOperators(calls, timeline.GetOperatorsByInstance(), reads, froms);

        return new TraceReads([.. reads.Select((r, i) => operators[i] is { } found ? r with { Operator = found } : r)]);
    }

    private static TraceReadType? GetReadType(CallstackFrame? frame)
        => frame is { Resolved: { ClassName: BufferPool } resolved } && frame.Module.Equals(Module, StringComparison.OrdinalIgnoreCase)
            ? resolved.MethodName switch
            {
                GetMethod => TraceReadType.Page,
                ReadAheadMethod => TraceReadType.ReadAhead,
                _ => null
            }
            : null;

    private static PageAddress? GetPage(TraceArgumentCall call)
    {
        foreach (var locator in call.Locators)
        {
            if (locator.ValueType == LocatorValueType.Page)
            {
                return locator.Page;
            }
        }

        return null;
    }

    private static ExecutionOperatorEvent?[] GetOperators(TraceCallStore calls,
                                                          Dictionary<ulong, ExecutionOperatorEvent> owners,
                                                          List<TraceRead> reads,
                                                          List<TracePosition> froms)
    {
        var operators = new ExecutionOperatorEvent?[reads.Count];

        if (owners.Count == 0)
        {
            return operators;
        }

        var threads = new Dictionary<uint, List<SweepEntry>>();

        foreach (var (address, instance) in calls.FunctionKeys)
        {
            if (!owners.ContainsKey(instance) || calls.GetCallList(address, instance) is not { Count: > 0 } list)
            {
                continue;
            }

            for (var index = 0; index < list.Count; index++)
            {
                Add(list.GetThread(index), new SweepEntry(list.GetFromPosition(index), list.GetToPosition(index), instance, -1));
            }
        }

        for (var index = 0; index < reads.Count; index++)
        {
            Add(reads[index].Thread, new SweepEntry(froms[index], froms[index], 0, index));
        }

        var open = new Stack<SweepEntry>();

        foreach (var thread in threads.Values)
        {
            thread.Sort((left, right) => left.From != right.From ? left.From.CompareTo(right.From) : right.To.CompareTo(left.To));

            open.Clear();

            foreach (var entry in thread)
            {
                while (open.TryPeek(out var outer) && outer.To <= entry.From)
                {
                    open.Pop();
                }

                if (entry.Read >= 0)
                {
                    operators[entry.Read] = open.TryPeek(out var enclosing) ? owners[enclosing.Instance] : null;

                    continue;
                }

                open.Push(entry);
            }
        }

        return operators;

        void Add(uint thread, SweepEntry entry)
        {
            ref var list = ref CollectionsMarshal.GetValueRefOrAddDefault(threads, thread, out _);

            (list ??= []).Add(entry);
        }
    }

    private readonly record struct SweepEntry(TracePosition From, TracePosition To, ulong Instance, int Read);
}
