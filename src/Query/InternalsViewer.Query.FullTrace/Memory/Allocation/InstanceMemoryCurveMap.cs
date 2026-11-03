using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Timeline;

namespace InternalsViewer.Query.FullTrace.Memory.Allocation;

public sealed class InstanceMemoryCurveMap
{
    private InstanceMemoryCurveMap(Dictionary<(uint Thread, ulong Instance), MemoryUsageCurve> curves)
    {
        Curves = curves;
    }

    public static InstanceMemoryCurveMap Empty { get; } = new([]);

    private Dictionary<(uint Thread, ulong Instance), MemoryUsageCurve> Curves { get; }

    public static InstanceMemoryCurveMap Build(TraceTimeline timeline, AllocationIndex allocation)
    {
        var calls = new Dictionary<uint, Dictionary<ulong, List<TraceSpan>>>();

        foreach (var location in timeline.GetSpanLocations())
        {
            if (GetOwner(location.Node) is not { } instance)
            {
                continue;
            }

            if (!calls.TryGetValue(location.Thread.ThreadId, out var instances))
            {
                instances = [];

                calls[location.Thread.ThreadId] = instances;
            }

            if (!instances.TryGetValue(instance, out var spans))
            {
                spans = [];

                instances[instance] = spans;
            }

            spans.Add(location.Span);
        }

        var curves = new Dictionary<(uint Thread, ulong Instance), MemoryUsageCurve>();

        foreach (var (thread, instances) in calls)
        {
            var owners = instances.Keys.ToList();

            var owned = allocation.InUseOwnedBy(thread, [.. owners.Select(IReadOnlyList<TraceSpan> (o) => instances[o])]);

            for (var index = 0; index < owners.Count; index++)
            {
                curves[(thread, owners[index])] = owned[index];
            }
        }

        return new InstanceMemoryCurveMap(curves);
    }

    public MemoryUsageCurve? GetCurve(uint thread, CallStackNode node)
        => GetOwner(node) is { } instance ? Curves.GetValueOrDefault((thread, instance), MemoryUsageCurve.Empty) : null;

    private static ulong? GetOwner(CallStackNode node)
        => node.Frame is { InstanceAddress: not 0 and var instance } frame && !IteratorSymbolLookup.IsPlanObject(frame)
            ? instance
            : null;
}
