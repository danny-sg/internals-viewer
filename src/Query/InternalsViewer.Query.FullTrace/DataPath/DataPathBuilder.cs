using InternalsViewer.Query.FullTrace.DataPath.Enums;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Iterators.Enums;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output.Enums;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;
using InternalsViewer.Query.Plans.Enums;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.Query.Plans.Operators;

namespace InternalsViewer.Query.FullTrace.DataPath;

public sealed class DataPathBuilder
{
    private DataPathBuilder(PlanNode statement,
                            IReadOnlyCollection<int> operators,
                            Func<int, IReadOnlyList<PlanOutputColumn>> getOutputs,
                            Func<int, PlanOutputColumn, PlanOutputPath> getPath,
                            IReadOnlyDictionary<int, PlanOutputRoute> routes,
                            Func<int, (TracePosition From, TracePosition To)?> getScope)
    {
        Statement = statement;

        Operators = [.. operators];

        GetOutputs = getOutputs;

        GetPath = getPath;

        Routes = routes;

        GetScope = getScope;

        Pulls = ExpressionPullResolver.Resolve(statement);
    }

    private PlanNode Statement { get; }

    private HashSet<int> Operators { get; }

    private Func<int, IReadOnlyList<PlanOutputColumn>> GetOutputs { get; }

    private Func<int, PlanOutputColumn, PlanOutputPath> GetPath { get; }

    private IReadOnlyDictionary<int, PlanOutputRoute> Routes { get; }

    private Func<int, (TracePosition From, TracePosition To)?> GetScope { get; }

    private List<ExpressionPull> Pulls { get; }

    private Dictionary<int, PlanNode> Nodes { get; } = [];

    private Dictionary<int, PlanNode> Parents { get; } = [];

    private Dictionary<int, int> Levels { get; } = [];

    private Dictionary<(int NodeId, int Index), DataPathField> OwnFields { get; } = [];

    private Dictionary<(int NodeId, int Index), DataPathField> Outputs { get; } = [];

    private Dictionary<(int NodeId, string Table, string Column), DataPathField> UsedFields { get; } = [];

    private Dictionary<PlanOutputRouteNode, PlanOutputRouteNode> Containers { get; } = [];

    public static DataPathMap Build(TraceResult trace,
                                    PlanNode statement,
                                    IReadOnlyDictionary<ulong, IteratorTarget> targets,
                                    bool isPlanObjectIncluded)
    {
        var operators = new Dictionary<int, List<KeyValuePair<ulong, IteratorTarget>>>();

        foreach (var target in targets)
        {
            if (target.Value.Operator?.PlanNodeIdentifier is not { } identifier)
            {
                continue;
            }

            if (!operators.TryGetValue(identifier.NodeId, out var found))
            {
                found = [];

                operators[identifier.NodeId] = found;
            }

            found.Add(target);
        }

        var routes = trace.GetDataPath([.. operators.Select(o => CreateRouteOperator(trace, o.Key, o.Value))], isPlanObjectIncluded);

        return Build(statement,
                     operators.Keys,
                     n => trace.OutputColumns.GetValueOrDefault(n) ?? [],
                     trace.GetOutputPath,
                     routes,
                     n => GetRowRange(trace, operators.GetValueOrDefault(n) ?? []));
    }

    internal static DataPathMap Build(PlanNode statement,
                                      IReadOnlyCollection<int> operators,
                                      Func<int, IReadOnlyList<PlanOutputColumn>> getOutputs,
                                      Func<int, PlanOutputColumn, PlanOutputPath> getPath,
                                      IReadOnlyDictionary<int, PlanOutputRoute> routes,
                                      Func<int, (TracePosition From, TracePosition To)?> getScope)
        => new DataPathBuilder(statement, operators, getOutputs, getPath, routes, getScope).BuildMap();

    private static PlanOutputRouteOperator CreateRouteOperator(TraceResult trace,
                                                               int nodeId,
                                                               List<KeyValuePair<ulong, IteratorTarget>> targets)
    {
        var iterator = targets.FirstOrDefault(t => t.Value.Type == IteratorTargetType.Iterator).Key;

        var planObject = targets.FirstOrDefault(t => t.Value.Type == IteratorTargetType.PlanObject).Key;

        var outputs = trace.OutputColumns.GetValueOrDefault(nodeId) ?? [];

        return new PlanOutputRouteOperator(nodeId, planObject, iterator, [.. outputs.Select(c => trace.GetOutputPath(nodeId, c))]);
    }

    private static (TracePosition From, TracePosition To)? GetRowRange(TraceResult trace,
                                                                       List<KeyValuePair<ulong, IteratorTarget>> targets)
    {
        (TracePosition From, TracePosition To)? scope = null;

        foreach (var (address, _) in targets)
        {
            if (trace.GetRowRange(address) is not { } range)
            {
                continue;
            }

            scope = scope is { } found
                ? (range.From < found.From ? range.From : found.From, range.To > found.To ? range.To : found.To)
                : range;
        }

        return scope;
    }

    private static IEnumerable<PlanNode> GetNodes(PlanNode node) => node.Children.SelectMany(c => GetNodes(c).Prepend(c));

    private static PlanNode? FindChild(PlanNode node, string column, string table)
        => node.Children.Find(c => c.OutputColumns.Exists(o => IsMatch(o, column, table)));

    private static bool IsMatch(ColumnReference reference, string column, string table)
        => reference.Column == column && (table.Length == 0 || reference.Table.Length == 0 || reference.Table == table);

    private static ColumnReference GetColumn(PlanNode node, int count, int index)
        => node.OutputColumns.Count == count ? node.OutputColumns[index] : new ColumnReference { Column = $"Column {index + 1}" };

    private static DataPathFieldKind GetKind(PlanOutputPath path)
    {
        if (path.Stages is not [var stage, ..])
        {
            return DataPathFieldKind.None;
        }

        var links = path.Links.Where(l => l.Depth == 0).ToList();

        if (stage.Type == PlanOutputStageType.Expression || links.Exists(l => l.ClassName == PlanOutputValueLayout.ExpressionValueClass))
        {
            return DataPathFieldKind.Computed;
        }

        return stage.Type switch
        {
            PlanOutputStageType.RowBuffer or PlanOutputStageType.Variables or PlanOutputStageType.Counter => DataPathFieldKind.Copy,
            PlanOutputStageType.RowContext => links.Exists(l => l.ClassName == TraceHashTableLayout.ValueClass)
                ? DataPathFieldKind.Copy
                : DataPathFieldKind.Pointer,
            PlanOutputStageType.Record or PlanOutputStageType.InputValue => DataPathFieldKind.Pointer,
            _ => DataPathFieldKind.None
        };
    }

    private static IEnumerable<PlanOutputRouteNode> Flatten(IEnumerable<PlanOutputRouteNode> nodes)
        => nodes.SelectMany(n => Flatten(n.Children).Prepend(n));

    private DataPathMap BuildMap()
    {
        foreach (var child in Statement.Children)
        {
            AddLevels(child, null, 0);
        }

        foreach (var root in Routes.Values.SelectMany(r => r.FromIterator.Concat(r.FromPlanObject)))
        {
            AddContainers(root);
        }

        var nodes = GetNodes(Statement).Where(n => Operators.Contains(n.NodeId)).ToList();

        foreach (var node in nodes)
        {
            AddOutputs(node);
        }

        foreach (var field in OwnFields.Values)
        {
            AddInputs(field);
        }

        foreach (var node in nodes)
        {
            AddPassThroughs(node);
        }

        var columns = CreateColumns();

        foreach (var node in nodes)
        {
            AddUses(node);
        }

        foreach (var field in OwnFields.Values.Concat(UsedFields.Values))
        {
            foreach (var link in field.Inputs)
            {
                link.Readers = GetReaders(field.Node.NodeId, link.Source);
            }
        }

        return new DataPathMap
        {
            Statement = Statement,
            Columns = columns,
            Roots = [.. columns, .. UsedFields.Values],
            Fields = [.. columns, .. OwnFields.Values, .. UsedFields.Values],
            Routes = Routes,
            Outputs = Outputs,
            Levels = Levels
        };
    }

    private void AddLevels(PlanNode node, PlanNode? parent, int level)
    {
        Nodes.TryAdd(node.NodeId, node);

        Levels.TryAdd(node.NodeId, level);

        if (parent is not null)
        {
            Parents.TryAdd(node.NodeId, parent);
        }

        foreach (var child in node.Children)
        {
            AddLevels(child, node, level + 1);
        }
    }

    private void AddContainers(PlanOutputRouteNode node)
    {
        foreach (var child in node.Children)
        {
            Containers[child] = node;

            AddContainers(child);
        }
    }

    private void AddOutputs(PlanNode node)
    {
        var outputs = GetOutputs(node.NodeId);

        var route = Routes.GetValueOrDefault(node.NodeId);

        var scope = GetScope(node.NodeId);

        for (var index = 0; index < outputs.Count; index++)
        {
            var output = outputs[index];

            if (output.ProducerNodeId >= 0 && output.ProducerNodeId != node.NodeId)
            {
                continue;
            }

            var path = GetPath(node.NodeId, output);

            var held = route?.Columns.ElementAtOrDefault(index) is { Node: { } found } location && location.NodeId == node.NodeId
                ? found
                : null;

            var field = new DataPathField(node, GetColumn(node, outputs.Count, index))
            {
                Output = output,
                Type = output.Type,
                Kind = GetKind(path),
                Stage = path.Stages.FirstOrDefault(),
                Held = held,
                Scope = scope
            };

            OwnFields[(node.NodeId, index)] = field;

            Outputs[(node.NodeId, index)] = field;
        }
    }

    private void AddInputs(DataPathField field)
    {
        var node = field.Node;

        var column = field.Column;

        var sources = new List<DataPathField>();

        if (FindDefinition(node, column.Column, column.Table) is { } value)
        {
            field.Definition = value;

            foreach (var reference in value.References)
            {
                AddSources(node, reference, sources);
            }
        }
        else if (FindChild(node, column.Column, column.Table) is { } child)
        {
            (PlanNode Expression, DefinedValueInfo Value)? definition = null;

            GetSources(child, column.Column, column.Table, sources, ref definition);

            if (definition is var (expression, defined))
            {
                field.Definition = defined;

                field.Expression = expression;

                field.Evaluator = GetPuller(expression);
            }
        }

        foreach (var source in sources)
        {
            Link(field, source, ColumnUseType.None);
        }
    }

    private void AddPassThroughs(PlanNode node)
    {
        var outputs = GetOutputs(node.NodeId);

        for (var index = 0; index < outputs.Count; index++)
        {
            if (Outputs.ContainsKey((node.NodeId, index)))
            {
                continue;
            }

            var column = GetColumn(node, outputs.Count, index);

            var sources = new List<DataPathField>();

            if (FindChild(node, column.Column, column.Table) is { } child)
            {
                (PlanNode Expression, DefinedValueInfo Value)? definition = null;

                GetSources(child, column.Column, column.Table, sources, ref definition);
            }

            if (sources is [var single])
            {
                Outputs[(node.NodeId, index)] = single;
            }
        }
    }

    private List<DataPathField> CreateColumns()
    {
        if (Statement.Children is not [var root, ..])
        {
            return [];
        }

        var columns = new List<DataPathField>();

        foreach (var output in root.OutputColumns)
        {
            var sources = new List<DataPathField>();

            (PlanNode Expression, DefinedValueInfo Value)? definition = null;

            GetSources(root, output.Column, output.Table, sources, ref definition);

            var field = new DataPathField(Statement, output)
            {
                Kind = DataPathFieldKind.Statement,
                Definition = definition?.Value,
                Expression = definition?.Expression,
                Evaluator = definition is { } found ? GetPuller(found.Expression) ?? Statement : null,
                Type = sources switch
                {
                    [var only] when definition is null => only.Type,
                    [{ Type: { } type }, ..] => type with { MaxLength = 0 },
                    _ => null
                }
            };

            foreach (var source in sources)
            {
                Link(field, source, ColumnUseType.None);
            }

            columns.Add(field);
        }

        return columns;
    }

    private void AddUses(PlanNode node)
    {
        foreach (var (column, columnUseType) in node.ReferencedColumns)
        {
            if (columnUseType is ColumnUseType.None or ColumnUseType.Expression)
            {
                continue;
            }

            var index = node.OutputColumns.FindIndex(c => IsMatch(c, column.Column, column.Table));

            if (index >= 0 && OwnFields.TryGetValue((node.NodeId, index), out var own))
            {
                own.Uses |= columnUseType;

                continue;
            }

            var sources = new List<DataPathField>();

            AddSources(node, column, sources);

            if (sources.Count == 0)
            {
                continue;
            }

            var key = (node.NodeId, column.Table, column.Column);

            if (!UsedFields.TryGetValue(key, out var field))
            {
                field = new DataPathField(node, column)
                {
                    Kind = DataPathFieldKind.Pointer,
                    Type = sources[0].Type,
                    Scope = GetScope(node.NodeId)
                };

                UsedFields[key] = field;
            }

            field.Uses |= columnUseType;

            foreach (var source in sources)
            {
                Link(field, source, columnUseType);
            }
        }
    }

    private void GetSources(PlanNode node,
                            string column,
                            string table,
                            List<DataPathField> sources,
                            ref (PlanNode Expression, DefinedValueInfo Value)? definition)
    {
        if (Operators.Contains(node.NodeId))
        {
            var index = node.OutputColumns.FindIndex(c => IsMatch(c, column, table));

            if (index >= 0 && OwnFields.TryGetValue((node.NodeId, index), out var field))
            {
                sources.Add(field);
            }
            else if (index >= 0 && FindChild(node, column, table) is { } through)
            {
                GetSources(through, column, table, sources, ref definition);
            }

            return;
        }

        if (FindDefinition(node, column, table) is { } value)
        {
            definition ??= (node, value);

            foreach (var reference in value.References)
            {
                AddSources(node, reference, sources);
            }

            return;
        }

        if (FindChild(node, column, table) is { } child)
        {
            GetSources(child, column, table, sources, ref definition);
        }
    }

    private void AddSources(PlanNode node, ColumnReference reference, List<DataPathField> sources)
    {
        if (FindChild(node, reference.Column, reference.Table) is not { } child)
        {
            return;
        }

        (PlanNode Expression, DefinedValueInfo Value)? nested = null;

        GetSources(child, reference.Column, reference.Table, sources, ref nested);
    }

    private static DefinedValueInfo? FindDefinition(PlanNode node, string column, string table)
        => node.DefinedValues.Find(d => d.Expression is { Length: > 0 } && d.Columns.Exists(c => IsMatch(c, column, table)));

    private void Link(DataPathField field, DataPathField source, ColumnUseType use)
    {
        if (field.Inputs.Find(l => l.Source == source) is not { } link)
        {
            link = new DataPathLink(source, !field.Node.IsStatement && IsBlockingInput(field.Node.NodeId, source.Node.NodeId));

            field.Inputs.Add(link);
        }

        link.Use |= use;
    }

    private PlanNode? GetPuller(PlanNode computeScalar)
        => (Pulls.Find(p => ReferenceEquals(p.ComputeScalar, computeScalar) && p.OutputValues.Count > 0)
            ?? Pulls.Find(p => ReferenceEquals(p.ComputeScalar, computeScalar)))?.Puller;

    private bool IsBlockingInput(int consumerNodeId, int inputNodeId)
    {
        for (var current = Nodes.GetValueOrDefault(inputNodeId); current is not null; current = Parents.GetValueOrDefault(current.NodeId))
        {
            if (Parents.GetValueOrDefault(current.NodeId) is { } parent && parent.NodeId == consumerNodeId)
            {
                return OperatorClassifier.RoleOf(parent, current) == InputRole.Blocking;
            }
        }

        return false;
    }

    private List<PlanOutputRouteNode>? GetReaders(int nodeId, DataPathField source)
    {
        if (source.Held is not { } held || source.Node.NodeId == nodeId || !Routes.TryGetValue(nodeId, out var route))
        {
            return null;
        }

        var targets = GetContainers(held).ToHashSet();

        var nodes = Flatten(route.FromIterator).ToList();

        var inputs = nodes.Where(n => n.Link.Type == PlanOutputLinkType.InputValue).ToList();

        var sourceId = source.Node.NodeId;

        return [.. nodes.Where(n => n.Targets.Exists(t => t.NodeId == sourceId && t.Node is { } target && targets.Contains(target)))
                        .Select(n => GetContainers(n).Select(c => inputs.Find(i => i.Link.Address == c.Link.Address))
                                                     .FirstOrDefault(i => i is not null) ?? n)
                        .Distinct()];
    }

    private IEnumerable<PlanOutputRouteNode> GetContainers(PlanOutputRouteNode node)
    {
        for (var current = node; current is not null; current = Containers.GetValueOrDefault(current))
        {
            yield return current;
        }
    }
}
