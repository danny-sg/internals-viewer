using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Timeline;

namespace InternalsViewer.Query.FullTrace.Memory;

public sealed class InstanceMemoryCurves
{
    private InstanceMemoryCurves(Dictionary<(uint Thread, ulong Instance), MemoryInUseCurve> curves)
    {
        Curves = curves;
    }

    public static InstanceMemoryCurves Empty { get; } = new([]);

    private Dictionary<(uint Thread, ulong Instance), MemoryInUseCurve> Curves { get; }

    public static InstanceMemoryCurves Build(TraceTimeline timeline, MemoryIndex memory)
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

        var curves = new Dictionary<(uint Thread, ulong Instance), MemoryInUseCurve>();

        foreach (var (thread, instances) in calls)
        {
            var owners = instances.Keys.ToList();

            var owned = memory.InUseOwnedBy(thread, [.. owners.Select(o => (IReadOnlyList<TraceSpan>)instances[o])]);

            for (var index = 0; index < owners.Count; index++)
            {
                curves[(thread, owners[index])] = owned[index];
            }
        }

        return new InstanceMemoryCurves(curves);
    }

    public MemoryInUseCurve? GetCurve(uint thread, CallStackNode node)
        => GetOwner(node) is { } instance ? Curves.GetValueOrDefault((thread, instance), MemoryInUseCurve.Empty) : null;

    private static ulong? GetOwner(CallStackNode node)
        => node.Frame is { InstanceAddress: not 0 and var instance } frame && !IteratorSymbols.IsPlanObject(frame.Resolved?.ClassName)
            ? instance
            : null;
}
