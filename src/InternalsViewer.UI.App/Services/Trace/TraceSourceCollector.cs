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

        Walk(definition, TraceSourceRole.None, null, sources);

        return sources;
    }

    /// <summary>
    /// Finds the operators that show a visual of their own rather than a set of records
    /// </summary>
    public static IReadOnlyList<IteratorDefinition> CollectOperators(IteratorDefinition definition)
    {
        var operators = new List<IteratorDefinition>();

        WalkOperators(definition, operators);

        return operators;
    }

    private static void Walk(IteratorDefinition definition, TraceSourceRole role, int? operatorNodeId, List<TraceSource> sources)
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

            case ConcatenationDefinition concatenation:
                foreach (var input in concatenation.Inputs)
                {
                    Walk(input, TraceSourceRole.None, null, sources);
                }

                break;

            case UnaryDefinition unary:
                Walk(unary.Source, TraceSourceRole.None, null, sources);

                break;

            default:
                sources.Add(new TraceSource(definition.NodeId, definition)
                {
                    Role = role,
                    OperatorNodeId = operatorNodeId ?? definition.NodeId
                });

                break;
        }
    }

    private static void WalkOperators(IteratorDefinition definition, List<IteratorDefinition> operators)
    {
        operators.Add(definition);

        switch (definition)
        {
            case NestedLoopsDefinition loops:
                WalkJoinSide(loops.Outer, operators);
                WalkJoinSide(loops.Inner, operators);

                break;

            case MergeJoinDefinition merge:
                WalkJoinSide(merge.Outer.Source, operators);
                WalkJoinSide(merge.Inner.Source, operators);

                break;

            case HashMatchDefinition hash:
                WalkJoinSide(hash.Build.Source, operators);
                WalkJoinSide(hash.Probe.Source, operators);

                break;

            case ConcatenationDefinition concatenation:
                foreach (var input in concatenation.Inputs)
                {
                    WalkOperators(input, operators);
                }

                break;

            case UnaryDefinition unary:
                WalkOperators(unary.Source, operators);

                break;
        }
    }

    private static void WalkJoinSide(IteratorDefinition side, List<IteratorDefinition> operators)
    {
        if (!IsLeaf(side))
        {
            WalkOperators(side, operators);
        }
    }

    public static bool IsLeaf(IteratorDefinition definition)
        => definition is not (JoinDefinition or UnaryDefinition or ConcatenationDefinition);
}
