using System.Collections;
using InternalsViewer.Query.Events.Operators;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed class TraceOperatorLifetimeList(IReadOnlyList<TraceOperatorLifetime> lifetimes) : IReadOnlyList<TraceOperatorLifetime>
{
    public static TraceOperatorLifetimeList Empty { get; } = new([]);

    public int Count => Lifetimes.Count;

    public IReadOnlyDictionary<ulong, ExecutionOperatorEvent> OperatorsByInstance { get; } = GetOperatorsByInstance(lifetimes);

    private IReadOnlyList<TraceOperatorLifetime> Lifetimes { get; } = lifetimes;

    public TraceOperatorLifetime this[int index] => Lifetimes[index];

    public IEnumerator<TraceOperatorLifetime> GetEnumerator() => Lifetimes.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static Dictionary<ulong, ExecutionOperatorEvent> GetOperatorsByInstance(IReadOnlyList<TraceOperatorLifetime> lifetimes)
    {
        var owners = new Dictionary<ulong, ExecutionOperatorEvent>();

        foreach (var lifetime in lifetimes)
        {
            foreach (var instance in lifetime.Instances)
            {
                owners.TryAdd(instance, lifetime.Operator);
            }
        }

        return owners;
    }
}
