using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using InternalsViewer.Execution.AccessPaths.Aggregation;
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

namespace InternalsViewer.UI.App.Services.Query.Trace;

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
        if (OperatorClassifier.IsHashAggregate(node))
        {
            return BuildHashAggregate(node);
        }

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

        if (OperatorClassifier.IsConcatenation(node))
        {
            return BuildConcatenation(node);
        }

        if (OperatorClassifier.IsSort(node))
        {
            return BuildSort(node);
        }

        if (OperatorClassifier.IsStreamAggregate(node))
        {
            return BuildStreamAggregate(node);
        }

        if (OperatorClassifier.IsComputeScalar(node))
        {
            return BuildComputeScalar(node);
        }

        if (OperatorClassifier.IsRead(node))
        {
            return BuildAccess(node);
        }

        return null;
    }

    private IteratorDefinition? BuildSort(PlanNode node)
    {
        if (node.Children.Count != 1 || node.SortColumns.Count == 0 || node.SortInfo is { WithTies: true })
        {
            return null;
        }

        if (Build(node.Children[0]) is not { } source)
        {
            return null;
        }

        Nodes[node.NodeId] = node;

        return new SortDefinition(source)
        {
            NodeId = node.NodeId,
            OutputList = OutputList(node),
            Keys = node.SortColumns.Select(c => new SortKey(ResolveColumnName(c.Column.Column), !c.Ascending)).ToList(),
            IsDistinct = node.SortInfo?.Distinct ?? false,
            TopCount = node.SortInfo?.TopRows
        };
    }

    private IteratorDefinition? BuildStreamAggregate(PlanNode node)
    {
        if (node.AggregateInfo is not { HasUntranslatedAggregate: false } info || node.Children.Count != 1)
        {
            return null;
        }

        if (info.Columns.Count == 0 && info.GroupBy.Count == 0)
        {
            return null;
        }

        if (Build(node.Children[0]) is not { } source)
        {
            return null;
        }

        RegisterAggregateTypes(info);

        Nodes[node.NodeId] = node;

        return new StreamAggregateDefinition(source)
        {
            NodeId = node.NodeId,
            OutputList = OutputList(node),
            GroupBy = [.. info.GroupBy.Select(c => ResolveColumnName(c.Column))],
            Aggregates = info.Columns
        };
    }

    private IteratorDefinition? BuildHashAggregate(PlanNode node)
    {
        if (node.AggregateInfo is not { HasUntranslatedAggregate: false, GroupBy.Count: > 0 } info || node.Children.Count != 1)
        {
            return null;
        }

        if (Build(node.Children[0]) is not { } source)
        {
            return null;
        }

        RegisterAggregateTypes(info);

        Nodes[node.NodeId] = node;

        return new HashAggregateDefinition(source)
        {
            NodeId = node.NodeId,
            OutputList = OutputList(node),
            GroupBy = [.. info.GroupBy.Select(c => ResolveColumnName(c.Column))],
            Aggregates = info.Columns,
            RowEstimate = node.EstimatedRows > 0 ? node.EstimatedRows : node.RowsOutput
        };
    }

    private IteratorDefinition? BuildComputeScalar(PlanNode node)
    {
        if (node.Children.Count != 1)
        {
            return null;
        }

        if (Build(node.Children[0]) is not { } source)
        {
            return null;
        }

        var columns = new List<ComputedColumn>();

        foreach (var definedValue in node.DefinedValues)
        {
            if (definedValue.Columns.Count != 1 || definedValue.ParsedExpression is not { } expression)
            {
                return null;
            }

            var name = definedValue.Columns[0].Column.Trim('[', ']');

            columns.Add(new ComputedColumn(name, expression)
            {
                DataType = definedValue.DataType,
                Text = definedValue.Expression ?? string.Empty
            });

            _typesByColumn[name] = definedValue.DataType ?? TypeOf(expression);
        }

        Nodes[node.NodeId] = node;

        return new ComputeScalarDefinition(source)
        {
            NodeId = node.NodeId,
            OutputList = OutputList(node),
            Columns = columns
        };
    }

    private void RegisterAggregateTypes(AggregateInfo info)
    {
        foreach (var column in info.Columns)
        {
            var argumentType = column.Argument is null ? null : TypeOf(column.Argument);

            _typesByColumn[column.Column] = AggregateFunctions.ResultType(column.Function, argumentType);
        }
    }

    private SqlDbType? TypeOf(AccessExpression expression)
        => expression switch
        {
            AccessExpression.Column column => _typesByColumn.GetValueOrDefault(column.Name.Trim('[', ']')),
            AccessExpression.Constant constant => constant.Value.DataType,
            _ => null
        };

    private IteratorDefinition? BuildConcatenation(PlanNode node)
    {
        if (node.Children.Count < 2)
        {
            return null;
        }

        var inputs = new List<IteratorDefinition>();

        foreach (var child in node.Children)
        {
            if (Build(child) is not { } input)
            {
                return null;
            }

            inputs.Add(input);
        }

        foreach (var definedValue in node.DefinedValues)
        {
            if (definedValue.Columns.Count >= 2)
            {
                _columnAliases[definedValue.Columns[0].Column.Trim('[', ']')] = ResolveColumn(definedValue.Columns[1]);
            }
        }

        Nodes[node.NodeId] = node;

        return new ConcatenationDefinition(inputs)
        {
            NodeId = node.NodeId,
            OutputList = OutputList(node)
        };
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

        Nodes[node.NodeId] = node;

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

        Nodes[node.NodeId] = node;

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

        Nodes[node.NodeId] = node;

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

    private readonly Dictionary<string, ColumnReference> _columnAliases = new(StringComparer.OrdinalIgnoreCase);

    private ColumnReference ResolveColumn(ColumnReference column)
        => _columnAliases.TryGetValue(column.Column.Trim('[', ']'), out var resolved) ? resolved : column;

    private string ResolveColumnName(string name)
    {
        var trimmed = name.Trim('[', ']');

        return _columnAliases.TryGetValue(trimmed, out var resolved) ? resolved.Column.Trim('[', ']') : trimmed;
    }

    private IReadOnlyList<OutputColumn> OutputList(PlanNode node)
        => [.. node.OutputColumns.Select(ToOutputColumn)];

    private OutputColumn ToOutputColumn(ColumnReference column)
    {
        column = ResolveColumn(column);

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
