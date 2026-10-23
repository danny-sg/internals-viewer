using System.Runtime.InteropServices;
using InternalsViewer.Query.Events.Operators;

namespace InternalsViewer.Query.FullTrace.Timeline.Ticks;

internal sealed class OperatorMarks<T> where T : ITraceMark
{
    private OperatorMarks(Dictionary<ExecutionOperatorEvent, Dictionary<uint, TraceMarkIndex<T>>> marks)
    {
        Marks = marks;
    }

    public static OperatorMarks<T> Empty { get; }
        = new(new Dictionary<ExecutionOperatorEvent, Dictionary<uint, TraceMarkIndex<T>>>(ReferenceEqualityComparer.Instance));

    private Dictionary<ExecutionOperatorEvent, Dictionary<uint, TraceMarkIndex<T>>> Marks { get; }

    public static OperatorMarks<T> Build(IEnumerable<T> marks,
                                         TraceScale scale,
                                         Func<T, ExecutionOperatorEvent?> getOperator,
                                         Func<T, uint> getThread)
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
            indexed[operatorEvent] = threads.ToDictionary(t => t.Key, t => new TraceMarkIndex<T>(t.Value, scale));
        }

        return new OperatorMarks<T>(indexed);
    }

    public TraceMarkIndex<T>? Get(ExecutionOperatorEvent operatorEvent, uint thread)
        => Marks.TryGetValue(operatorEvent, out var threads) ? threads.GetValueOrDefault(thread) : null;
}
