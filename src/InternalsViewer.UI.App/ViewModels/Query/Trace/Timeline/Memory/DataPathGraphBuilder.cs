using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

    private const string ExpressionType = "Expression";

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
            var stages = new List<DataPathStage>();

            DataPathStage? root = null;

            if (column.Expression is { } expression)
            {
                root = new DataPathStage(DataPathPlan.GetLabel(expression), column.Value?.Expression ?? ExpressionType, null, null);

                levels[root] = plan?.GetLevel(expression.NodeId) ?? 0;

                stages.Add(root);
            }

            foreach (var input in column.Inputs)
            {
                var path = history.GetOutputPath(input.NodeId, input.Column);

                if (path.Stages.Count == 0)
                {
                    continue;
                }

                stages.AddRange(CreateStages(history, plan, path, root, column.Expression?.NodeId, levels));

                warnings.AddRange(GetWarnings(history, path));
            }

            if (stages.Count == 0)
            {
                continue;
            }

            column.Output = stages[0];

            rows.Add((column, stages));
        }

        if (rows.Count == 0)
        {
            return null;
        }

        var minimum = levels.Values.Min();

        var maximum = levels.Values.Max();

        var step = DataPathGraph.BoxWidth + DataPathGraph.ColumnGap;

        var left = Margin + DataPathGraph.ColumnWidth + DataPathGraph.ColumnGap;

        foreach (var (stage, level) in levels)
        {
            stage.X = left + (level - minimum) * step;
        }

        var bottom = Margin;

        foreach (var (column, stages) in rows)
        {
            bottom = Place(stages, stages[0], bottom);

            column.X = Margin;

            column.Y = stages[0].Y;
        }

        return new DataPathGraph([.. rows.SelectMany(r => r.Stages)], [.. rows.Select(r => r.Column)], [.. warnings.Distinct()], open)
        {
            Width = left + (maximum - minimum + 1) * step - DataPathGraph.ColumnGap + Margin,
            Height = bottom - RowGap + Margin
        };
    }

    public static void Refresh(DataPathGraph graph, MemoryHistoryViewModel history, MemoryHistoryReader memory)
    {
        var version = ++graph.Version;

        foreach (var stage in graph.Stages)
        {
            if (stage.Stage is not { } output)
            {
                continue;
            }

            stage.Value = PlanOutputValueReader.Read(memory, output) ?? string.Empty;

            if (stage.Value.Length == 0 && history.GetOutputRecord(memory, output) is { } record)
            {
                _ = ReadRecordAsync(graph, history, stage, record, version);
            }
        }

        graph.RaiseChanged();
    }

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

        stage.Y = first is null || last is null ? top : Math.Floor((first.Y + last.Y) / 2);

        return Math.Max(next, stage.Y + DataPathGraph.BoxHeight + RowGap);
    }

    private static List<DataPathStage> CreateStages(MemoryHistoryViewModel history,
                                                    DataPathPlan? plan,
                                                    PlanOutputPath path,
                                                    DataPathStage? root,
                                                    int? rootNodeId,
                                                    Dictionary<DataPathStage, int> levels)
    {
        var count = path.Stages.Count;

        var targets = new IteratorTarget?[count];

        var nodeIds = new int?[count];

        for (var index = 0; index < count; index++)
        {
            var stage = path.Stages[index];

            targets[index] = history.IteratorTargets.GetValueOrDefault(stage.Iterator)
                             ?? history.IteratorTargets.GetValueOrDefault(stage.PlanObject);

            nodeIds[index] = targets[index]?.Operator?.PlanNodeIdentifier?.NodeId;
        }

        var expressions = new PlanNode?[count];

        var ids = new int?[count];

        for (var index = 0; index < count; index++)
        {
            var consumer = path.Stages[index].Consumer;

            var consumerId = consumer >= 0 ? ids[consumer] : rootNodeId;

            if (nodeIds[index] is null && plan is not null && GetInputNodeId(path, index, nodeIds) is { } inputId)
            {
                expressions[index] = plan.GetExpression(consumerId, inputId);
            }

            ids[index] = nodeIds[index] ?? expressions[index]?.NodeId;
        }

        var stages = new List<DataPathStage>(count);

        var mapped = new DataPathStage?[count];

        for (var index = 0; index < count; index++)
        {
            var stage = path.Stages[index];

            var consumer = stage.Consumer >= 0 ? mapped[stage.Consumer] : root;

            var consumerId = stage.Consumer >= 0 ? ids[stage.Consumer] : rootNodeId;

            if (stage.Consumer >= 0 && ids[index] is not null && ids[index] == consumerId)
            {
                mapped[index] = consumer;

                continue;
            }

            var name = nodeIds[index] is not null
                ? targets[index]!.Label
                : expressions[index] is { } expression ? DataPathPlan.GetLabel(expression) : targets[index]?.Label ?? UnknownOperator;

            var isBuild = consumer is not null
                          && (consumer.IsBuild
                              || ids[index] is { } own && consumerId is { } reader && plan?.IsBlockingInput(reader, own) == true);

            var item = new DataPathStage(name, PlanOutputLinkText.GetName(stage.Type), stage, consumer) { IsBuild = isBuild };

            levels[item] = (ids[index] is { } id ? plan?.GetLevel(id) : null) ?? (consumer is not null ? levels[consumer] + 1 : 0);

            mapped[index] = item;

            stages.Add(item);
        }

        return stages;
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
