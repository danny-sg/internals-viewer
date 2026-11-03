using System.Collections;
using InternalsViewer.Query.Events.Operators;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed class TraceOperatorLifetimes(IReadOnlyList<TraceOperatorLifetime> lifetimes) : IReadOnlyList<TraceOperatorLifetime>
{
    public static TraceOperatorLifetimes Empty { get; } = new([]);

    public int Count => Lifetimes.Count;

    public IReadOnlyDictionary<ulong, ExecutionOperatorEvent> OperatorsByInstance { get; }
        = OperatorHierarchy.GetOperatorsByInstance(lifetimes.Select(l => l.Operator));

    private IReadOnlyList<TraceOperatorLifetime> Lifetimes { get; } = lifetimes;

    public TraceOperatorLifetime this[int index] => Lifetimes[index];

    public IEnumerator<TraceOperatorLifetime> GetEnumerator() => Lifetimes.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
