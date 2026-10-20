using System.Runtime.InteropServices;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Calls;

namespace InternalsViewer.Query.FullTrace.Timeline;

internal sealed class OperatorMarks<T> where T : ITraceMark
{
    private OperatorMarks(Dictionary<ExecutionOperatorEvent, Dictionary<uint, TraceMarkIndex<T>>> marks)
    {
        Marks = marks;
    }

    public static OperatorMarks<T> Empty { get; }
        = new(new Dictionary<ExecutionOperatorEvent, Dictionary<uint, TraceMarkIndex<T>>>(ReferenceEqualityComparer.Instance));

    private Dictionary<ExecutionOperatorEvent, Dictionary<uint, TraceMarkIndex<T>>> Marks { get; }

    public static OperatorMarks<T> Build(IEnumerable<T> marks, Func<T, ExecutionOperatorEvent?> getOperator, Func<T, uint> getThread)
    {
        var grouped = new Dictionary<ExecutionOperatorEvent, Dictionary<uint, List<T>>>(ReferenceEqualityComparer.Instance);

        foreach (var mark in marks)
        {
            if (getOperator(mark) is not { } operatorEvent)
            {
                continue;
            }

            ref var threads = ref CollectionsMarshal.GetValueRefOrAddDefault(grouped, operatorEvent, out _);

            threads ??= [];

            ref var list = ref CollectionsMarshal.GetValueRefOrAddDefault(threads, getThread(mark), out _);

            (list ??= []).Add(mark);
        }

        var indexed = new Dictionary<ExecutionOperatorEvent, Dictionary<uint, TraceMarkIndex<T>>>(ReferenceEqualityComparer.Instance);

        foreach (var (operatorEvent, threads) in grouped)
        {
            indexed[operatorEvent] = threads.ToDictionary(t => t.Key, t => new TraceMarkIndex<T>([.. t.Value]));
        }

        return new OperatorMarks<T>(indexed);
    }

    public static ExecutionOperatorEvent?[] GetEnclosing(TraceCallStore calls,
                                                         Dictionary<ulong, ExecutionOperatorEvent> owners,
                                                         IReadOnlyList<(uint Thread, TracePosition From)> points)
    {
        var operators = new ExecutionOperatorEvent?[points.Count];

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

        for (var index = 0; index < points.Count; index++)
        {
            Add(points[index].Thread, new SweepEntry(points[index].From, points[index].From, 0, index));
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

                if (entry.Point >= 0)
                {
                    operators[entry.Point] = open.TryPeek(out var enclosing) ? owners[enclosing.Instance] : null;

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

    public TraceMarkIndex<T>? Get(ExecutionOperatorEvent operatorEvent, uint thread)
        => Marks.TryGetValue(operatorEvent, out var threads) ? threads.GetValueOrDefault(thread) : null;

    private readonly record struct SweepEntry(TracePosition From, TracePosition To, ulong Instance, int Point);
}
