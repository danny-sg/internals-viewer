using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Output;
using InternalsViewer.Query.FullTrace.Timeline;

namespace InternalsViewer.Query.FullTrace.RowSources;

internal static class TraceOutputLineage
{
    public static IReadOnlyList<TraceOutputRowSource> GetSources(TraceTimeline timeline, IReadOnlyList<TraceOutputStructure> structures)
    {
        var owners = timeline.GetOperatorsByInstance();

        var structure = structures.Where(s => s.Owners.Append(s.DirectSelectOwner).Any(owners.ContainsKey)).MaxBy(s => s.Owners.Count);

        if (structure is null)
        {
            return [];
        }

        if (structure.Owners.Count == 0)
        {
            return [new TraceOutputRowSource(owners[structure.DirectSelectOwner], [], true)];
        }

        return [.. structure.Owners
                            .Select((owner, ordinal) => (Owner: owner, Ordinal: ordinal))
                            .Where(c => owners.ContainsKey(c.Owner))
                            .GroupBy<(ulong Owner, int Ordinal), ExecutionOperatorEvent>(c => owners[c.Owner],
                                                                                         ReferenceEqualityComparer.Instance)
                            .Select(g => new TraceOutputRowSource(g.Key, [.. g.Select(c => c.Ordinal)], false))];
    }
}
