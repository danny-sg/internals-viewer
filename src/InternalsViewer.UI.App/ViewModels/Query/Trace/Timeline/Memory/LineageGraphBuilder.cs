using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Trace.Lineage;
using InternalsViewer.UI.App.ViewModels.Query.Trace.MemoryHistory;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;

public static class LineageGraphBuilder
{
    private const double Margin = 16;

    private const double RowGap = 24;

    private const string UnknownOperator = "Unknown Operator";

    public static LineageGraph? Build(MemoryHistoryViewModel history, int nodeId, PlanOutputColumn column, Action<ulong> open)
    {
        var path = history.GetOutputPath(nodeId, column);

        if (path.Stages.Count == 0)
        {
            return null;
        }

        var stages = new List<LineageStage>(path.Stages.Count);

        foreach (var stage in path.Stages)
        {
            stages.Add(new LineageStage(GetOperator(history, stage),
                                        PlanOutputLinkText.GetName(stage.Type),
                                        stage,
                                        stage.Consumer >= 0 ? stages[stage.Consumer] : null));
        }

        var depth = stages.Max(s => s.Stage.Depth);

        foreach (var stage in stages)
        {
            stage.X = Margin + (depth - stage.Stage.Depth) * (LineageGraph.BoxWidth + LineageGraph.ColumnGap);
        }

        var bottom = Place(stages, stages[0], Margin);

        return new LineageGraph(stages, GetWarnings(history, path), open)
        {
            Width = Margin * 2 + (depth + 1) * LineageGraph.BoxWidth + depth * LineageGraph.ColumnGap,
            Height = bottom - RowGap + Margin
        };
    }

    public static void Refresh(LineageGraph graph, MemoryHistoryViewModel history, MemoryHistoryReader memory)
    {
        var version = ++graph.Version;

        foreach (var stage in graph.Stages)
        {
            stage.Value = PlanOutputValueReader.Read(memory, stage.Stage) ?? string.Empty;

            if (stage.Value.Length == 0 && history.GetOutputRecord(memory, stage.Stage) is { } record)
            {
                _ = ReadRecordAsync(graph, history, stage, record, version);
            }
        }

        graph.RaiseChanged();
    }

    private static async Task ReadRecordAsync(LineageGraph graph,
                                              MemoryHistoryViewModel history,
                                              LineageStage stage,
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

    private static double Place(List<LineageStage> stages, LineageStage stage, double top)
    {
        var next = top;

        LineageStage? first = null;

        LineageStage? last = null;

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

        return Math.Max(next, stage.Y + LineageGraph.BoxHeight + RowGap);
    }

    private static string GetOperator(MemoryHistoryViewModel history, PlanOutputStage stage)
    {
        if (history.IteratorTargets.TryGetValue(stage.Iterator, out var iterator))
        {
            return iterator.Label;
        }

        return history.IteratorTargets.TryGetValue(stage.PlanObject, out var planObject) ? planObject.Label : UnknownOperator;
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
