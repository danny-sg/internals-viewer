using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Calls;

namespace InternalsViewer.Query.FullTrace.Arguments;

public sealed class HoBtAccessMap
{
    private const string ClassName = "HoBtAccess";

    private const string InitMethod = "Init";

    private const string DestructorMethod = "~HoBtAccess";

    private const int ObjectSlot = 0;

    private const int HoBtIdSlot = 2;

    private const int PackedShift = 16;

    private const int FullHoBtIdShift = 48;

    private HoBtAccessMap(Dictionary<ulong, Lifetime[]> lifetimes)
    {
        Lifetimes = lifetimes;
    }

    public static HoBtAccessMap Empty { get; } = new([]);

    public int Count => Lifetimes.Count;

    private Dictionary<ulong, Lifetime[]> Lifetimes { get; }

    public static HoBtAccessMap Build(TraceCallStore callStore, IReadOnlyList<CallStackNode?> nodes)
    {
        var destroyed = CallsOf(callStore, nodes, DestructorMethod).ToLookup(c => c.Slot(ObjectSlot), c => c.Sequence);

        var lifetimes = new Dictionary<ulong, Lifetime[]>();

        foreach (var calls in CallsOf(callStore, nodes, InitMethod).GroupBy(c => c.Slot(ObjectSlot)))
        {
            var ends = destroyed[calls.Key].Order().ToArray();

            lifetimes[calls.Key] = [.. calls.Where(c => c.Slot(HoBtIdSlot) != 0)
                                           .GroupBy(c => c.Sequence)
                                           .OrderBy(g => g.Key)
                                           .Select(g => new Lifetime(g.Key,
                                                                     ends.FirstOrDefault(e => e >= g.Key, ulong.MaxValue),
                                                                     GetHoBtId([.. g.Select(c => c.Slot(HoBtIdSlot)).Distinct()])))];
        }

        return new HoBtAccessMap(lifetimes);
    }

    public ulong? GetHoBtId(ulong address, ulong sequence)
    {
        if (!Lifetimes.TryGetValue(address, out var lifetimes))
        {
            return null;
        }

        for (var index = lifetimes.Length - 1; index >= 0; index--)
        {
            if (lifetimes[index].Start <= sequence)
            {
                return sequence <= lifetimes[index].End ? lifetimes[index].HoBtId : null;
            }
        }

        return null;
    }

    public ulong? GetHoBtId(ulong address)
        => Lifetimes.TryGetValue(address, out var lifetimes) && lifetimes.Length > 0 && lifetimes.All(l => l.HoBtId == lifetimes[0].HoBtId)
            ? lifetimes[0].HoBtId
            : null;

    private static ulong GetHoBtId(ulong[] values)
    {
        foreach (var value in values)
        {
            if (values.Contains(value >> PackedShift))
            {
                return value;
            }
        }

        return values[0] >> FullHoBtIdShift == 0 ? values[0] << PackedShift : values[0];
    }

    private static IEnumerable<TraceArgumentCall> CallsOf(TraceCallStore callStore, IReadOnlyList<CallStackNode?> nodes, string method)
    {
        var functions = nodes.OfType<CallStackNode>()
                             .Where(n => n.Frame?.Resolved is { ClassName: ClassName } resolved && resolved.MethodName == method)
                             .Select(n => (n.Frame!.Address, n.Frame.Instance))
                             .Distinct();

        foreach (var (address, instance) in functions)
        {
            if (callStore.GetCallList(address, instance) is not { } calls)
            {
                continue;
            }

            foreach (var call in calls)
            {
                yield return call;
            }
        }
    }

    private readonly record struct Lifetime(ulong Start, ulong End, ulong HoBtId);
}
