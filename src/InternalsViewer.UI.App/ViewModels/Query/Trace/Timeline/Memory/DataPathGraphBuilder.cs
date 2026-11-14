using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Query.FullTrace.DataPath;
using InternalsViewer.Query.FullTrace.Iterators.Enums;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Trace.DataPath;
using InternalsViewer.UI.App.ViewModels.Query.Trace.MemoryHistory;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;

public static class DataPathGraphBuilder
{
    private const double Margin = 16;

    private const double RowGap = 24;

    private const string UnknownOperator = "Unknown Operator";

    private const int MaximumDepth = 32;

    public static DataPathGraph? Build(MemoryHistoryViewModel history,
                                       DataPathMap map,
                                       IReadOnlyList<DataPathField> roots,
                                       Action<ulong> open)
    {
        var operators = GetOperators(history);

        var levels = new Dictionary<DataPathStage, int>();

        var rows = new List<(DataPathColumn Column, List<DataPathStage> Stages)>();

        var warnings = new List<string>();

        foreach (var root in roots)
        {
            var column = new DataPathColumn(root);

            var stages = new List<DataPathStage>();

            var context = new GraphContext(history, map, operators, column, stages, levels, warnings);

            if (root.Node.IsStatement)
            {
                foreach (var input in root.Inputs)
                {
                    AddStage(context, input.Source, null, input, 0);
                }
            }
            else
            {
                AddStage(context, root, null, null, 0);
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
            stage.IsActive = stage.Field.Scope is not { } scope || scope.From <= position && position <= scope.To;

            if (stage.Field.Stage is not { } output)
            {
                continue;
            }

            stage.Value = PlanOutputValueReader.Read(memory, output) ?? string.Empty;

            if (stage.Value.Length == 0 && history.GetOutputRecord(memory, output) is { } record)
            {
                _ = ReadRecordAsync(graph, history, stage, record, version);
            }
        }

        foreach (var stage in graph.Stages)
        {
            if (stage.Field.Stage is null && graph.Stages.Where(s => s.Consumer == stage).ToList() is [var input])
            {
                stage.Value = input.Value;
            }
        }

        if (graph.Columns.Where(c => c.Definition is not null).ToList() is [var column] && GetResultType(column) is { } type)
        {
            column.Text = history.GetStatementResult(memory.Position, type) ?? string.Empty;
        }

        graph.RaiseChanged();
    }

    private static SqlDbType? GetResultType(DataPathColumn column)
        => column.Definition?.DataType
           ?? (column.Type is { } type && SqlTypeHelpers.TryGetSqlType(type.TypeId, out var found) ? found : null);

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

    private static void AddStage(GraphContext context, DataPathField field, DataPathStage? consumer, DataPathLink? link, int depth)
    {
        var nodeId = field.Node.NodeId;

        var (label, address) = context.Operators.TryGetValue(nodeId, out var found) ? found : (UnknownOperator, 0UL);

        var box = new DataPathStage(field, label, address, consumer, link)
        {
            Detail = GetDetail(field),
            IsBuild = consumer is not null && (consumer.IsBuild || link?.IsBlocking == true)
        };

        context.Levels[box] = context.Map.GetLevel(nodeId) ?? (consumer is not null ? context.Levels[consumer] + 1 : 0);

        context.Stages.Add(box);

        if (consumer is null)
        {
            context.Column.Outputs.Add(box);
        }

        if (field.Output is { } output)
        {
            context.Warnings.AddRange(GetWarnings(context.History, context.History.GetOutputPath(nodeId, output)));
        }

        if (depth >= MaximumDepth)
        {
            return;
        }

        foreach (var input in field.Inputs)
        {
            AddStage(context, input.Source, box, input, depth + 1);
        }
    }

    private static Dictionary<int, (string Label, ulong Address)> GetOperators(MemoryHistoryViewModel history)
        => history.IteratorTargets
                  .Where(t => t.Value.Operator?.PlanNodeIdentifier is not null)
                  .GroupBy(t => t.Value.Operator!.PlanNodeIdentifier!.NodeId)
                  .ToDictionary(g => g.Key,
                                g => (g.First().Value.Label,
                                      g.FirstOrDefault(t => t.Value.Type == IteratorTargetType.Iterator).Key is not 0 and var iterator
                                          ? iterator
                                          : g.FirstOrDefault(t => t.Value.Type == IteratorTargetType.PlanObject).Key));

    private static string GetDetail(DataPathField field) => DataPathText.GetObjectName(field.Node);

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

    private sealed record GraphContext(MemoryHistoryViewModel History,
                                       DataPathMap Map,
                                       Dictionary<int, (string Label, ulong Address)> Operators,
                                       DataPathColumn Column,
                                       List<DataPathStage> Stages,
                                       Dictionary<DataPathStage, int> Levels,
                                       List<string> Warnings);
}
