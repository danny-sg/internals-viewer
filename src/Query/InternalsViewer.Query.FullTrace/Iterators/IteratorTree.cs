using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Batches;
using InternalsViewer.Query.FullTrace.Calls;

namespace InternalsViewer.Query.FullTrace.Iterators;

internal sealed class IteratorTree
{
    private const int InstanceRegister = 0;

    private IteratorTree(Dictionary<ulong, ulong> parents, HashSet<ulong> iterators)
    {
        Parents = parents;

        Iterators = iterators;
    }

    public static IteratorTree Empty { get; } = new([], []);

    private Dictionary<ulong, ulong> Parents { get; }

    private HashSet<ulong> Iterators { get; }

    public bool Contains(ulong iterator) => Iterators.Contains(iterator);

    public ulong GetParent(ulong iterator) => Parents.GetValueOrDefault(iterator);

    public static IteratorTree Build(TraceCallStore callStore, IReadOnlyList<CallStackNode?> nodes, BatchStore batches)
    {
        var constructors = new List<Constructor>();

        var functions = nodes.OfType<CallStackNode>()
                             .Where(IteratorPlanLinker.IsIteratorConstructor)
                             .Select(n => (n.Frame!.Address, n.Frame.Instance))
                             .Distinct();

        foreach (var (address, instance) in functions)
        {
            if (callStore.GetCallList(address, instance) is not { } list)
            {
                continue;
            }

            for (var index = 0; index < list.Count; index++)
            {
                if (list[index].Slot(InstanceRegister) is not 0 and var iterator)
                {
                    constructors.Add(new Constructor(iterator, list.GetThread(index), list.GetFromPosition(index), list.GetToPosition(index)));
                }
            }
        }

        var parents = new Dictionary<ulong, ulong>();

        foreach (var thread in constructors.GroupBy(c => c.Thread))
        {
            var open = new Stack<Constructor>();

            foreach (var constructor in thread.OrderBy(c => c.From))
            {
                while (open.TryPeek(out var outer) && outer.To <= constructor.From)
                {
                    open.Pop();
                }

                if (open.FirstOrDefault(c => c.Iterator != constructor.Iterator) is { Iterator: not 0 } parent)
                {
                    parents.TryAdd(constructor.Iterator, parent.Iterator);
                }

                open.Push(constructor);
            }
        }

        foreach (var capture in batches.All.Where(c => c.Consumer != 0 && c.Consumer != c.Instance))
        {
            parents.TryAdd(capture.Instance, capture.Consumer);
        }

        return new IteratorTree(parents, [.. constructors.Select(c => c.Iterator), .. parents.Keys]);
    }

    private readonly record struct Constructor(ulong Iterator, uint Thread, TracePosition From, TracePosition To);
}
