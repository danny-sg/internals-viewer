using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.Plans;
using InternalsViewer.Query.Plans.Joins;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.Query.Plans.Operators;

namespace InternalsViewer.UI.App.Services.Trace;

/// <summary>
/// Turns a plan operator and everything below it into the definition tree that runs it
/// </summary>
/// <remarks>
/// A showplan node names objects and columns, and resolving those to allocation units, root pages and seek bounds needs the database.
/// Doing it once here is what keeps the plan out of Execution, and what lets an operator be traced without the caller knowing which kind
/// it is. A node with no case returns null and takes its whole tree with it, so a trace is offered only when every operator below it can
/// be simulated.
/// </remarks>
public sealed class TraceDefinitionBuilder(Func<PlanNode, AllocationUnit?> resolveUnit)
{
    /// <summary>
    /// The plan operator behind each definition, by node id
    /// </summary>
    /// <remarks>
    /// Definitions are deliberately free of plan types, but the tabs still have to be labelled and coloured from what the operator reads,
    /// so the mapping is kept alongside rather than carried on the definition.
    /// </remarks>
    public Dictionary<int, PlanNode> Nodes { get; } = [];

    public Dictionary<int, AllocationUnit> Units { get; } = [];

    public bool CanBuild(PlanNode node) => new TraceDefinitionBuilder(resolveUnit).Build(node) is not null;

    public IteratorDefinition? Build(PlanNode node)
    {
        if (OperatorClassifier.IsHash(node))
        {
            return BuildHashMatch(node);
        }

        if (OperatorClassifier.IsMergeJoin(node))
        {
            return BuildMergeJoin(node);
        }

        if (OperatorClassifier.IsNestedLoop(node))
        {
            return BuildNestedLoops(node);
        }

        if (OperatorClassifier.IsRead(node))
        {
            return BuildAccess(node);
        }

        return null;
    }

    private IteratorDefinition? BuildHashMatch(PlanNode node)
    {
        if (HashJoinResolver.Resolve(node) is not { } hash || node.HashInfo is not { } info)
        {
            return null;
        }

        if (BuildSide(hash.Build, info.BuildKeys) is not { } build
            || BuildSide(hash.Probe, info.ProbeKeys) is not { } probe)
        {
            return null;
        }

        return new HashMatchDefinition(build, probe)
        {
            NodeId = node.NodeId,
            JoinType = hash.JoinType,
            Residual = info.Residual,
            HasUntranslatedResidual = info.HasUntranslatedResidual
        };
    }

    private IteratorDefinition? BuildMergeJoin(PlanNode node)
    {
        if (MergeJoinResolver.Resolve(node) is not { } merge || node.MergeInfo is not { } info)
        {
            return null;
        }

        if (BuildSide(merge.Outer, info.OuterKeys) is not { } outer
            || BuildSide(merge.Inner, info.InnerKeys) is not { } inner)
        {
            return null;
        }

        return new MergeJoinDefinition(outer, inner)
        {
            NodeId = node.NodeId,
            JoinType = merge.JoinType,
            Residual = info.Residual,
            HasUntranslatedResidual = info.HasUntranslatedResidual
        };
    }

    private IteratorDefinition? BuildNestedLoops(PlanNode node)
    {
        if (CorrelatedJoinResolver.Resolve(node) is not { } join)
        {
            return null;
        }

        if (Build(join.Outer) is not { } outer || BuildInner(join.Inner) is not { } inner)
        {
            return null;
        }

        return new NestedLoopsDefinition(outer, inner)
        {
            NodeId = node.NodeId,
            JoinType = join.JoinType,
            Residual = node.PredicateInfo?.Residual
        };
    }

    /// <summary>
    /// Builds the side a loop join restarts for each outer row, which binds either a row identifier or key values
    /// </summary>
    private IteratorDefinition? BuildInner(PlanNode node)
    {
        if (CorrelatedJoinResolver.IsRidLookup(node))
        {
            Unit(node);

            return new HeapFetchDefinition
            {
                NodeId = node.NodeId,
                Residual = node.PredicateInfo?.Residual
            };
        }

        if (Unit(node) is not { } unit)
        {
            return null;
        }

        var bindings = (node.PredicateInfo?.CorrelatedSeekColumns ?? [])
                       .Select(c => new CorrelationBinding(c.Column, c.OuterColumn))
                       .ToList();

        if (bindings.Count == 0)
        {
            return null;
        }

        return new SeekDefinition(unit.AllocationUnitId, unit.RootPage, bindings)
        {
            NodeId = node.NodeId,
            Residual = node.PredicateInfo?.Residual
        };
    }

    private IteratorDefinition? BuildAccess(PlanNode node)
    {
        if (CorrelatedJoinResolver.IsRidLookup(node))
        {
            return BuildInner(node);
        }

        if (Unit(node) is not { } unit)
        {
            return null;
        }

        if (unit.IndexId == 0)
        {
            return new AllocationScanDefinition(unit.FirstIamPage)
            {
                NodeId = node.NodeId,
                Residual = Residual(node),
                RowGoal = node.PredicateInfo?.RowGoal,
                HasUntranslatedResidual = node.PredicateInfo?.HasUntranslatedPredicate == true
            };
        }

        return BuildRange(node);
    }

    private RangeDefinition? BuildRange(PlanNode node)
    {
        if (Unit(node) is not { } unit)
        {
            return null;
        }

        return new RangeDefinition(unit.AllocationUnitId, unit.RootPage, Ranges(node))
        {
            NodeId = node.NodeId,
            Residual = Residual(node),
            Direction = Direction(node),
            RowGoal = node.PredicateInfo?.RowGoal,
            HasUntranslatedResidual = node.PredicateInfo?.HasUntranslatedPredicate == true
        };
    }

    /// <summary>
    /// Builds one side of a join, which is whatever access path or operator feeds it plus the columns the join matches on
    /// </summary>
    private JoinInputDefinition? BuildSide(PlanNode node, List<ColumnReference> keys)
    {
        if (Build(node) is not { } source)
        {
            return null;
        }

        return new JoinInputDefinition(source, KeyColumns(keys))
        {
            RowEstimate = node.EstimatedRows > 0 ? node.EstimatedRows : node.RowsOutput
        };
    }

    /// <summary>
    /// Resolves the object an operator reads, recording what was resolved so a tab can be labelled from it later
    /// </summary>
    private AllocationUnit? Unit(PlanNode node)
    {
        Nodes[node.NodeId] = node;

        if (resolveUnit(node) is not { } unit)
        {
            return null;
        }

        Units[node.NodeId] = unit;

        return unit;
    }

    private static IReadOnlyList<SeekBounds> Ranges(PlanNode node)
        => node.PredicateInfo is { HasSeekBounds: true } predicate ? predicate.SeekBounds : [SeekBounds.All];

    private static AccessPredicate? Residual(PlanNode node)
        => node.HasRedundantResidual() ? null : node.PredicateInfo?.Residual;

    private static ScanDirection Direction(PlanNode node)
        => node.ScanInfo?.IsForward == false ? ScanDirection.Backward : ScanDirection.Forward;

    private static IReadOnlyList<string> KeyColumns(List<ColumnReference> keys)
        => [.. keys.Select(k => k.Column.Trim('[', ']'))];
}
