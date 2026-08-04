using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Metadata.Structures;
using InternalsViewer.Internals.Providers.Metadata;
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
public sealed class TraceDefinitionBuilder(Func<PlanNode, AllocationUnit?> resolveUnit, DatabaseSource? database = null)
{
    private readonly Dictionary<string, SqlDbType> _typesByTableColumn = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, SqlDbType?> _typesByColumn = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The plan operator behind each definition, by node id
    /// </summary>
    /// <remarks>
    /// Definitions are deliberately free of plan types, but the tabs still have to be labelled and coloured from what the operator reads,
    /// so the mapping is kept alongside rather than carried on the definition.
    /// </remarks>
    public Dictionary<int, PlanNode> Nodes { get; } = [];

    public Dictionary<int, AllocationUnit> Units { get; } = [];

    public bool CanBuild(PlanNode node) => new TraceDefinitionBuilder(resolveUnit, database).Build(node) is not null;

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

        if (OperatorClassifier.IsTop(node))
        {
            return BuildTop(node);
        }

        if (OperatorClassifier.IsRead(node))
        {
            return BuildAccess(node);
        }

        return null;
    }

    /// <summary>
    /// Builds a TOP, which is traceable only when the number of rows it asks for is known before the input is read
    /// </summary>
    private IteratorDefinition? BuildTop(PlanNode node)
    {
        if (node.TopInfo is not { IsPercent: false, WithTies: false, RowCount: { } rowCount }
            || node.Children.Count != 1)
        {
            return null;
        }

        if (Build(node.Children[0]) is not { } source)
        {
            return null;
        }

        Nodes[node.NodeId] = node;

        return new TopDefinition(source)
        {
            NodeId = node.NodeId,
            OutputList = OutputList(node),
            RowCount = rowCount
        };
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
            OutputList = OutputList(node),
            JoinType = hash.JoinType,
            Residual = Translated(info.Residual, info.HasUntranslatedResidual)
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
            OutputList = OutputList(node),
            JoinType = merge.JoinType,
            Residual = Translated(info.Residual, info.HasUntranslatedResidual)
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
            OutputList = OutputList(node),
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
                OutputList = OutputList(node),
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
            OutputList = OutputList(node),
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
                OutputList = OutputList(node),
                Residual = Translated(Residual(node), node.PredicateInfo?.HasUntranslatedPredicate == true),
                RowGoal = node.PredicateInfo?.RowGoal
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
            OutputList = OutputList(node),
            Residual = Translated(Residual(node), node.PredicateInfo?.HasUntranslatedPredicate == true),
            Direction = Direction(node),
            RowGoal = node.PredicateInfo?.RowGoal
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
            RowEstimate = node.EstimatedRows > 0 ? node.EstimatedRows : node.RowsOutput,
            Direction = source is RangeDefinition range ? range.Direction : ScanDirection.Forward
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

        RegisterColumnTypes(unit);

        return unit;
    }

    private void RegisterColumnTypes(AllocationUnit unit)
    {
        if (database is null)
        {
            return;
        }

        if (unit.IndexId == 0)
        {
            Register(unit.TableName, TableStructureProvider.GetTableStructure(database, unit.AllocationUnitId).Columns);

            return;
        }

        var structure = IndexStructureProvider.GetIndexStructure(database, unit.AllocationUnitId);

        Register(unit.TableName, structure.Columns);

        if (structure.TableStructure is { } table)
        {
            Register(unit.TableName, table.Columns);
        }
    }

    private void Register(string? table, IEnumerable<ColumnStructure> columns)
    {
        foreach (var column in columns)
        {
            if (string.IsNullOrEmpty(column.ColumnName))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(table))
            {
                _typesByTableColumn[$"{table}.{column.ColumnName}"] = column.DataType;
            }

            _typesByColumn[column.ColumnName] = !_typesByColumn.TryGetValue(column.ColumnName, out var existing)
                ? column.DataType
                : existing == column.DataType ? existing : null;
        }
    }

    private IReadOnlyList<OutputColumn> OutputList(PlanNode node)
        => [.. node.OutputColumns.Select(ToOutputColumn)];

    private OutputColumn ToOutputColumn(ColumnReference column)
    {
        var name = column.Column.Trim('[', ']');

        var table = column.Table.Trim('[', ']');

        var type = table.Length > 0 && _typesByTableColumn.TryGetValue($"{table}.{name}", out var qualified)
            ? qualified
            : _typesByColumn.GetValueOrDefault(name);

        return new OutputColumn(name, table.Length > 0 ? table : null, type);
    }

    private static IReadOnlyList<SeekBounds> Ranges(PlanNode node)
        => node.PredicateInfo is { HasSeekBounds: true } predicate ? predicate.SeekBounds : [SeekBounds.All];

    private static AccessPredicate? Residual(PlanNode node)
        => node.HasRedundantResidual() ? null : node.PredicateInfo?.Residual;

    private static AccessPredicate? Translated(AccessPredicate? residual, bool isUntranslated)
    {
        if (!isUntranslated)
        {
            return residual;
        }

        return residual is null
            ? new AccessPredicate.NoTranslation()
            : new AccessPredicate.And([residual, new AccessPredicate.NoTranslation()]);
    }

    private static ScanDirection Direction(PlanNode node)
        => node.ScanInfo?.IsForward == false ? ScanDirection.Backward : ScanDirection.Forward;

    private static IReadOnlyList<string> KeyColumns(List<ColumnReference> keys)
        => [.. keys.Select(k => k.Column.Trim('[', ']'))];
}
