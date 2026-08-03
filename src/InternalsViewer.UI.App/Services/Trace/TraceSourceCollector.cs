using System.Collections.Generic;
using InternalsViewer.Execution.AccessPaths.Definitions;

namespace InternalsViewer.UI.App.Services.Trace;

/// <summary>
/// Finds the inputs a definition tree reads from, in the order a reader meets them
/// </summary>
/// <remarks>
/// Only the leaves become tabs. An operator that reads other operators has no records of its own to show, so it contributes its visual
/// (a hash table, say) rather than a source tab. A join reading a join therefore still yields one tab per underlying table.
/// </remarks>
public static class TraceSourceCollector
{
    public static IReadOnlyList<TraceSource> Collect(IteratorDefinition definition)
    {
        var sources = new List<TraceSource>();

        Walk(definition, string.Empty, sources);

        return sources;
    }

    /// <summary>
    /// Finds the operators that show a visual of their own rather than a set of records
    /// </summary>
    public static IReadOnlyList<JoinDefinition> CollectOperators(IteratorDefinition definition)
    {
        var operators = new List<JoinDefinition>();

        WalkOperators(definition, operators);

        return operators;
    }

    private static void Walk(IteratorDefinition definition, string role, List<TraceSource> sources)
    {
        switch (definition)
        {
            case NestedLoopsDefinition loops:
                Walk(loops.Outer, "Seek", sources);
                Walk(loops.Inner, loops.Inner is HeapFetchDefinition ? "Heap" : "Lookup", sources);

                break;

            case MergeJoinDefinition merge:
                Walk(merge.Outer.Source, "Outer", sources);
                Walk(merge.Inner.Source, "Inner", sources);

                break;

            case HashMatchDefinition hash:
                Walk(hash.Build.Source, "Build", sources);
                Walk(hash.Probe.Source, "Probe", sources);

                break;

            default:
                sources.Add(new TraceSource(definition.NodeId, definition) { Role = role });

                break;
        }
    }

    private static void WalkOperators(IteratorDefinition definition, List<JoinDefinition> operators)
    {
        if (definition is not JoinDefinition join)
        {
            return;
        }

        operators.Add(join);

        switch (join)
        {
            case NestedLoopsDefinition loops:
                WalkOperators(loops.Outer, operators);
                WalkOperators(loops.Inner, operators);

                break;

            case MergeJoinDefinition merge:
                WalkOperators(merge.Outer.Source, operators);
                WalkOperators(merge.Inner.Source, operators);

                break;

            case HashMatchDefinition hash:
                WalkOperators(hash.Build.Source, operators);
                WalkOperators(hash.Probe.Source, operators);

                break;
        }
    }
}
