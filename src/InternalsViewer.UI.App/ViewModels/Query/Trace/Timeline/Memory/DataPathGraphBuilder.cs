using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Trace.DataPath;
using InternalsViewer.UI.App.ViewModels.Query.Trace.MemoryHistory;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;

public static class DataPathGraphBuilder
{
    private const double Margin = 16;

    private const double RowGap = 24;

    private const string UnknownOperator = "Unknown Operator";

    public static DataPathPlan? GetPlan(MemoryHistoryViewModel history)
        => history.IteratorTargets.Values.Select(t => t.Operator?.PlanNodeIdentifier).OfType<PlanNodeIdentifier>().FirstOrDefault() is
               { } identifier
           && history.GetStatement(identifier) is { } statement
            ? new DataPathPlan(statement)
            : null;

    public static DataPathGraph? Build(MemoryHistoryViewModel history,
                                       DataPathPlan? plan,
                                       IReadOnlyList<DataPathColumn> columns,
                                       Action<ulong> open)
    {
        var levels = new Dictionary<DataPathStage, int>();

        var rows = new List<(DataPathColumn Column, List<DataPathStage> Stages)>();

        var warnings = new List<string>();

        foreach (var column in columns)
        {
            column.Outputs.Clear();

            column.Text = string.Empty;

            var stages = new List<DataPathStage>();

            foreach (var input in column.Inputs)
            {
                var path = history.GetOutputPath(input.NodeId, input.Column);

                if (path.Stages.Count == 0)
                {
                    continue;
                }

                AddPath(history, plan, path, column, stages, levels);

                warnings.AddRange(GetWarnings(history, path));
            }

            if (column.Outputs.Count > 0)
            {
                rows.Add((column, stages));
            }
        }

        if (rows.Count == 0)
        {
            return null;
        }

        var order = levels.Values
                          .Distinct()
                          .Order()
                          .Select((level, index) => (Level: level, Index: index))
                          .ToDictionary(l => l.Level, l => l.Index);

        var step = DataPathGraph.BoxWidth + DataPathGraph.ColumnGap;

        var left = Margin + step;

        foreach (var (stage, level) in levels)
        {
            stage.X = left + order[level] * step;
        }

        var bottom = Margin;

        foreach (var (column, stages) in rows)
        {
            var top = bottom;

            foreach (var output in column.Outputs)
            {
                bottom = Place(stages, output, bottom);
            }

            column.X = Margin;

            column.Y = Math.Max(top, Math.Floor(column.Outputs.Average(o => o.Y + DataPathGraph.BoxHeight / 2) - column.Height / 2));

            bottom = Math.Max(bottom, column.Y + column.Height + RowGap);
        }

        return new DataPathGraph([.. rows.SelectMany(r => r.Stages)], [.. rows.Select(r => r.Column)], [.. warnings.Distinct()], open)
        {
            Width = left + order.Count * step - DataPathGraph.ColumnGap + Margin,
            Height = bottom - RowGap + Margin
        };
    }

    public static void Refresh(DataPathGraph graph, MemoryHistoryViewModel history, MemoryHistoryReader memory)
    {
        var version = ++graph.Version;

        var position = memory.Position;

        foreach (var stage in graph.Stages)
        {
            if (stage.Stage is not { } output)
            {
                continue;
            }

            stage.IsActive = stage.Scope is not { } scope || scope.From <= position && position <= scope.To;

            stage.Value = PlanOutputValueReader.Read(memory, output) ?? string.Empty;

            if (stage.Value.Length == 0 && history.GetOutputRecord(memory, output) is { } record)
            {
                _ = ReadRecordAsync(graph, history, stage, record, version);
            }
        }

        if (graph.Columns.Where(c => c.Expression is not null).ToList() is [var column] && GetResultType(column) is { } type)
        {
            column.Text = history.GetStatementResult(memory.Position, type) ?? string.Empty;
        }

        graph.RaiseChanged();
    }

    private static SqlDbType? GetResultType(DataPathColumn column)
        => column.Definition?.DataType
           ?? (column.Inputs is [var first, ..] && SqlTypeHelpers.TryGetSqlType(first.Column.Type.TypeId, out var type) ? type : null);

    private static async Task ReadRecordAsync(DataPathGraph graph,
                                              MemoryHistoryViewModel history,
                                              DataPathStage stage,
                                              PlanOutputRecord record,
                                              int version)
    {
        var value = await history.ReadRecordAsync(record);

        if (value is not null && version == graph.Version)
        {
            stage.Value = value;

            graph.RaiseChanged();
        }
    }

    private static void AddPath(MemoryHistoryViewModel history,
                                DataPathPlan? plan,
                                PlanOutputPath path,
                                DataPathColumn column,
                                List<DataPathStage> stages,
                                Dictionary<DataPathStage, int> levels)
    {
        var count = path.Stages.Count;

        var targets = new IteratorTarget?[count];

        var keys = new ulong[count];

        var nodeIds = new int?[count];

        for (var index = 0; index < count; index++)
        {
            var stage = path.Stages[index];

            keys[index] = history.IteratorTargets.ContainsKey(stage.Iterator) ? stage.Iterator : stage.PlanObject;

            targets[index] = history.IteratorTargets.GetValueOrDefault(keys[index]);

            nodeIds[index] = targets[index]?.Operator?.PlanNodeIdentifier?.NodeId;
        }

        var mapped = new DataPathStage?[count];

        var ids = new int?[count];

        for (var index = 0; index < count; index++)
        {
            var stage = path.Stages[index];

            var consumer = stage.Consumer >= 0 ? mapped[stage.Consumer] : null;

            var consumerId = stage.Consumer >= 0 ? ids[stage.Consumer] : null;

            var isExpression = nodeIds[index] is null
                               && plan is not null
                               && GetInputNodeId(path, index, nodeIds) is { } inputId
                               && plan.GetExpression(consumerId, inputId) is not null;

            if (isExpression || consumer is not null && nodeIds[index] is { } same && same == consumerId)
            {
                mapped[index] = consumer;

                ids[index] = consumerId;

                continue;
            }

            var nodeId = nodeIds[index];

            var name = GetFieldName(history, nodeId, keys[index], stage.Reference);

            var definition = name.Length > 0 ? plan?.FindDefinition(name) : null;

            var isBuild = consumer is not null
                          && (consumer.IsBuild
                              || nodeId is { } own && consumerId is { } reader && plan?.IsBlockingInput(reader, own) == true);

            var box = new DataPathStage(name,
                                        PlanOutputLinkText.GetReferenceName(stage),
                                        targets[index]?.Label ?? UnknownOperator,
                                        keys[index],
                                        consumer)
            {
                Detail = GetDetail(plan, nodeId, definition),
                IsCalculated = definition is not null,
                Stage = stage,
                IsBuild = isBuild,
                Scope = nodeId is { } scoped ? GetScope(history, scoped) : null
            };

            levels[box] = (nodeId is { } id ? plan?.GetLevel(id) : null) ?? (consumer is not null ? levels[consumer] + 1 : 0);

            stages.Add(box);

            if (consumer is null)
            {
                column.Outputs.Add(box);
            }

            mapped[index] = box;

            ids[index] = nodeId;
        }
    }

    private static (TracePosition From, TracePosition To)? GetScope(MemoryHistoryViewModel history, int nodeId)
    {
        (TracePosition From, TracePosition To)? scope = null;

        foreach (var (address, target) in history.IteratorTargets)
        {
            if (target.Operator?.PlanNodeIdentifier?.NodeId != nodeId || history.GetRowRange(address) is not { } range)
            {
                continue;
            }

            scope = scope is { } found
                ? (range.From < found.From ? range.From : found.From, range.To > found.To ? range.To : found.To)
                : range;
        }

        return scope;
    }

    private static string GetDetail(DataPathPlan? plan, int? nodeId, DefinedValueInfo? definition)
    {
        if (definition is not null)
        {
            return DataPathPlan.GetText(definition);
        }

        return nodeId is { } id && plan?.GetNode(id) is { } node ? DataPathPlan.GetObjectName(node) : string.Empty;
    }

    private static string GetFieldName(MemoryHistoryViewModel history, int? nodeId, ulong target, ulong reference)
    {
        if (nodeId is not { } id)
        {
            return string.Empty;
        }

        var outputs = history.GetOutputColumns(id);

        for (var index = 0; index < outputs.Count; index++)
        {
            if (outputs[index].Reference == reference)
            {
                return history.GetOutputColumnNames(target, outputs.Count)[index];
            }
        }

        return string.Empty;
    }

    private static int? GetInputNodeId(PlanOutputPath path, int stageIndex, int?[] nodeIds)
    {
        for (var index = stageIndex + 1; index < path.Stages.Count; index++)
        {
            if (path.Stages[index].Consumer == stageIndex && nodeIds[index] is { } nodeId)
            {
                return nodeId;
            }
        }

        return null;
    }

    private static double Place(List<DataPathStage> stages, DataPathStage stage, double top)
    {
        var next = top;

        DataPathStage? first = null;

        DataPathStage? last = null;

        foreach (var input in stages)
        {
            if (input.Consumer != stage)
            {
                continue;
            }

            next = Place(stages, input, next);

            first ??= input;

            last = input;
        }

        stage.Y = first is null || last is null ? top : Math.Max(top, Math.Floor((first.Y + last.Y) / 2));

        return Math.Max(next, stage.Y + DataPathGraph.BoxHeight + RowGap);
    }

    private static List<string> GetWarnings(MemoryHistoryViewModel history, PlanOutputPath path)
    {
        var warnings = new List<string>();

        foreach (var link in path.Links)
        {
            if (history.GetUnexpectedWriter(link) is { } writer)
            {
                warnings.Add($"{PlanOutputLinkText.GetName(link.Type)} +0x{link.SourceOffset:X} Written By {writer}");
            }
        }

        return warnings;
    }
}
