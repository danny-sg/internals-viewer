using System;
using System.Collections.Generic;
using InternalsViewer.Execution.AccessPaths.Definitions;

namespace InternalsViewer.UI.App.Services.Trace;

/// <summary>
/// Finds the inputs a definition tree reads from, in the order a reader meets them
/// </summary>
/// <remarks>
/// Only the leaves become tabs. An operator that reads other operators has no records of its own to show, so it contributes its own visual
/// rather than a source tab. A join reading a join therefore still yields one tab per underlying table.
/// </remarks>
public static class TraceSourceCollector
{
    public static IReadOnlyList<TraceSource> Collect(IteratorDefinition definition)
    {
        var sources = new List<TraceSource>();

        Walk(definition, TraceSourceRole.None, 0, sources);

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

    private static void Walk(IteratorDefinition definition, TraceSourceRole role, int operatorNodeId, List<TraceSource> sources)
    {
        switch (definition)
        {
            case NestedLoopsDefinition loops:
                Walk(loops.Outer, TraceSourceRole.Seek, loops.NodeId, sources);
                Walk(loops.Inner, TraceSourceRole.Lookup, loops.NodeId, sources);

                break;

            case MergeJoinDefinition merge:
                Walk(merge.Outer.Source, TraceSourceRole.Outer, merge.NodeId, sources);
                Walk(merge.Inner.Source, TraceSourceRole.Inner, merge.NodeId, sources);

                break;

            case HashMatchDefinition hash:
                Walk(hash.Build.Source, TraceSourceRole.Build, hash.NodeId, sources);
                Walk(hash.Probe.Source, TraceSourceRole.Probe, hash.NodeId, sources);

                break;

            default:
                sources.Add(new TraceSource(definition.NodeId, definition)
                {
                    Role = role,
                    OperatorNodeId = operatorNodeId
                });

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
